import sqlite3
import time
from pathlib import Path


class DedupStore:
    """Persists seen outbox MessageIds so at-least-once deliveries are idempotent."""

    def __init__(self, db_path: str = "/data/dedup.db", ttl_seconds: int = 604800):
        self._ttl = ttl_seconds
        Path(db_path).parent.mkdir(parents=True, exist_ok=True)
        self._conn = sqlite3.connect(db_path)
        self._conn.execute(
            "CREATE TABLE IF NOT EXISTS processed (message_id TEXT PRIMARY KEY, processed_at REAL)"
        )
        self._conn.commit()

    def is_processed(self, message_id: str) -> bool:
        return (
            self._conn.execute("SELECT 1 FROM processed WHERE message_id = ?", (message_id,)).fetchone()
            is not None
        )

    def mark_processed(self, message_id: str) -> None:
        self._conn.execute(
            "INSERT OR REPLACE INTO processed (message_id, processed_at) VALUES (?, ?)",
            (message_id, time.time()),
        )
        self._conn.commit()

    def sweep(self) -> None:
        cutoff = time.time() - self._ttl
        self._conn.execute("DELETE FROM processed WHERE processed_at < ?", (cutoff,))
        self._conn.commit()

    def close(self) -> None:
        self._conn.close()
