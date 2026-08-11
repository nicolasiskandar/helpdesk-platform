from fastapi.testclient import TestClient


class FakeSimilarity:
    def __init__(self, result=None, error=False):
        self._result = result if result is not None else []
        self._error = error
        self.last_query = None

    async def find_similar(self, query, *, exclude_ticket_id=None, limit=5):
        self.last_query = query
        if self._error:
            raise RuntimeError("store unavailable")
        return self._result


def test_similar_requires_auth(make_app):
    client = TestClient(make_app(similarity=FakeSimilarity()))
    resp = client.post("/api/ai/similar-tickets", json={"query": "vpn"})
    assert resp.status_code == 401


def test_similar_rejects_empty_query(make_app):
    client = TestClient(make_app(similarity=FakeSimilarity()))
    resp = client.post(
        "/api/ai/similar-tickets",
        json={"query": "   "},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 400


def test_similar_returns_grouped_tickets(make_app):
    result = [
        {
            "ticketId": "t1",
            "referenceNumber": "TKT-000001",
            "title": "VPN down",
            "excerpt": "VPN down for everyone",
            "category": "Network",
            "priority": "Critical",
            "status": "closed",
            "score": 0.91,
        }
    ]
    sim = FakeSimilarity(result=result)
    client = TestClient(make_app(similarity=sim))
    resp = client.post(
        "/api/ai/similar-tickets",
        json={"query": "can't connect to vpn", "excludeTicketId": "t9"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 200
    assert resp.json() == result
    assert sim.last_query == "can't connect to vpn"


def test_similar_returns_503_on_error(make_app):
    client = TestClient(make_app(similarity=FakeSimilarity(error=True)))
    resp = client.post(
        "/api/ai/similar-tickets",
        json={"query": "vpn"},
        headers={"Authorization": "Bearer xyz"},
    )
    assert resp.status_code == 503
