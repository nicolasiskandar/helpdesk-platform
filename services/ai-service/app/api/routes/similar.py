import logging

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel

from app.api.dependencies import get_current_user
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])


class SimilarTicketsRequest(BaseModel):
    query: str
    excludeTicketId: str | None = None


@router.post("/similar-tickets")
async def similar_tickets(
    req: SimilarTicketsRequest,
    request: Request,
    _: JwtClaims = Depends(get_current_user),  # noqa: B008
):
    query = req.query.strip()
    if not query:
        raise HTTPException(status_code=400, detail="Query cannot be empty.")

    similarity = request.app.state.similarity
    try:
        return await similarity.find_similar(query, exclude_ticket_id=req.excludeTicketId)
    except Exception as exc:
        logger.warning("Similarity search failed: %s", exc)
        raise HTTPException(
            status_code=503,
            detail="Similar-ticket search is currently unavailable.",
        ) from exc
