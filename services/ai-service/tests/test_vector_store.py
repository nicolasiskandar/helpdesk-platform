import pytest
from qdrant_client.models import Filter

from app.services.vector_store import VectorStore


class FakeResponse:
    def __init__(self, points=None):
        self.points = points or []


class FakeQdrant:
    def __init__(self, url=""):
        self.upsert_kwargs = None
        self.search_kwargs = None
        self.created = False

    async def collection_exists(self, collection_name):
        return True

    async def create_collection(self, **kwargs):
        self.created = True

    async def upsert(self, **kwargs):
        self.upsert_kwargs = kwargs

    async def query_points(self, **kwargs):
        self.search_kwargs = kwargs
        return FakeResponse()


@pytest.fixture
def store(monkeypatch):
    fake = FakeQdrant()
    monkeypatch.setattr("app.services.vector_store.AsyncQdrantClient", lambda url: fake)
    return VectorStore("http://qdrant:6333", "helpdesk_index", 768), fake


async def test_search_passes_filter(store):
    vs, fake = store
    await vs.search(
        [0.1] * 768,
        top_k=5,
        must_match={"doc_type": "ticket", "status": "resolved"},
    )
    query_filter = fake.search_kwargs["query_filter"]
    assert isinstance(query_filter, Filter)
    assert len(query_filter.must) == 2
    keys = {c.key for c in query_filter.must}
    assert keys == {"doc_type", "status"}


async def test_search_without_filter(store):
    vs, fake = store
    await vs.search([0.1] * 768, top_k=3)
    assert fake.search_kwargs["query_filter"] is None
    assert fake.search_kwargs["limit"] == 3


async def test_upsert_skips_empty(store):
    vs, fake = store
    await vs.upsert([])
    assert fake.upsert_kwargs is None
