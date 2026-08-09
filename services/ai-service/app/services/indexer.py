import logging

from qdrant_client.models import PointStruct

from app.services.embeddings import EmbeddingClient
from app.services.vector_store import VectorStore

logger = logging.getLogger(__name__)


class Indexer:
    """Chunks and embeds ticket / KB content into the vector store."""

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
                id=f"{doc_type}:{doc_id}:{i}",
                vector=vectors[i],
                payload={**payload, "text": chunks[i]},
            )
            for i in range(len(chunks))
        ]

    async def index_ticket(self, payload: dict) -> int:
        ticket_id = str(payload.get("ticketId"))
        title = payload.get("title", "")
        description = payload.get("description", "")
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
                "reference_number": payload.get("referenceNumber"),
                "category": payload.get("categoryName"),
                "priority": payload.get("priorityName"),
            },
        )
        await self._store.upsert(points)
        logger.info("Indexed ticket %s (%d chunks)", ticket_id, len(chunks))
        return len(chunks)

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
