import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel

from app.api.dependencies import get_current_user
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])

CLOSED_STATUS_ID = 4


class ConfirmResolvedRequest(BaseModel):
    ticketId: str


@router.post("/confirm-resolved")
async def confirm_resolved(
    req: ConfirmResolvedRequest,
    request: Request,
    user: JwtClaims = Depends(get_current_user),  # noqa: B008
):
    settings = request.app.state.settings
    store = request.app.state.followups

    if not settings.ai_service_key:
        raise HTTPException(status_code=503, detail="AI service write access is not configured.")

    if not store.is_pending_for_user(req.ticketId, user.user_id):
        raise HTTPException(status_code=403, detail="No pending confirmation for this ticket.")

    headers = {
        "X-AI-Service-Key": settings.ai_service_key,
        "Content-Type": "application/json",
    }
    url = f"{settings.ticket_service_base_url}/api/tickets/{req.ticketId}/status"
    try:
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.patch(url, json={"statusId": CLOSED_STATUS_ID}, headers=headers)
    except httpx.HTTPError as exc:
        logger.warning("Failed to reach ticket service for ticket %s: %s", req.ticketId, exc)
        raise HTTPException(status_code=502, detail="Ticket service is unreachable.") from exc

    if resp.status_code != 200:
        logger.warning(
            "Ticket service rejected close for %s: %d %s",
            req.ticketId,
            resp.status_code,
            resp.text[:300],
        )
        raise HTTPException(status_code=502, detail="Ticket service rejected the close request.")

    store.remove(req.ticketId)
    return resp.json()
