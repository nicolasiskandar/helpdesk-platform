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
            for key in ("ticket.created", "ticket.resolved", "ticket.commented", "ticket.status_changed"):
                await queue.bind(exchange, key)
            logger.info(
                "Listening on queue %s (routing keys: ticket.created, ticket.resolved, "
                "ticket.commented, ticket.status_changed)",
                self._settings.index_queue,
            )

            async with queue.iterator() as queue_iter:
                async for message in queue_iter:
                    if stop.is_set():
                        await message.nack(requeue=True)
                        break
                    try:
                        async with message.process():
                            message_id = message.message_id or ""
                            if message_id and self._dedup.is_processed(message_id):
                                logger.info("Duplicate message %s, skipping", message_id)
                                continue
                            payload = json.loads(message.body.decode())
                            await self._handler(message.routing_key, payload)
                            if message_id:
                                self._dedup.mark_processed(message_id)
                    except Exception:
                        logger.exception("Failed to process message %s", message.message_id)
