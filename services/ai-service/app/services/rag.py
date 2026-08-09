from app.core.config import Settings
from app.services.embeddings import EmbeddingClient
from app.services.vector_store import VectorStore

SYSTEM_PROMPT = (
    "You are the Helpdesk AI assistant for an IT help desk. "
    "Answer using only the provided context. If the context does not contain the answer, "
    "say you don't know and suggest opening a ticket. Be concise and helpful."
)


class RagService:
    """Retrieves relevant indexed content and builds a grounded prompt for the chat model."""

    def __init__(self, settings: Settings, store: VectorStore, embeddings: EmbeddingClient):
        self._settings = settings
        self._store = store
        self._embeddings = embeddings

    async def retrieve(self, query: str, top_k: int | None = None) -> list[str]:
        vector = await self._embeddings.embed(query)
        hits = await self._store.search(vector, top_k or self._settings.top_k)
        return [hit.payload.get("text", "") for hit in hits if hit.payload]

    def build_prompt(self, query: str, context: list[str]) -> str:
        block = "\n\n---\n\n".join(f"[{i + 1}] {chunk}" for i, chunk in enumerate(context))
        return (
            f"{SYSTEM_PROMPT}\n\n"
            f"Context:\n{block}\n\n"
            f"User question: {query}\n\n"
            f"Answer:"
        )
