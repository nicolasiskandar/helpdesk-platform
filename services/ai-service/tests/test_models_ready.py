import pytest

from app.core.config import Settings
from app.main import are_models_ready, normalize_model_name


class FakeResponse:
    def __init__(self, status_code, json_data):
        self.status_code = status_code
        self._json = json_data

    def json(self):
        return self._json


@pytest.mark.asyncio
async def test_are_models_ready_without_latest_suffix(monkeypatch):
    class FakeClient:
        async def __aenter__(self):
            return self

        async def __aexit__(self, *args):
            return False

        async def get(self, url):
            return FakeResponse(
                200,
                {"models": [{"name": "llama3.2:3b"}, {"name": "nomic-embed-text:latest"}]},
            )

    monkeypatch.setattr("app.main.httpx.AsyncClient", lambda timeout: FakeClient())
    settings = Settings(chat_model="llama3.2:3b", embedding_model="nomic-embed-text")
    ready, missing = await are_models_ready(settings)
    assert ready is True
    assert missing == []


@pytest.mark.asyncio
async def test_are_models_ready_reports_missing(monkeypatch):
    class FakeClient:
        async def __aenter__(self):
            return self

        async def __aexit__(self, *args):
            return False

        async def get(self, url):
            return FakeResponse(200, {"models": [{"name": "llama3.2:3b"}]})

    monkeypatch.setattr("app.main.httpx.AsyncClient", lambda timeout: FakeClient())
    settings = Settings(chat_model="llama3.2:3b", embedding_model="nomic-embed-text")
    ready, missing = await are_models_ready(settings)
    assert ready is False
    assert missing == ["nomic-embed-text"]


def test_normalize_model_name():
    assert normalize_model_name("nomic-embed-text") == "nomic-embed-text:latest"
    assert normalize_model_name("llama3.2:3b") == "llama3.2:3b"
