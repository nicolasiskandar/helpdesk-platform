import asyncio
import json
import logging
from collections.abc import Awaitable, Callable

import aio_pika

from app.consumers.dedup import DedupStore
from app.core.config import Settings

logger = logging.getLogger(__name__)

EventHandler = Callable[[str, dict], Awaitable[None]]


class IndexConsumer:
    """Subscribes to ticket events and feeds them to the indexer with MessageId dedup."""

    def __init__(self, settings: Settings, dedup: DedupStore, handler: EventHandler):
        self._settings = settings
        self._dedup = dedup
        self._handler = handler

    async def run(self, stop: asyncio.Event) -> None:
        connection = await aio_pika.connect_robust(self._settings.rabbitmq_url)
        async with connection:
            channel = await connection.channel()
            await channel.set_qos(prefetch_count=10)
            exchange = await channel.declare_exchange(
                self._settings.rabbitmq_exchange, aio_pika.ExchangeType.TOPIC, durable=True
            )
            queue = await channel.declare_queue(self._settings.index_queue, durable=True)
            for key in (
                "ticket.created",
                "ticket.resolved",
                "ticket.commented",
                "ticket.status_changed",
                "ticket.deleted",
            ):
                await queue.bind(exchange, key)
            logger.info(
                "Listening on queue %s (routing keys: ticket.created, ticket.resolved, "
                "ticket.commented, ticket.status_changed, ticket.deleted)",
                self._settings.index_queue,
            )

            async def sweep_loop() -> None:
                # TTL sweep: prune seen MessageIds so the dedup table doesn't
                # grow unbounded (AGENTS.md documents the table "with TTL").
                while not stop.is_set():
                    try:
                        self._dedup.sweep()
                    except Exception:
                        logger.exception("Dedup TTL sweep failed")
                    await asyncio.sleep(3600)

            sweep_task = asyncio.create_task(sweep_loop())
            try:
                async with queue.iterator() as queue_iter:
                    async for message in queue_iter:
                        if stop.is_set():
                            await message.nack(requeue=True)
                            break
                        message_id = message.message_id or ""
                        redeliveries = _redelivery_count(message)
                        try:
                            if message_id and self._dedup.is_processed(message_id):
                                logger.info("Duplicate message %s, skipping", message_id)
                                await message.ack()
                                continue
                            payload = json.loads(message.body.decode())
                            await self._handler(message.routing_key, payload)
                            if message_id:
                                self._dedup.mark_processed(message_id)
                            await message.ack()
                        except Exception:
                            logger.exception(
                                "Failed to process message %s (redelivery %d/%d)",
                                message_id,
                                redeliveries + 1,
                                self._settings.max_redeliveries,
                            )
                            if redeliveries >= self._settings.max_redeliveries:
                                # Poison message: stop requeueing so one bad payload
                                # can't stall the queue behind every other message.
                                logger.warning(
                                    "Poison message %s dropped after %d redeliveries",
                                    message_id,
                                    redeliveries,
                                )
                                await message.reject(requeue=False)
                            else:
                                await message.nack(requeue=True)
            finally:
                sweep_task.cancel()


def _redelivery_count(message: aio_pika.IncomingMessage) -> int:
    if not message.redelivered or not message.headers:
        return 0
    x_death = message.headers.get("x-death")
    if not isinstance(x_death, list):
        return 0
    total = 0
    for entry in x_death:
        if isinstance(entry, dict):
            count = entry.get("count")
            if isinstance(count, int):
                total += count
    return total
