import json
import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

from app.api.dependencies import get_current_user
from app.core.config import Settings
from app.core.jwt import JwtClaims
from app.services.ticket_client import TicketServiceError, fetch_ticket_thread, pick

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])

TROUBLESHOOTING_TEMPERATURE = 0.2

TROUBLESHOOTING_INSTRUCTIONS = (
    "You are the Helpdesk AI assistant for an IT help desk. Suggest concrete "
    "troubleshooting steps to help resolve the ticket below. Return the steps "
    "as a numbered plain-text list. Ground every step only in the provided "
    "knowledge base articles, similar resolved tickets, and the ticket's own "
    "description and comments. Do not invent steps or facts that are not "
    "supported by the provided context. If there is not enough information to "
    "give useful steps, say so and list what additional details are needed."
)


class TroubleshootingRequest(BaseModel):
    ticketId: str


def build_troubleshooting_prompt(
    ticket: dict, comments: list[dict], context: list[str], similar: list[dict]
) -> str:
    ref = pick(ticket, "referenceNumber", "ReferenceNumber", "?")
    title = pick(ticket, "title", "Title", "(untitled)")
    description = pick(ticket, "description", "Description", "(no description)")
    status = pick(ticket, "statusName", "StatusName", "unknown")
    category = pick(ticket, "categoryName", "CategoryName", "uncategorized")
    priority = pick(ticket, "priorityName", "PriorityName", "medium")

    lines = [
        f"Ticket: {title} ({ref})",
        f"Category: {category} · Priority: {priority} · Status: {status}",
        "",
        "Description:",
        description,
    ]
    if comments:
        lines.append("")
        lines.append("Comments:")
        for i, comment in enumerate(comments, 1):
            content = pick(comment, "content", "Content", "")
            if content:
                lines.append(f"{i}. {content}")
    if similar:
        lines.append("")
        lines.append("Similar resolved tickets:")
        for i, s in enumerate(similar, 1):
            title_ref = s.get("title") or "(untitled)"
            excerpt = (s.get("excerpt") or "").strip()
            lines.append(f"{i}. [{s.get('referenceNumber')}] {title_ref}")
            if excerpt:
                lines.append(f"   {excerpt}")
    if context:
        lines.append("")
        lines.append("Knowledge base context:")
        for i, chunk in enumerate(context, 1):
            lines.append(f"[{i}] {chunk}")
    block = "\n".join(lines)
    return f"{TROUBLESHOOTING_INSTRUCTIONS}\n\nTicket content:\n{block}\n\nTroubleshooting steps:"


@router.post("/troubleshooting")
async def troubleshooting(
    req: TroubleshootingRequest,
    request: Request,
    _: JwtClaims = Depends(get_current_user),  # noqa: B008
):
    ticket_id = req.ticketId.strip()
    if not ticket_id:
        raise HTTPException(status_code=400, detail="ticketId cannot be empty.")

    settings: Settings = request.app.state.settings
    llm = request.app.state.llm
    rag = request.app.state.rag
    similarity = request.app.state.similarity

    models_ok, missing = await request.app.state.are_models_ready(settings)
    if not models_ok:
        raise HTTPException(
            status_code=503,
            detail=f"AI models are still downloading (missing: {', '.join(missing)}). "
            "Please try again in a moment.",
        )

    token = request.headers.get("Authorization", "")
    try:
        ticket, comments = await fetch_ticket_thread(
            settings.ticket_service_base_url, token, ticket_id
        )
    except TicketServiceError as exc:
        logger.warning(
            "Ticket service fetch failed for %s: %d %s",
            ticket_id,
            exc.status_code,
            exc.detail,
        )
        if exc.status_code == 403:
            raise HTTPException(status_code=403, detail="You do not have access to this ticket.") from exc
        if exc.status_code == 404:
            raise HTTPException(status_code=404, detail="Ticket not found.") from exc
        raise HTTPException(status_code=502, detail="Ticket service rejected the request.") from exc
    except httpx.HTTPError as exc:
        logger.warning("Failed to reach ticket service for %s: %s", ticket_id, exc)
        raise HTTPException(status_code=502, detail="Ticket service is unreachable.") from exc

    query = f"{pick(ticket, 'title', 'Title', '')} {pick(ticket, 'description', 'Description', '')}".strip()
    context: list[str] = []
    similar: list[dict] = []
    try:
        context = await rag.retrieve(query)
    except Exception as exc:  # noqa: BLE001
        logger.warning("RAG retrieval failed, grounding on ticket thread only: %s", exc)
    try:
        similar = await similarity.find_similar(query, exclude_ticket_id=ticket_id)
    except Exception as exc:  # noqa: BLE001
        logger.warning("Similar-ticket lookup failed, continuing without it: %s", exc)

    prompt = build_troubleshooting_prompt(ticket, comments, context, similar)

    async def event_stream():
        yield {
            "event": "meta",
            "data": json.dumps({"sources": len(context), "similarTickets": len(similar)}),
        }
        try:
            async for token in llm.generate(
                prompt, max_tokens=settings.max_tokens, temperature=TROUBLESHOOTING_TEMPERATURE
            ):
                yield {"event": "token", "data": json.dumps({"token": token})}
        except Exception as exc:  # noqa: BLE001
            logger.warning("Troubleshooting generation failed: %s", exc)
            yield {"event": "error", "data": json.dumps({"error": str(exc)})}
        finally:
            yield {"event": "done", "data": ""}

    return EventSourceResponse(event_stream())
