import httpx


class EmbeddingClient:
    """Produces vector embeddings via an Ollama-compatible /api/embeddings endpoint."""

    def __init__(self, base_url: str, model: str):
        self._base_url = base_url.rstrip("/")
        self._model = model

    async def embed(self, text: str) -> list[float]:
        url = f"{self._base_url}/api/embeddings"
        timeout = httpx.Timeout(connect=15.0, read=600.0, write=30.0, pool=15.0)
        async with httpx.AsyncClient(timeout=timeout) as client:
            response = await client.post(
                url, json={"model": self._model, "prompt": text, "keep_alive": 3600}
            )
            response.raise_for_status()
            return response.json()["embedding"]

    async def embed_many(self, texts: list[str]) -> list[list[float]]:
        return [await self.embed(text) for text in texts]

    async def warmup(self) -> None:
        """Loads the embedding model into memory so the first request is fast."""
        await self.embed("warmup")
