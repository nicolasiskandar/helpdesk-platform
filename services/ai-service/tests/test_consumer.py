import asyncio
import json

from app.consumers.dedup import DedupStore
from app.consumers.index_consumer import IndexConsumer
from app.core.config import Settings


class FakeMessage:
    def __init__(self, message_id: str, routing_key: str, body: dict, *, redelivered: bool = False, headers: dict | None = None):
        self.message_id = message_id
        self.routing_key = routing_key
        self.body = json.dumps(body).encode()
        self.redelivered = redelivered
        self.headers = headers or {}
        self.acked = False
        self.rejected = False
        self.reject_requeue: bool | None = None

    async def ack(self):
        self.acked = True

    async def nack(self, requeue=True):
        self.rejected = True
        self.reject_requeue = requeue

    async def reject(self, requeue=False):
        self.rejected = True
        self.reject_requeue = requeue


class FakeIterator:
    def __init__(self, messages: list):
        self._messages = list(messages)

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        return False

    def __aiter__(self):
        return self

    async def __anext__(self):
        if not self._messages:
            raise StopAsyncIteration
        return self._messages.pop(0)


class FakeQueue:
    def __init__(self, messages: list):
        self._messages = messages
        self.bind_keys = []

    async def bind(self, exchange, key: str):
        self.bind_keys.append(key)

    def iterator(self):
        return FakeIterator(self._messages)


class FakeChannel:
    def __init__(self, messages: list):
        self.queue = FakeQueue(messages)
        self.qos = None

    async def set_qos(self, **kwargs):
        self.qos = kwargs

    async def declare_exchange(self, *args, **kwargs):
        return object()

    async def declare_queue(self, name, **kwargs):
        return self.queue


class FakeConnection:
    def __init__(self, messages: list):
        self._channel = FakeChannel(messages)

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        return False

    async def channel(self):
        return self._channel


def _run(monkeypatch, messages, dedup_db_path, handler):
    conn = FakeConnection(messages)

    async def fake_connect(url):
        return conn

    monkeypatch.setattr(
        "app.consumers.index_consumer.aio_pika.connect_robust",
        fake_connect,
    )
    settings = Settings(rabbitmq_url="amqp://guest:guest@rabbitmq:5672/", dedup_db_path=dedup_db_path)
    dedup = DedupStore(dedup_db_path)
    consumer = IndexConsumer(settings, dedup, handler)
    asyncio.run(consumer.run(asyncio.Event()))
    return conn


def test_consumer_binds_expected_keys(monkeypatch, tmp_path):
    calls = []
    conn = FakeConnection([])

    async def fake_connect(url):
        return conn

    monkeypatch.setattr("app.consumers.index_consumer.aio_pika.connect_robust", fake_connect)
    settings = Settings(dedup_db_path=str(tmp_path / "d.db"))
    consumer = IndexConsumer(settings, DedupStore(str(tmp_path / "d.db")), lambda k, p: calls.append(k))
    asyncio.run(consumer.run(asyncio.Event()))
    assert sorted(conn._channel.queue.bind_keys) == [
        "ticket.commented",
        "ticket.created",
        "ticket.deleted",
        "ticket.resolved",
        "ticket.status_changed",
    ]


def test_consumer_delivers_and_dedups(monkeypatch, tmp_path):
    payload = {"TicketId": "t1", "Title": "VPN", "Description": "down"}
    calls = []

    async def handler(routing_key, body):
        calls.append((routing_key, body))

    _run(
        monkeypatch,
        [
            FakeMessage("m1", "ticket.created", payload),
            FakeMessage("m1", "ticket.created", payload),
        ],
        str(tmp_path / "d.db"),
        handler,
    )
    assert len(calls) == 1
    assert calls[0] == ("ticket.created", payload)


def test_consumer_skips_duplicate_after_restart(monkeypatch, tmp_path):
    payload = {"TicketId": "t2", "Title": "X", "Description": "y"}
    calls = []

    async def handler(routing_key, body):
        calls.append((routing_key, body))

    _run(monkeypatch, [FakeMessage("m2", "ticket.resolved", payload)], str(tmp_path / "d.db"), handler)
    _run(monkeypatch, [FakeMessage("m2", "ticket.resolved", payload)], str(tmp_path / "d.db"), handler)
    assert len(calls) == 1


def test_consumer_acknowledges_duplicate_skip(monkeypatch, tmp_path):
    payload = {"TicketId": "t3", "Title": "X", "Description": "y"}
    calls = []

    async def handler(routing_key, body):
        calls.append((routing_key, body))

    msg1 = FakeMessage("dup", "ticket.created", payload)
    msg2 = FakeMessage("dup", "ticket.created", payload)
    _run(monkeypatch, [msg1, msg2], str(tmp_path / "d.db"), handler)
    assert len(calls) == 1
    assert msg1.acked is True
    assert msg2.acked is True


def test_consumer_requeues_failure_below_cap(monkeypatch, tmp_path):
    payload = {"TicketId": "t4", "Title": "X", "Description": "y"}

    async def handler(routing_key, body):
        raise ValueError("boom")

    msg = FakeMessage("m4", "ticket.created", payload, redelivered=True, headers={"x-death": [{"count": 1}]})
    _run(monkeypatch, [msg], str(tmp_path / "d.db"), handler)
    assert msg.rejected is True
    assert msg.reject_requeue is True


def test_consumer_drops_poison_message_at_cap(monkeypatch, tmp_path):
    payload = {"TicketId": "t5", "Title": "X", "Description": "y"}

    async def handler(routing_key, body):
        raise ValueError("boom")

    msg = FakeMessage("m5", "ticket.created", payload, redelivered=True, headers={"x-death": [{"count": 5}]})
    _run(monkeypatch, [msg], str(tmp_path / "d.db"), handler)
    assert msg.rejected is True
    assert msg.reject_requeue is False
