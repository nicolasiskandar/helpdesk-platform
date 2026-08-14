import asyncio

from qdrant_client import AsyncQdrantClient
from qdrant_client.models import (
    Distance,
    FieldCondition,
    Filter,
    MatchValue,
    PointStruct,
    VectorParams,
)


class VectorStore:
    """Thin wrapper over Qdrant for the helpdesk content index."""

    def __init__(self, url: str, collection_name: str, vector_size: int):
        self._client = AsyncQdrantClient(url=url)
        self._collection = collection_name
        self._vector_size = vector_size

    async def ensure_collection(self, retries: int = 30, delay: float = 2.0) -> None:
        for attempt in range(retries):
            try:
                if not await self._client.collection_exists(self._collection):
                    await self._client.create_collection(
                        collection_name=self._collection,
                        vectors_config=VectorParams(size=self._vector_size, distance=Distance.COSINE),
                    )
                return
            except Exception:
                if attempt == retries - 1:
                    raise
                await asyncio.sleep(delay)

    async def upsert(self, points: list[PointStruct]) -> None:
        if points:
            await self._client.upsert(collection_name=self._collection, points=points)

    async def search(
        self,
        vector: list[float],
        top_k: int = 5,
        *,
        must_match: dict[str, str] | None = None,
    ) -> list:
        query_filter = None
        if must_match:
            conditions = [
                FieldCondition(key=key, match=MatchValue(value=value))
                for key, value in must_match.items()
            ]
            query_filter = Filter(must=conditions)
        resp = await self._client.query_points(
            collection_name=self._collection,
            query=vector,
            limit=top_k,
            query_filter=query_filter,
        )
        return resp.points

    def _filter(self, must_match: dict[str, str]) -> Filter:
        conditions = [
            FieldCondition(key=key, match=MatchValue(value=value))
            for key, value in must_match.items()
        ]
        return Filter(must=conditions)

    async def delete_by_filter(self, must_match: dict[str, str]) -> None:
        await self._client.delete(
            collection_name=self._collection,
            points_selector=self._filter(must_match),
        )

    async def set_payload(self, must_match: dict[str, str], payload: dict) -> None:
        await self._client.set_payload(
            collection_name=self._collection,
            payload=payload,
            points=self._filter(must_match),
        )
