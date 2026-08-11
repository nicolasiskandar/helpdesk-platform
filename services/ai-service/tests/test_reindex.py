import httpx
from fastapi.testclient import TestClient

from tests.conftest import FakeJwtValidator


class FakeIndexer:
    def __init__(self, indexed: int = 7):
        self._indexed = indexed

    async def index_kb_articles(self, articles: list[dict]) -> int:
        return self._indexed


def test_reindex_requires_auth(make_app):
    client = TestClient(make_app(indexer=FakeIndexer()))
    resp = client.post("/api/ai/reindex")
    assert resp.status_code == 401


def test_reindex_admin_only(make_app, monkeypatch):
    async def no_articles(base_url, token):
        return []

    monkeypatch.setattr("app.api.routes.reindex.fetch_published_kb", no_articles)
    client = TestClient(make_app(indexer=FakeIndexer(), jwt_validator=FakeJwtValidator(role="Employee")))
    resp = client.post("/api/ai/reindex", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 403


def test_reindex_502_when_kb_unreachable(make_app, monkeypatch):
    async def boom(base_url, token):
        raise httpx.ConnectError("nope")

    monkeypatch.setattr("app.api.routes.reindex.fetch_published_kb", boom)
    client = TestClient(make_app(indexer=FakeIndexer(), jwt_validator=FakeJwtValidator(role="Admin")))
    resp = client.post("/api/ai/reindex", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 502


def test_reindex_success(make_app, monkeypatch):
    async def one_article(base_url, token):
        return [{"id": "k1"}]

    monkeypatch.setattr("app.api.routes.reindex.fetch_published_kb", one_article)
    client = TestClient(make_app(indexer=FakeIndexer(indexed=9), jwt_validator=FakeJwtValidator(role="Admin")))
    resp = client.post("/api/ai/reindex", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 200
    assert resp.json() == {"indexed": 9}
