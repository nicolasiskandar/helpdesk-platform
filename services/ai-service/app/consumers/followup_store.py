import json
import sqlite3
import time
from pathlib import Path


class FollowUpStore:
    """Tracks tickets awaiting resolution confirmation (ARCHITECTURE §6.3).

    Persisted in the same SQLite file as the dedup store so at-least-once
    status events don't create duplicate follow-ups.
    """

    def __init__(self, db_path: str = "/data/dedup.db"):
        Path(db_path).parent.mkdir(parents=True, exist_ok=True)
        self._conn = sqlite3.connect(db_path)
        self._conn.execute(
            "CREATE TABLE IF NOT EXISTS followups ("
            " ticket_id TEXT PRIMARY KEY,"
            " reference_number TEXT NOT NULL,"
            " recipients TEXT NOT NULL,"
            " created_at REAL NOT NULL)"
        )
        self._conn.commit()

    def record(self, ticket_id: str, reference_number: str, recipients: list[str]) -> None:
        self._conn.execute(
            "INSERT OR REPLACE INTO followups (ticket_id, reference_number, recipients, created_at)"
            " VALUES (?, ?, ?, ?)",
            (ticket_id, reference_number, json.dumps(recipients), time.time()),
        )
        self._conn.commit()

    def is_pending_for_user(self, ticket_id: str, user_id: str) -> bool:
        row = self._conn.execute(
            "SELECT recipients FROM followups WHERE ticket_id = ?", (ticket_id,)
        ).fetchone()
        if not row:
            return False
        try:
            return user_id in json.loads(row[0])
        except (json.JSONDecodeError, TypeError):
            return False

    def list_for_user(self, user_id: str) -> list[dict]:
        rows = self._conn.execute("SELECT ticket_id, reference_number FROM followups").fetchall()
        return [
            {"ticketId": ticket_id, "referenceNumber": reference_number}
            for ticket_id, reference_number in rows
            if self.is_pending_for_user(ticket_id, user_id)
        ]

    def remove(self, ticket_id: str) -> None:
        self._conn.execute("DELETE FROM followups WHERE ticket_id = ?", (ticket_id,))
        self._conn.commit()

    def close(self) -> None:
        self._conn.close()
