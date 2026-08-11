import pytest
from fastapi import FastAPI

from app.api.routes import analyze, chat, followup, reindex, similar, summarize, troubleshooting
from app.core.config import Settings
from app.core.jwt import JwtClaims


class FakeJwtValidator:
    def __init__(self, role: str = "Employee", user_id: str = "u-123"):
        self._role = role
        self._user_id = user_id

    def decode(self, token: str) -> JwtClaims:
        return JwtClaims(
            user_id=self._user_id,
            email="test@helpdesk.local",
            role=self._role,
            name="Test User",
        )


class FakeAreModelsReady:
    def __init__(self, ready: bool = True):
        self._ready = ready

    async def __call__(self, settings: Settings):
        return (self._ready, [] if self._ready else ["llama3.2:3b"])


@pytest.fixture
def make_app():
    def _make(**state) -> FastAPI:
        app = FastAPI()
        app.include_router(chat.router)
        app.include_router(analyze.router)
        app.include_router(similar.router)
        app.include_router(reindex.router)
        app.include_router(followup.router)
        app.include_router(summarize.router)
        app.include_router(troubleshooting.router)
        app.state.settings = state.pop("settings", Settings(ai_service_key="test-key"))
        app.state.jwt_validator = state.pop("jwt_validator", FakeJwtValidator())
        app.state.are_models_ready = state.pop("are_models_ready", FakeAreModelsReady())
        app.state.llm = state.pop("llm", FakeLlm())
        for key, value in state.items():
            setattr(app.state, key, value)
        return app

    return _make


class FakeRag:
    def __init__(self, context: list | None = None, error: bool = False):
        self._context = context if context is not None else []
        self._error = error

    async def retrieve(self, query: str) -> list[str]:
        if self._error:
            raise RuntimeError("vector store unavailable")
        return self._context

    def build_prompt(self, query: str, context: list[str]) -> str:
        return f"prompt for {query} with {len(context)} sources"


class FakeSimilarity:
    def __init__(self, results: list | None = None, error: bool = False):
        self._results = results if results is not None else []
        self._error = error

    async def find_similar(self, query: str, **kwargs) -> list[dict]:
        if self._error:
            raise RuntimeError("vector store unavailable")
        return self._results


class FakeLlm:
    def __init__(self, tokens: tuple = ("hello", " world"), error: bool = False):
        self._tokens = tokens
        self._error = error

    async def generate(self, prompt: str, **kwargs):
        if self._error:
            raise RuntimeError("llm boom")
        for token in self._tokens:
            yield token
