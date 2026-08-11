import pytest

from app.services.llm import LlmClient


class FakeStream:
    def __init__(self, lines: list[str]):
        self._lines = lines

    def raise_for_status(self):
        pass

    async def aiter_lines(self):
        for line in self._lines:
            yield line


class FakeStreamContext:
    def __init__(self, lines: list[str]):
        self.response = FakeStream(lines)

    async def __aenter__(self):
        return self.response

    async def __aexit__(self, *args):
        return False


class FakeJsonResponse:
    def __init__(self, data: dict):
        self._data = data

    def raise_for_status(self):
        pass

    def json(self):
        return self._data


class FakeClient:
    def __init__(self, stream_lines=None, json_data=None, timeout=None):
        self._stream_lines = stream_lines
        self._json_data = json_data
        self.request_kwargs = None

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        return False

    def stream(self, method, url, json=None):
        self.request_kwargs = (method, url, json)
        return FakeStreamContext(self._stream_lines or [])

    async def post(self, url, json=None):
        self.request_kwargs = ("POST", url, json)
        return FakeJsonResponse(self._json_data or {})


@pytest.mark.asyncio
async def test_generate_streams_tokens(monkeypatch):
    lines = [
        '{"response": "hello", "done": false}',
        '{"response": " world", "done": true}',
    ]
    monkeypatch.setattr(
        "app.services.llm.httpx.AsyncClient",
        lambda timeout: FakeClient(stream_lines=lines),
    )
    llm = LlmClient("http://ollama:11434", "llama3.2:3b")
    tokens = [token async for token in llm.generate("prompt")]
    assert tokens == ["hello", " world"]


@pytest.mark.asyncio
async def test_generate_skips_malformed_lines(monkeypatch):
    lines = [
        "not-json",
        '{"response": "ok", "done": true}',
    ]
    monkeypatch.setattr(
        "app.services.llm.httpx.AsyncClient",
        lambda timeout: FakeClient(stream_lines=lines),
    )
    llm = LlmClient("http://ollama:11434", "llama3.2:3b")
    tokens = [token async for token in llm.generate("prompt")]
    assert tokens == ["ok"]


@pytest.mark.asyncio
async def test_complete_returns_text(monkeypatch):
    monkeypatch.setattr(
        "app.services.llm.httpx.AsyncClient",
        lambda timeout: FakeClient(json_data={"response": "Software"}),
    )
    llm = LlmClient("http://ollama:11434", "llama3.2:3b")
    result = await llm.complete("classify")
    assert result == "Software"
