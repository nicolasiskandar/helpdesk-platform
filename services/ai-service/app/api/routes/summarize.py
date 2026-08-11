import json
import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

from app.api.dependencies import get_current_user
from app.core.config import Settings
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])

SUMMARY_TEMPERATURE = 0.2

SUMMARY_INSTRUCTIONS = (
    "You are the Helpdesk AI assistant. Write a concise summary of the ticket "
    "below. Cover the problem, what has been done so far, and the current "
    "state. Use short plain-text bullet points. Do not invent facts that are "
    "not in the ticket."
)


class SummarizeRequest(BaseModel):
    ticketId: str


class TicketServiceError(Exception):
    def __init__(self, status_code: int, detail: str):
        super().__init__(detail)
        self.status_code = status_code
        self.detail = detail


def pick(obj: dict, camel: str, pascal: str, default: str = "") -> str:
    return obj.get(camel) or obj.get(pascal) or default


async def fetch_ticket_thread(base_url: str, token: str, ticket_id: str) -> tuple[dict, list[dict]]:
    headers = {"Authorization": token}
    async with httpx.AsyncClient(timeout=30) as client:
        ticket_resp = await client.get(f"{base_url}/api/tickets/{ticket_id}", headers=headers)
        comments_resp = await client.get(
            f"{base_url}/api/tickets/{ticket_id}/comments", headers=headers
        )
    for resp in (ticket_resp, comments_resp):
        if resp.status_code != 200:
            raise TicketServiceError(resp.status_code, resp.text[:300])
    return ticket_resp.json(), comments_resp.json()


def build_summary_prompt(ticket: dict, comments: list[dict]) -> str:
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
    block = "\n".join(lines)
    return f"{SUMMARY_INSTRUCTIONS}\n\nTicket content:\n{block}\n\nSummary:"


@router.post("/summarize")
async def summarize(
    req: SummarizeRequest,
    request: Request,
    _: JwtClaims = Depends(get_current_user),  # noqa: B008
):
    ticket_id = req.ticketId.strip()
    if not ticket_id:
        raise HTTPException(status_code=400, detail="ticketId cannot be empty.")

    settings: Settings = request.app.state.settings
    llm = request.app.state.llm

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

    prompt = build_summary_prompt(ticket, comments)

    async def event_stream():
        yield {"event": "meta", "data": json.dumps({"comments": len(comments)})}
        try:
            async for token in llm.generate(
                prompt, max_tokens=settings.max_tokens, temperature=SUMMARY_TEMPERATURE
            ):
                yield {"event": "token", "data": json.dumps({"token": token})}
        except Exception as exc:  # noqa: BLE001
            logger.warning("Summary generation failed: %s", exc)
            yield {"event": "error", "data": json.dumps({"error": str(exc)})}
        finally:
            yield {"event": "done", "data": ""}

    return EventSourceResponse(event_stream())
