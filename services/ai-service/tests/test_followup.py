from fastapi.testclient import TestClient

from app.consumers.followup_store import FollowUpStore


def test_followup_store_roundtrip(tmp_path):
    store = FollowUpStore(str(tmp_path / "data.db"))
    store.record("t1", "TKT-000001", ["u1", "u2"])
    assert store.is_pending_for_user("t1", "u1")
    assert store.is_pending_for_user("t1", "u2")
    assert not store.is_pending_for_user("t1", "u3")
    assert store.list_for_user("u2") == [{"ticketId": "t1", "referenceNumber": "TKT-000001"}]
    store.remove("t1")
    assert not store.is_pending_for_user("t1", "u1")


def test_followup_store_replace(tmp_path):
    store = FollowUpStore(str(tmp_path / "data.db"))
    store.record("t1", "TKT-000001", ["u1"])
    store.record("t1", "TKT-000001", ["u2"])
    assert not store.is_pending_for_user("t1", "u1")
    assert store.is_pending_for_user("t1", "u2")


class FakeStore:
    def __init__(self, pending=False):
        self._pending = pending
        self.removed = None

    def is_pending_for_user(self, ticket_id, user_id):
        return self._pending

    def remove(self, ticket_id):
        self.removed = ticket_id


class FakeTicketResponse:
    def __init__(self, status_code, text="", json_data=None):
        self.status_code = status_code
        self.text = text
        self._json = json_data

    def json(self):
        return self._json


class FakeHttpxClient:
    def __init__(self, response):
        self._response = response
        self.patch_kwargs = None

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        return False

    async def patch(self, url, json=None, headers=None):
        self.patch_kwargs = (url, json, headers)
        return self._response


def test_confirm_requires_auth(make_app):
    client = TestClient(make_app(followups=FakeStore()))
    resp = client.post("/api/ai/confirm-resolved", json={"ticketId": "t1"})
    assert resp.status_code == 401


def test_confirm_403_when_not_pending(make_app):
    client = TestClient(make_app(followups=FakeStore(pending=False)))
    resp = client.post(
        "/api/ai/confirm-resolved",
        json={"ticketId": "t1"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 403


def test_confirm_503_when_key_missing(make_app):
    from app.core.config import Settings

    client = TestClient(
        make_app(settings=Settings(ai_service_key=""), followups=FakeStore(pending=True))
    )
    resp = client.post(
        "/api/ai/confirm-resolved",
        json={"ticketId": "t1"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 503


def test_confirm_502_when_ticket_service_rejects(make_app, monkeypatch):
    monkeypatch.setattr(
        "app.api.routes.followup.httpx.AsyncClient",
        lambda timeout: FakeHttpxClient(FakeTicketResponse(403, "forbidden")),
    )
    client = TestClient(make_app(followups=FakeStore(pending=True)))
    resp = client.post(
        "/api/ai/confirm-resolved",
        json={"ticketId": "t1"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 502


def test_confirm_success_calls_scoped_close(make_app, monkeypatch):
    fake_client = FakeHttpxClient(
        FakeTicketResponse(200, json_data={"id": "t1", "statusName": "Closed"})
    )
    monkeypatch.setattr(
        "app.api.routes.followup.httpx.AsyncClient",
        lambda timeout: fake_client,
    )
    store = FakeStore(pending=True)
    client = TestClient(make_app(followups=store))
    resp = client.post(
        "/api/ai/confirm-resolved",
        json={"ticketId": "t1"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 200
    url, payload, headers = fake_client.patch_kwargs
    assert url.endswith("/api/tickets/t1/status")
    assert payload == {"statusId": 4}
    assert headers["X-AI-Service-Key"] == "test-key"
    assert store.removed == "t1"
