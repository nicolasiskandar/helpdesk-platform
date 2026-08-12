import json
import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

from app.api.dependencies import get_current_user
from app.core.config import Settings
from app.core.jwt import JwtClaims
from app.services.ticket_client import (
    TicketServiceError,
    fetch_ticket_thread,
    pick,
    render_ticket_thread,
)

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])

TICKET_CHAT_INSTRUCTIONS = (
    "You are the Helpdesk AI assistant for an IT help desk. Answer the user's "
    "question about the ticket below. Ground your answer only in the ticket's "
    "description and comments, the previous conversation, and any knowledge "
    "base context. If the answer is not in the provided context, say you don't "
    "know and suggest opening a new ticket or contacting the assigned agent. "
    "Be concise and helpful."
)


class ChatMessage(BaseModel):
    role: str
    content: str


class ChatRequest(BaseModel):
    message: str
    ticketId: str | None = None
    history: list[ChatMessage] | None = None


def build_ticket_chat_prompt(
    ticket: dict,
    comments: list[dict],
    query: str,
    history: list[ChatMessage] | None = None,
    context: list[str] | None = None,
) -> str:
    parts = [render_ticket_thread(ticket, comments)]
    if context:
        parts.append("")
        parts.append("Knowledge base context:")
        parts.extend(f"[{i}] {chunk}" for i, chunk in enumerate(context, 1))
    if history:
        parts.append("")
        parts.append("Previous conversation:")
        for item in history:
            role = item.role
            content = item.content
            who = "User" if role == "user" else "Assistant"
            parts.append(f"{who}: {content}")
    block = "\n".join(parts)
    return (
        f"{TICKET_CHAT_INSTRUCTIONS}\n\nTicket content:\n{block}\n\n"
        f"User question: {query}\n\nAnswer:"
    )


@router.get("/health/ready")
async def health_ready(request: Request):
    settings = request.app.state.settings
    models_ok, missing = await request.app.state.are_models_ready(settings)
    if not models_ok:
        raise HTTPException(
            status_code=503,
            detail=f"AI models are still downloading (missing: {', '.join(missing)}).",
        )
    return {"status": "ok"}


@router.post("/chat")
async def chat(req: ChatRequest, request: Request, _: JwtClaims = Depends(get_current_user)):  # noqa: B008
    message = req.message.strip()
    if not message:
        raise HTTPException(status_code=400, detail="Message cannot be empty.")

    settings: Settings = request.app.state.settings
    rag = request.app.state.rag
    llm = request.app.state.llm

    models_ok, missing = await request.app.state.are_models_ready(settings)
    if not models_ok:
        raise HTTPException(
            status_code=503,
            detail=f"AI models are still downloading (missing: {', '.join(missing)}). Please try again in a moment.",
        )

    ticket: dict | None = None
    comments: list[dict] = []
    if req.ticketId:
        ticket_id = req.ticketId.strip()
        if not ticket_id:
            raise HTTPException(status_code=400, detail="ticketId cannot be empty.")
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

    retrieval_query = message
    if ticket is not None:
        retrieval_query = (
            f"{pick(ticket, 'title', 'Title', '')} {pick(ticket, 'description', 'Description', '')}"
        ).strip() or message
    try:
        context = await rag.retrieve(retrieval_query)
    except Exception as exc:  # noqa: BLE001
        logger.warning("RAG retrieval failed, answering without context: %s", exc)
        context = []

    async def event_stream():
        meta = {"sources": len(context)}
        if ticket is not None:
            meta["ticketRef"] = pick(ticket, "referenceNumber", "ReferenceNumber", "")
            meta["comments"] = len(comments)
        yield {"event": "meta", "data": json.dumps(meta)}
        try:
            if ticket is not None:
                prompt = build_ticket_chat_prompt(
                    ticket, comments, message, req.history or [], context
                )
            else:
                prompt = rag.build_prompt(message, context)
            async for token in llm.generate(
                prompt, max_tokens=settings.max_tokens, temperature=settings.temperature
            ):
                yield {"event": "token", "data": json.dumps({"token": token})}
        except Exception as exc:  # noqa: BLE001
            yield {"event": "error", "data": json.dumps({"error": str(exc)})}
        finally:
            yield {"event": "done", "data": ""}

    return EventSourceResponse(event_stream())
