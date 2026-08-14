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


def _ticket_payload(ticket: dict) -> dict:
    """Maps a ticket-service TicketResponse (camelCase or PascalCase) to an indexer payload."""
    return {
        "ticketId": ticket.get("id") or ticket.get("Id"),
        "referenceNumber": ticket.get("referenceNumber") or ticket.get("ReferenceNumber"),
        "title": ticket.get("title") or ticket.get("Title"),
        "description": ticket.get("description") or ticket.get("Description"),
        "categoryName": ticket.get("categoryName") or ticket.get("CategoryName"),
        "priorityName": ticket.get("priorityName") or ticket.get("PriorityName"),
        "statusName": ticket.get("statusName") or ticket.get("StatusName"),
    }


def index_status_tag(status_name: str) -> str:
    """Maps a ticket status name to its vector-store tag (matches Indexer indexing rules)."""
    name = (status_name or "").strip()
    if name == "Closed":
        return "closed"
    if name == "Resolved - Pending Confirmation":
        return "resolved"
    return name.lower()


async def fetch_all_tickets(base_url: str, token: str) -> list[dict]:
    tickets: list[dict] = []
    page = 1
    page_size = 500
    headers = {"Authorization": token}
    async with httpx.AsyncClient(timeout=30) as client:
        while True:
            resp = await client.get(
                f"{base_url}/api/tickets",
                params={"page": page, "pageSize": page_size},
                headers=headers,
            )
            resp.raise_for_status()
            data = resp.json()
            items = data.get("tickets", [])
            tickets.extend(items)
            total = data.get("totalCount", 0)
            if not items or page * page_size >= total:
                break
            page += 1
    return tickets


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


@router.post("/reindex-tickets", response_model=ReindexResponse)
async def reindex_tickets(request: Request, user: JwtClaims = Depends(get_current_user)):  # noqa: B008
    if user.role != "Admin":
        raise HTTPException(status_code=403, detail="Admin only.")
    settings: Settings = request.app.state.settings
    indexer = request.app.state.indexer
    token = request.headers.get("Authorization", "")
    try:
        tickets = await fetch_all_tickets(settings.ticket_service_base_url, token)
    except httpx.HTTPError as exc:
        logger.exception("Failed to fetch tickets")
        raise HTTPException(status_code=502, detail=f"Failed to fetch tickets: {exc}") from exc
    # Wipe stale ticket vectors first: status tags drift (e.g. a ticket closed
    # then reopened) and deleted tickets would otherwise linger in similar-ticket
    # results forever. Comments/KB vectors are left untouched.
    await indexer.wipe_tickets()
    indexed = 0
    for ticket in tickets:
        payload = _ticket_payload(ticket)
        if not payload["ticketId"]:
            continue
        await indexer.index_ticket(payload, status=index_status_tag(payload["statusName"]))
        indexed += 1
    logger.info("Reindexed %d tickets", indexed)
    return ReindexResponse(indexed=indexed)