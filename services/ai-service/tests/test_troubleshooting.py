import httpx
from fastapi.testclient import TestClient

from app.api.routes.troubleshooting import build_troubleshooting_prompt
from app.services.ticket_client import TicketServiceError
from tests.conftest import FakeAreModelsReady, FakeLlm, FakeRag, FakeSimilarity

TICKET = {
    "id": "t1",
    "referenceNumber": "TKT-000001",
    "title": "Laptop won't boot",
    "description": "Screen stays black after BIOS.",
    "categoryName": "Hardware",
    "priorityName": "High",
    "statusName": "In Progress",
}

COMMENTS = [
    {"id": "c1", "content": "Asked for the charger model."},
    {"id": "c2", "content": "Replaced the power adapter, still no boot."},
]

CONTEXT = ["Knowledge base: reseat the RAM module and battery."]
SIMILAR = [
    {
        "ticketId": "t9",
        "referenceNumber": "TKT-000009",
        "title": "Black screen after BIOS",
        "excerpt": "Reseating memory fixed the black screen.",
        "status": "closed",
    }
]


def _client(make_app, *, models_ready=True, rag_error=False, similar_error=False, llm_error=False):
    return TestClient(
        make_app(
            rag=FakeRag(context=CONTEXT, error=rag_error),
            similarity=FakeSimilarity(results=SIMILAR, error=similar_error),
            llm=FakeLlm(error=llm_error),
            are_models_ready=FakeAreModelsReady(models_ready),
        )
    )


def _auth_headers():
    return {"Authorization": "Bearer xyz"}


def test_build_troubleshooting_prompt_includes_thread_and_context():
    prompt = build_troubleshooting_prompt(TICKET, COMMENTS, CONTEXT, SIMILAR)
    assert "Laptop won't boot" in prompt
    assert "Screen stays black after BIOS." in prompt
    assert "Asked for the charger model" in prompt
    assert "Replaced the power adapter, still no boot." in prompt
    assert "Knowledge base: reseat the RAM module and battery." in prompt
    assert "TKT-000009" in prompt
    assert "Black screen after BIOS" in prompt
    assert "Troubleshooting steps:" in prompt
    assert prompt.index("Asked for the charger model") < prompt.index("Replaced the power adapter")


def test_troubleshooting_requires_auth(make_app):
    client = TestClient(make_app(rag=FakeRag(), similarity=FakeSimilarity(), llm=FakeLlm()))
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"})
    assert resp.status_code == 401


def test_troubleshooting_rejects_empty_ticket_id(make_app):
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "  "}, headers=_auth_headers())
    assert resp.status_code == 400


def test_troubleshooting_503_while_downloading(make_app):
    client = _client(make_app, models_ready=False)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 503


def test_troubleshooting_502_when_ticket_service_unreachable(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise httpx.ConnectError("boom")

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 502


def test_troubleshooting_forwards_ticket_service_403(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(403, "forbidden")

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 403


def test_troubleshooting_forwards_ticket_service_404(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(404, "missing")

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "missing"}, headers=_auth_headers())
    assert resp.status_code == 404


def test_troubleshooting_502_when_ticket_service_rejects(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(500, "boom")

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 502


def test_troubleshooting_streams_and_forwards_metadata(make_app, monkeypatch):
    captured = {}

    async def fake_fetch(base_url, token, ticket_id):
        captured["token"] = token
        captured["ticket_id"] = ticket_id
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert captured["token"] == "Bearer xyz"
    assert captured["ticket_id"] == "t1"
    assert 'event: meta' in resp.text
    assert '"sources": 1' in resp.text
    assert '"similarTickets": 1' in resp.text
    assert 'event: token' in resp.text
    assert '"token": "hello"' in resp.text
    assert 'event: done' in resp.text


def test_troubleshooting_degrades_when_rag_fails(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app, rag_error=True, similar_error=True)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert '"sources": 0' in resp.text
    assert '"similarTickets": 0' in resp.text
    assert 'event: done' in resp.text


def test_troubleshooting_reports_llm_error_event(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.troubleshooting.fetch_ticket_thread", fake_fetch)
    client = _client(make_app, llm_error=True)
    resp = client.post("/api/ai/troubleshooting", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert 'event: error' in resp.text
    assert 'event: done' in resp.text
