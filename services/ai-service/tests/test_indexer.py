from app.services.indexer import Indexer, pick, resolved_index_status


def test_resolved_index_status_maps_closed_and_pending():
    assert (
        resolved_index_status({"ResolvedStatusName": "Closed"})
        == "closed"
    )
    assert (
        resolved_index_status({"ResolvedStatusName": "Resolved - Pending Confirmation"})
        == "resolved"
    )
    assert resolved_index_status({"resolvedStatusName": "Closed"}) == "closed"
    assert resolved_index_status({}) == "resolved"


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


def test_pick_is_case_insensitive():
    assert pick({"TicketId": "t1"}, "ticketId", "TicketId") == "t1"
    assert pick({"ticketId": "t1"}, "ticketId", "TicketId") == "t1"
    assert pick({"Title": "x"}, "title", "Title") == "x"
    assert pick({}, "ticketId", "TicketId") is None
    assert pick({"ReferenceNumber": "TKT-1"}, "referenceNumber", "ReferenceNumber") == "TKT-1"


class FakeEmbeddings:
    async def embed_many(self, texts):
        return [[0.1] * 3 for _ in texts]


class FakeStore:
    def __init__(self):
        self.points = []

    async def upsert(self, points):
        self.points.extend(points)


async def test_index_ticket_reads_pascal_case_payload():
    store = FakeStore()
    indexer = Indexer(store, FakeEmbeddings(), chunk_size=800, chunk_overlap=80)
    n = await indexer.index_ticket(
        {
            "TicketId": "t1",
            "Title": "VPN down",
            "Description": "Everyone affected",
            "ReferenceNumber": "TKT-1",
            "CategoryName": "Network",
            "PriorityName": "High",
        },
        status="resolved",
    )
    assert n == 1
    payload = store.points[0].payload
    assert payload["doc_type"] == "ticket"
    assert payload["doc_id"] == "t1"
    assert payload["status"] == "resolved"
    assert payload["reference_number"] == "TKT-1"
    assert "VPN down" in payload["text"]


async def test_point_ids_are_valid_uuids_and_deterministic():
    store = FakeStore()
    indexer = Indexer(store, FakeEmbeddings(), chunk_size=800, chunk_overlap=80)
    await indexer.index_ticket(
        {"TicketId": "t1", "Title": "VPN down", "Description": "Everyone affected"},
        status="resolved",
    )
    first_ids = [str(p.id) for p in store.points]
    assert all(len(p_id) == 36 and "-" in p_id for p_id in first_ids)

    store.points = []
    await indexer.index_ticket(
        {"TicketId": "t1", "Title": "VPN down", "Description": "Everyone affected"},
        status="resolved",
    )
    second_ids = [str(p.id) for p in store.points]
    assert second_ids == first_ids


async def test_index_comment_skips_private():
    store = FakeStore()
    indexer = Indexer(store, FakeEmbeddings())
    n = await indexer.index_comment(
        {"CommentId": "c1", "Content": "secret internal note", "IsPrivate": True}
    )
    assert n == 0
    assert store.points == []


async def test_index_comment_public():
    store = FakeStore()
    indexer = Indexer(store, FakeEmbeddings())
    n = await indexer.index_comment(
        {
            "CommentId": "c1",
            "Content": "Fixed by rebooting the router",
            "IsPrivate": False,
            "TicketId": "t1",
            "ReferenceNumber": "TKT-1",
            "AuthorName": "Jane",
        }
    )
    assert n == 1
    payload = store.points[0].payload
    assert payload["doc_type"] == "comment"
    assert payload["doc_id"] == "c1"
    assert payload["ticket_id"] == "t1"
    assert payload["author_name"] == "Jane"
