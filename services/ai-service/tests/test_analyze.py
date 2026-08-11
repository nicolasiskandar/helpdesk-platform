import pytest
from fastapi.testclient import TestClient

from app.core.config import Settings
from app.services.classifier import (
    CATEGORY_IDS,
    DEFAULT_CATEGORY,
    DEFAULT_PRIORITY,
    PRIORITY_IDS,
    Classifier,
)


class FakeLlmComplete:
    def __init__(self, result: str):
        self._result = result
        self.calls = 0

    async def complete(self, prompt: str, **kwargs) -> str:
        self.calls += 1
        return self._result


@pytest.mark.asyncio
async def test_rules_override_critical_priority():
    classifier = Classifier(Settings())
    result = await classifier.classify("VPN is down for the entire office", "no one can connect")
    assert result["priority"] == "Critical"
    assert result["priorityId"] == PRIORITY_IDS["Critical"]
    assert result["method"] == "rules"


@pytest.mark.asyncio
async def test_category_rules_win():
    classifier = Classifier(Settings())
    result = await classifier.classify("Can't access the shared drive", "permission denied")
    assert result["category"] == "Access"
    assert result["method"] == "rules"


@pytest.mark.asyncio
async def test_ambiguous_uses_llm():
    classifier = Classifier(Settings())
    llm = FakeLlmComplete('{"category": "Software", "priority": "High"}')
    result = await classifier.classify("Something odd happens", "weird behavior", llm=llm)
    assert llm.calls == 1
    assert result["category"] == "Software"
    assert result["priority"] == "High"
    assert result["method"] == "llm"


@pytest.mark.asyncio
async def test_ambiguous_defaults_without_llm():
    classifier = Classifier(Settings())
    result = await classifier.classify("Something odd happens", "weird behavior")
    assert result["category"] == DEFAULT_CATEGORY
    assert result["priority"] == DEFAULT_PRIORITY
    assert result["categoryId"] == CATEGORY_IDS[DEFAULT_CATEGORY]


@pytest.mark.asyncio
async def test_llm_failure_falls_back_to_defaults():
    classifier = Classifier(Settings())

    class BrokenLlm:
        async def complete(self, prompt, **kwargs):
            raise RuntimeError("model down")

    result = await classifier.classify("Something odd happens", "weird behavior", llm=BrokenLlm())
    assert result["category"] == DEFAULT_CATEGORY
    assert result["priority"] == DEFAULT_PRIORITY


@pytest.mark.asyncio
async def test_invalid_llm_json_ignored():
    classifier = Classifier(Settings())
    llm = FakeLlmComplete("sure, here you go: no json at all")
    result = await classifier.classify("Something odd happens", "weird behavior", llm=llm)
    assert result["category"] == DEFAULT_CATEGORY


def test_analyze_requires_auth(make_app):
    client = TestClient(make_app(classifier=Classifier(Settings())))
    resp = client.post("/api/ai/analyze", json={"title": "t", "description": "d"})
    assert resp.status_code == 401


def test_analyze_rejects_empty_title(make_app):
    client = TestClient(make_app(classifier=Classifier(Settings())))
    resp = client.post(
        "/api/ai/analyze",
        json={"title": "   ", "description": "d"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 400


def test_analyze_returns_classification(make_app):
    client = TestClient(make_app(classifier=Classifier(Settings())))
    resp = client.post(
        "/api/ai/analyze",
        json={"title": "server is down for everyone", "description": ""},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 200
    body = resp.json()
    assert body["priority"] == "Critical"
    assert body["priorityId"] == 4
