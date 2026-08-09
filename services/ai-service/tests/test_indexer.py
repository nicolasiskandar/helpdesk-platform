from app.services.indexer import Indexer


def test_chunk_text_short():
    indexer = Indexer(None, None, chunk_size=800, chunk_overlap=80)  # type: ignore[arg-type]
    assert indexer.chunk_text("short text") == ["short text"]


def test_chunk_text_empty():
    indexer = Indexer(None, None, chunk_size=800, chunk_overlap=80)  # type: ignore[arg-type]
    assert indexer.chunk_text("  ") == []


def test_chunk_text_overlaps():
    indexer = Indexer(None, None, chunk_size=10, chunk_overlap=2)  # type: ignore[arg-type]
    chunks = indexer.chunk_text("abcdefghijklmnopqrstuvwxyz")
    assert all(len(c) <= 10 for c in chunks)
    assert chunks[0].endswith(chunks[1][:2])
    assert "".join(chunks) == "abcdefghijijklmnopqrqrstuvwxyz"
