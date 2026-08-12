import httpx
from fastapi.testclient import TestClient

from app.api.routes.chat import TicketServiceError, build_ticket_chat_prompt
from tests.conftest import FakeAreModelsReady, FakeLlm, FakeRag

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


def _client(make_app, *, models_ready=True, rag_error=False, llm_error=False):
    return TestClient(
        make_app(
            rag=FakeRag(context=["ctx one"], error=rag_error),
            llm=FakeLlm(error=llm_error),
            are_models_ready=FakeAreModelsReady(models_ready),
        )
    )


def _auth_headers():
    return {"Authorization": "Bearer xyz"}


def test_health_ready_ok(make_app):
    client = TestClient(make_app())
    resp = client.get("/api/ai/health/ready")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}


def test_health_ready_503_while_downloading(make_app):
    client = _client(make_app, models_ready=False)
    resp = client.get("/api/ai/health/ready")
    assert resp.status_code == 503


def test_chat_requires_auth(make_app):
    client = TestClient(make_app(rag=FakeRag(), llm=FakeLlm()))
    resp = client.post("/api/ai/chat", json={"message": "hello"})
    assert resp.status_code == 401


def test_chat_rejects_empty_message(make_app):
    client = TestClient(make_app(rag=FakeRag(), llm=FakeLlm()))
    resp = client.post("/api/ai/chat", json={"message": "   "}, headers=_auth_headers())
    assert resp.status_code == 400


def test_chat_503_while_downloading(make_app):
    client = _client(make_app, models_ready=False)
    resp = client.post("/api/ai/chat", json={"message": "hello"}, headers=_auth_headers())
    assert resp.status_code == 503


def test_chat_streams_tokens(make_app):
    client = _client(make_app)
    resp = client.post("/api/ai/chat", json={"message": "hello"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert 'event: meta' in resp.text
    assert '"sources": 1' in resp.text
    assert 'event: token' in resp.text
    assert '"token": "hello"' in resp.text
    assert 'event: done' in resp.text


def test_chat_degrades_when_rag_fails(make_app):
    client = _client(make_app, rag_error=True)
    resp = client.post("/api/ai/chat", json={"message": "hello"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert '"sources": 0' in resp.text
    assert 'event: done' in resp.text


def test_chat_reports_llm_error_event(make_app):
    client = _client(make_app, llm_error=True)
    resp = client.post("/api/ai/chat", json={"message": "hello"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert 'event: error' in resp.text
    assert 'event: done' in resp.text


def test_build_ticket_chat_prompt_includes_thread_history_and_context():
    prompt = build_ticket_chat_prompt(
        TICKET,
        COMMENTS,
        "Was the charger replaced?",
        history=[
            {"role": "user", "content": "hi"},
            {"role": "assistant", "content": "hello"},
        ],
        context=["KB article about power adapters"],
    )
    assert "Laptop won't boot" in prompt
    assert "Screen stays black after BIOS." in prompt
    assert "Asked for the charger model" in prompt
    assert "Replaced the power adapter, still no boot." in prompt
    assert "KB article about power adapters" in prompt
    assert "hi" in prompt
    assert "hello" in prompt
    assert "Was the charger replaced?" in prompt
    assert prompt.index("Replaced the power adapter") < prompt.index("hi")


def test_chat_with_empty_ticket_id_rejected(make_app):
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "hello", "ticketId": "   "},
        headers=_auth_headers(),
    )
    assert resp.status_code == 400


def test_chat_with_ticket_streams_grounded_answer(make_app, monkeypatch):
    captured = {}

    async def fake_fetch(base_url, token, ticket_id):
        captured["token"] = token
        captured["ticket_id"] = ticket_id
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "Was the charger replaced?", "ticketId": "t1"},
        headers=_auth_headers(),
    )
    assert resp.status_code == 200
    assert captured["token"] == "Bearer xyz"
    assert captured["ticket_id"] == "t1"
    assert 'event: meta' in resp.text
    assert '"sources": 1' in resp.text
    assert '"ticketRef": "TKT-000001"' in resp.text
    assert '"comments": 2' in resp.text
    assert 'event: token' in resp.text
    assert 'event: done' in resp.text


def test_chat_with_ticket_forwards_403(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(403, "forbidden")

    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "hello", "ticketId": "t1"},
        headers=_auth_headers(),
    )
    assert resp.status_code == 403


def test_chat_with_ticket_forwards_404(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(404, "missing")

    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "hello", "ticketId": "missing"},
        headers=_auth_headers(),
    )
    assert resp.status_code == 404


def test_chat_with_ticket_502_when_ticket_service_rejects(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(500, "boom")

    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "hello", "ticketId": "t1"},
        headers=_auth_headers(),
    )
    assert resp.status_code == 502


def test_chat_with_ticket_502_when_ticket_service_unreachable(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise httpx.ConnectError("boom")

    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = _client(make_app)
    resp = client.post(
        "/api/ai/chat",
        json={"message": "hello", "ticketId": "t1"},
        headers=_auth_headers(),
    )
    assert resp.status_code == 502


class RecordingRag(FakeRag):
    def __init__(self):
        super().__init__(context=["ctx one"])
        self.queries = []

    async def retrieve(self, query: str) -> list[str]:
        self.queries.append(query)
        return await super().retrieve(query)


def test_chat_with_ticket_forwards_history_and_uses_ticket_rag_query(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        return TICKET, COMMENTS

    rag = RecordingRag()
    monkeypatch.setattr("app.api.routes.chat.fetch_ticket_thread", fake_fetch)
    client = TestClient(
        make_app(
            rag=rag,
            llm=FakeLlm(),
            are_models_ready=FakeAreModelsReady(True),
        )
    )
    resp = client.post(
        "/api/ai/chat",
        json={
            "message": "Was the charger replaced?",
            "ticketId": "t1",
            "history": [
                {"role": "user", "content": "hi"},
                {"role": "assistant", "content": "hello"},
            ],
        },
        headers=_auth_headers(),
    )
    assert resp.status_code == 200
    assert 'event: done' in resp.text
    assert rag.queries == ["Laptop won't boot Screen stays black after BIOS."]
