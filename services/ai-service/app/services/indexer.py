import logging
import uuid

from qdrant_client.models import PointStruct

from app.services.embeddings import EmbeddingClient
from app.services.vector_store import VectorStore

logger = logging.getLogger(__name__)


def pick(payload: dict, *keys: str):
    """Returns the first present key in ``payload`` (case-insensitive).

    The ticket service publishes outbox events serialized with default
    System.Text.Json settings (PascalCase property names), while the AI
    service consumes them as dicts. Normalize so both forms work.
    """
    normalized = {k.lower(): v for k, v in payload.items()}
    for key in keys:
        value = normalized.get(key.lower())
        if value is not None:
            return value
    return None


def resolved_index_status(payload: dict) -> str:
    """Maps a ``ticket.resolved`` event to its vector-store status tag.

    Only tickets that reached the terminal ``Closed`` status are treated as
    closed; ``Resolved - Pending Confirmation`` tickets may still be reopened,
    so they are tagged ``resolved`` (kept in the index but excluded from
    similar-ticket recommendations).
    """
    name = str(pick(payload, "resolvedStatusName", "ResolvedStatusName") or "")
    return "closed" if name == "Closed" else "resolved"


class Indexer:
    """Chunks and embeds ticket / comment / KB content into the vector store."""

    def __init__(
        self,
        store: VectorStore,
        embeddings: EmbeddingClient,
        chunk_size: int = 800,
        chunk_overlap: int = 80,
    ):
        self._store = store
        self._embeddings = embeddings
        self._chunk_size = chunk_size
        self._chunk_overlap = chunk_overlap

    def chunk_text(self, text: str) -> list[str]:
        text = (text or "").strip()
        if not text:
            return []
        if len(text) <= self._chunk_size:
            return [text]
        chunks: list[str] = []
        start = 0
        while start < len(text):
            end = min(start + self._chunk_size, len(text))
            chunks.append(text[start:end])
            if end == len(text):
                break
            start = end - self._chunk_overlap
        return chunks

    def _points(
        self,
        doc_type: str,
        doc_id: str,
        chunks: list[str],
        vectors: list[list[float]],
        payload: dict,
    ) -> list[PointStruct]:
        return [
            PointStruct(
                id=str(
                    uuid.uuid5(uuid.NAMESPACE_URL, f"{doc_type}:{doc_id}:{i}")
                ),
                vector=vectors[i],
                payload={**payload, "text": chunks[i]},
            )
            for i in range(len(chunks))
        ]

    async def index_ticket(self, payload: dict, *, status: str = "open") -> int:
        ticket_id = str(pick(payload, "ticketId", "TicketId"))
        title = pick(payload, "title", "Title") or ""
        description = pick(payload, "description", "Description") or ""
        text = f"{title}\n{description}".strip()
        chunks = self.chunk_text(text)
        if not chunks:
            return 0
        vectors = await self._embeddings.embed_many(chunks)
        points = self._points(
            "ticket",
            ticket_id,
            chunks,
            vectors,
            {
                "doc_type": "ticket",
                "doc_id": ticket_id,
                "reference_number": pick(payload, "referenceNumber", "ReferenceNumber"),
                "title": title,
                "category": pick(payload, "categoryName", "CategoryName"),
                "priority": pick(payload, "priorityName", "PriorityName"),
                "status": status,
            },
        )
        await self._store.upsert(points)
        logger.info("Indexed ticket %s (%d chunks)", ticket_id, len(chunks))
        return len(chunks)

    async def index_comment(self, payload: dict) -> int:
        if pick(payload, "isPrivate", "IsPrivate"):
            return 0
        comment_id = str(pick(payload, "commentId", "CommentId"))
        content = pick(payload, "content", "Content") or ""
        chunks = self.chunk_text(content)
        if not chunks:
            return 0
        vectors = await self._embeddings.embed_many(chunks)
        points = self._points(
            "comment",
            comment_id,
            chunks,
            vectors,
            {
                "doc_type": "comment",
                "doc_id": comment_id,
                "ticket_id": str(pick(payload, "ticketId", "TicketId")),
                "reference_number": pick(payload, "referenceNumber", "ReferenceNumber"),
                "author_name": pick(payload, "authorName", "AuthorName"),
            },
        )
        await self._store.upsert(points)
        logger.info("Indexed comment %s (%d chunks)", comment_id, len(chunks))
        return len(chunks)

    async def delete_ticket(self, ticket_id: str) -> None:
        await self._store.delete_by_filter({"doc_type": "ticket", "doc_id": ticket_id})
        await self._store.delete_by_filter({"doc_type": "comment", "ticket_id": ticket_id})
        logger.info("Deleted vectors for ticket %s", ticket_id)

    async def set_ticket_status(self, ticket_id: str, status: str) -> None:
        await self._store.set_payload({"doc_type": "ticket", "doc_id": ticket_id}, {"status": status})
        logger.info("Set status=%s for ticket %s", status, ticket_id)

    async def wipe_kb(self) -> None:
        await self._store.delete_by_filter({"doc_type": "kb"})
        logger.info("Wiped KB vectors")

    async def index_kb_articles(self, articles: list[dict]) -> int:
        total = 0
        for article in articles:
            article_id = str(article.get("id"))
            text = f"{article.get('title', '')}\n{article.get('body', '')}".strip()
            chunks = self.chunk_text(text)
            if not chunks:
                continue
            vectors = await self._embeddings.embed_many(chunks)
            points = self._points(
                "kb",
                article_id,
                chunks,
                vectors,
                {
                    "doc_type": "kb",
                    "doc_id": article_id,
                    "title": article.get("title"),
                    "category": article.get("category"),
                },
            )
            await self._store.upsert(points)
            total += len(chunks)
        logger.info("Indexed %d KB articles (%d chunks)", len(articles), total)
        return total
