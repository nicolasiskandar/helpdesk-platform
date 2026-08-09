from app.consumers.dedup import DedupStore


def test_dedup_marks_and_checks(tmp_path):
    store = DedupStore(str(tmp_path / "dedup.db"))
    assert not store.is_processed("m1")
    store.mark_processed("m1")
    assert store.is_processed("m1")


def test_dedup_sweep_expires(tmp_path):
    store = DedupStore(str(tmp_path / "dedup.db"), ttl_seconds=0)
    store.mark_processed("m1")
    store.sweep()
    assert not store.is_processed("m1")
