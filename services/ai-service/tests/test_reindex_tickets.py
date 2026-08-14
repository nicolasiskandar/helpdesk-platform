import httpx
from fastapi.testclient import TestClient

from app.api.routes.reindex import index_status_tag
from tests.conftest import FakeJwtValidator


class FakeIndexer:
    def __init__(self):
        self.wiped = False
        self.indexed: list[tuple[dict, str]] = []

    async def wipe_tickets(self) -> None:
        self.wiped = True

    async def index_ticket(self, payload: dict, *, status: str = "open") -> int:
        self.indexed.append((payload, status))
        return 1


def test_reindex_tickets_requires_auth(make_app):
    client = TestClient(make_app(indexer=FakeIndexer()))
    resp = client.post("/api/ai/reindex-tickets")
    assert resp.status_code == 401


def test_reindex_tickets_admin_only(make_app, monkeypatch):
    async def no_tickets(base_url, token):
        return []

    monkeypatch.setattr("app.api.routes.reindex.fetch_all_tickets", no_tickets)
    client = TestClient(make_app(indexer=FakeIndexer(), jwt_validator=FakeJwtValidator(role="Employee")))
    resp = client.post("/api/ai/reindex-tickets", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 403


def test_reindex_tickets_502_when_ticket_service_unreachable(make_app, monkeypatch):
    async def boom(base_url, token):
        raise httpx.ConnectError("nope")

    monkeypatch.setattr("app.api.routes.reindex.fetch_all_tickets", boom)
    client = TestClient(make_app(indexer=FakeIndexer(), jwt_validator=FakeJwtValidator(role="Admin")))
    resp = client.post("/api/ai/reindex-tickets", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 502


def test_reindex_tickets_success(make_app, monkeypatch):
    async def some_tickets(base_url, token):
        return [
            {"id": "t1", "title": "Printer jammed in finance", "description": "d1",
             "referenceNumber": "TKT-000001", "categoryName": "Hardware", "priorityName": "High",
             "statusName": "Closed"},
            {"id": "t2", "title": "Laptop won't boot", "description": "d2",
             "referenceNumber": "TKT-000002", "categoryName": "Hardware", "priorityName": "Medium",
             "statusName": "Resolved - Pending Confirmation"},
            {"id": "t3", "title": "VPN drops", "description": "d3",
             "referenceNumber": "TKT-000003", "categoryName": "Network", "priorityName": "Low",
             "statusName": "In Progress"},
            {"id": None, "title": "orphan", "description": "d4",
             "referenceNumber": "TKT-000004", "categoryName": "Network", "priorityName": "Low",
             "statusName": "Open"},
        ]

    monkeypatch.setattr("app.api.routes.reindex.fetch_all_tickets", some_tickets)
    indexer = FakeIndexer()
    client = TestClient(make_app(indexer=indexer, jwt_validator=FakeJwtValidator(role="Admin")))
    resp = client.post("/api/ai/reindex-tickets", headers={"Authorization": "Bearer xyz"})
    assert resp.status_code == 200
    assert resp.json() == {"indexed": 3}
    assert indexer.wiped is True
    statuses = {payload["ticketId"]: status for payload, status in indexer.indexed}
    assert statuses == {"t1": "closed", "t2": "resolved", "t3": "in progress"}


def test_index_status_tag():
    assert index_status_tag("Closed") == "closed"
    assert index_status_tag("Resolved - Pending Confirmation") == "resolved"
    assert index_status_tag("In Progress") == "in progress"
    assert index_status_tag("Open") == "open"
    assert index_status_tag("") == ""
    assert index_status_tag(None) == ""
