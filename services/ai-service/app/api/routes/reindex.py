import logging

import httpx
from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel

from app.api.dependencies import get_current_user
from app.core.config import Settings
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])


class ReindexResponse(BaseModel):
    indexed: int


async def fetch_published_kb(base_url: str, token: str) -> list[dict]:
    articles: list[dict] = []
    page = 1
    page_size = 500
    headers = {"Authorization": token}
    async with httpx.AsyncClient(timeout=30) as client:
        while True:
            resp = await client.get(
                f"{base_url}/api/kb-articles",
                params={"page": page, "pageSize": page_size},
                headers=headers,
            )
            resp.raise_for_status()
            data = resp.json()
            items = data.get("articles", [])
            articles.extend(item for item in items if item.get("status") == "published")
            total = data.get("totalCount", 0)
            if not items or page * page_size >= total:
                break
            page += 1
    return articles


@router.post("/reindex", response_model=ReindexResponse)
async def reindex(request: Request, user: JwtClaims = Depends(get_current_user)):  # noqa: B008
    if user.role != "Admin":
        raise HTTPException(status_code=403, detail="Admin only.")
    settings: Settings = request.app.state.settings
    indexer = request.app.state.indexer
    token = request.headers.get("Authorization", "")
    try:
        articles = await fetch_published_kb(settings.ticket_service_base_url, token)
    except httpx.HTTPError as exc:
        logger.exception("Failed to fetch KB articles")
        raise HTTPException(status_code=502, detail=f"Failed to fetch KB articles: {exc}") from exc
    # Wipe stale KB vectors first: edits/unpublishes would otherwise leave
    # orphaned points that surface in RAG retrieval forever.
    await indexer.wipe_kb()
    indexed = await indexer.index_kb_articles(articles)
    return ReindexResponse(indexed=indexed)