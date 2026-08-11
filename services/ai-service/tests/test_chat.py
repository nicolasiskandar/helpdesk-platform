from fastapi.testclient import TestClient

from tests.conftest import FakeAreModelsReady, FakeLlm, FakeRag


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
