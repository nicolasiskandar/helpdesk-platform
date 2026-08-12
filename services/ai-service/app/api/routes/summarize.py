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
    render_ticket_thread,
)

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


def build_summary_prompt(ticket: dict, comments: list[dict]) -> str:
    block = render_ticket_thread(ticket, comments)
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
