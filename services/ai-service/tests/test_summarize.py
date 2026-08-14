import httpx
import pytest
from fastapi.testclient import TestClient

from app.api.routes.summarize import TicketServiceError, build_summary_prompt, fetch_ticket_thread
from tests.conftest import FakeAreModelsReady, FakeLlm

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


def _auth_headers():
    return {"Authorization": "Bearer xyz"}


class FakeResp:
    def __init__(self, status_code, json_data=None):
        self.status_code = status_code
        self.text = str(json_data)
        self._json = json_data

    def json(self):
        return self._json


class FakeGetClient:
    def __init__(self, ticket_resp, comments_resp):
        self._ticket_resp = ticket_resp
        self._comments_resp = comments_resp
        self.gets = []

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        return False

    async def get(self, url, headers=None):
        self.gets.append((url, headers))
        return self._comments_resp if url.endswith("/comments") else self._ticket_resp


def test_build_summary_prompt_includes_thread():
    prompt = build_summary_prompt(TICKET, COMMENTS)
    assert "Laptop won't boot" in prompt
    assert "Screen stays black after BIOS." in prompt
    assert "Asked for the charger model" in prompt
    assert "Replaced the power adapter, still no boot." in prompt
    assert "Summary:" in prompt
    assert prompt.index("Asked for the charger model") < prompt.index("Replaced the power adapter")


def test_build_summary_prompt_caps_comment_count_and_content():
    from app.services.ticket_client import MAX_CONTENT_CHARS

    many = [{"id": f"c{i}", "content": f"comment {i}"} for i in range(60)]
    prompt = build_summary_prompt(TICKET, many)
    assert "showing the last 30 of 60 comments" in prompt
    assert "comment 59" in prompt
    assert "comment 0" not in prompt

    huge = [{"id": "c1", "content": "x" * (MAX_CONTENT_CHARS + 500)}]
    prompt = build_summary_prompt(TICKET, huge)
    assert "[... truncated]" in prompt
    assert len(prompt) < MAX_CONTENT_CHARS * 3


async def test_fetch_ticket_thread_success(monkeypatch):
    fake_client = FakeGetClient(
        FakeResp(200, TICKET), FakeResp(200, COMMENTS)
    )
    monkeypatch.setattr("app.api.routes.summarize.httpx.AsyncClient", lambda timeout: fake_client)
    ticket, comments = await fetch_ticket_thread("http://ts:8080", "Bearer xyz", "t1")
    assert ticket["title"] == "Laptop won't boot"
    assert len(comments) == 2
    for url, headers in fake_client.gets:
        assert url.startswith("http://ts:8080/api/tickets/t1")
        assert headers == {"Authorization": "Bearer xyz"}


async def test_fetch_ticket_thread_forbidden(monkeypatch):
    fake_client = FakeGetClient(FakeResp(403, "forbidden"), FakeResp(403, "forbidden"))
    monkeypatch.setattr("app.api.routes.summarize.httpx.AsyncClient", lambda timeout: fake_client)
    with pytest.raises(TicketServiceError) as excinfo:
        await fetch_ticket_thread("http://ts:8080", "Bearer xyz", "t1")
    assert excinfo.value.status_code == 403


def test_summarize_requires_auth(make_app):
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"})
    assert resp.status_code == 401


def test_summarize_rejects_empty_ticket_id(make_app):
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "  "}, headers=_auth_headers())
    assert resp.status_code == 400


def test_summarize_503_while_downloading(make_app):
    client = TestClient(make_app(are_models_ready=FakeAreModelsReady(False)))
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 503


def test_summarize_502_when_ticket_service_unreachable(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise httpx.ConnectError("boom")

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 502


def test_summarize_forwards_ticket_service_403(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(403, "forbidden")

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 403


def test_summarize_forwards_ticket_service_404(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(404, "missing")

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "missing"}, headers=_auth_headers())
    assert resp.status_code == 404


def test_summarize_502_when_ticket_service_rejects(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        raise TicketServiceError(500, "boom")

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app())
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 502


def test_summarize_streams_summary_and_forwards_token(make_app, monkeypatch):
    captured = {}

    async def fake_fetch(base_url, token, ticket_id):
        captured["token"] = token
        captured["ticket_id"] = ticket_id
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app(llm=FakeLlm(tokens=("Summary:", " done."))))
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert captured["token"] == "Bearer xyz"
    assert captured["ticket_id"] == "t1"
    assert 'event: meta' in resp.text
    assert '"comments": 2' in resp.text
    assert 'event: token' in resp.text
    assert '"token": "Summary:"' in resp.text
    assert 'event: done' in resp.text


def test_summarize_reports_llm_error_event(make_app, monkeypatch):
    async def fake_fetch(base_url, token, ticket_id):
        return TICKET, COMMENTS

    monkeypatch.setattr("app.api.routes.summarize.fetch_ticket_thread", fake_fetch)
    client = TestClient(make_app(llm=FakeLlm(error=True)))
    resp = client.post("/api/ai/summarize", json={"ticketId": "t1"}, headers=_auth_headers())
    assert resp.status_code == 200
    assert 'event: error' in resp.text
    assert 'event: done' in resp.text
