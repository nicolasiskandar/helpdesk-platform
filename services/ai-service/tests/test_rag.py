from app.core.config import Settings
from app.services.rag import RagService


def test_build_prompt_includes_context():
    rag = RagService(Settings(), None, None)  # type: ignore[arg-type]
    prompt = rag.build_prompt("How do I reset my password?", ["Reset via portal.", "See KB doc."])
    assert "How do I reset my password?" in prompt
    assert "[1] Reset via portal." in prompt
    assert "[2] See KB doc." in prompt
    assert "suggest opening a ticket" in prompt
