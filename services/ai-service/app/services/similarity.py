import logging

from app.core.config import Settings
from app.services.embeddings import EmbeddingClient
from app.services.vector_store import VectorStore

logger = logging.getLogger(__name__)


class SimilarityService:
    """Finds previously closed tickets semantically similar to a query."""

    def __init__(self, settings: Settings, store: VectorStore, embeddings: EmbeddingClient):
        self._settings = settings
        self._store = store
        self._embeddings = embeddings

    async def find_similar(
        self,
        query: str,
        *,
        exclude_ticket_id: str | None = None,
        limit: int = 5,
    ) -> list[dict]:
        vector = await self._embeddings.embed(query)
        hits = await self._store.search(
            vector,
            top_k=self._settings.similar_scan,
            must_match={"doc_type": "ticket", "status": "closed"},
        )

        grouped: dict[str, tuple[float, dict]] = {}
        for hit in hits:
            payload = hit.payload or {}
            doc_id = str(payload.get("doc_id", ""))
            if not doc_id or (exclude_ticket_id and doc_id == exclude_ticket_id):
                continue
            score = hit.score or 0.0
            if doc_id not in grouped or score > grouped[doc_id][0]:
                grouped[doc_id] = (score, payload)

        results = []
        for doc_id, (score, payload) in grouped.items():
            results.append(
                {
                    "ticketId": doc_id,
                    "referenceNumber": payload.get("reference_number"),
                    "title": payload.get("title"),
                    "excerpt": (payload.get("text") or "")[:300],
                    "category": payload.get("category"),
                    "priority": payload.get("priority"),
                    "status": payload.get("status"),
                    "score": round(score, 4),
                }
            )
        results.sort(key=lambda r: r["score"], reverse=True)
        return results[:limit]
