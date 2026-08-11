import asyncio
import logging
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import analyze, chat, followup, reindex, similar, summarize
from app.consumers.dedup import DedupStore
from app.consumers.followup_store import FollowUpStore
from app.consumers.index_consumer import IndexConsumer
from app.core.config import Settings, get_settings
from app.core.jwt import JwtValidator
from app.services.classifier import Classifier
from app.services.embeddings import EmbeddingClient
from app.services.indexer import Indexer, pick, resolved_index_status
from app.services.llm import LlmClient
from app.services.rag import RagService
from app.services.similarity import SimilarityService
from app.services.vector_store import VectorStore

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s [%(name)s] %(message)s")
logger = logging.getLogger(__name__)


def normalize_model_name(name: str) -> str:
    """Ollama reports a model without an explicit tag as 'name:latest'."""
    return name if ":" in name else f"{name}:latest"


async def are_models_ready(settings: Settings) -> tuple[bool, list[str]]:
    """Returns (ready, missing_models) by comparing Ollama's tags against the configured models."""
    async with httpx.AsyncClient(timeout=5) as client:
        try:
            resp = await client.get(f"{settings.ollama_url}/api/tags")
        except httpx.HTTPError:
            return False, ["ollama unreachable"]
    if resp.status_code != 200:
        return False, [f"ollama http {resp.status_code}"]
    present = {m["name"] for m in resp.json().get("models", [])}
    missing = [m for m in (settings.chat_model, settings.embedding_model) if normalize_model_name(m) not in present]
    return not missing, missing


def build_services(settings: Settings):
    embeddings = EmbeddingClient(settings.ollama_url, settings.embedding_model)
    store = VectorStore(settings.qdrant_url, settings.collection_name, settings.vector_size)
    llm = LlmClient(settings.ollama_url, settings.chat_model)
    rag = RagService(settings, store, embeddings)
    similarity = SimilarityService(settings, store, embeddings)
    classifier = Classifier(settings)
    indexer = Indexer(store, embeddings, settings.chunk_size, settings.chunk_overlap)
    jwt_validator = JwtValidator(settings.jwt_public_key_path, settings.jwt_audience)
    dedup = DedupStore(settings.dedup_db_path, settings.dedup_ttl_seconds)
    followups = FollowUpStore(settings.dedup_db_path)
    return store, llm, rag, similarity, classifier, indexer, jwt_validator, dedup, followups, embeddings


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    store, llm, rag, similarity, classifier, indexer, jwt_validator, dedup, followups, embeddings = (
        build_services(settings)
    )
    await store.ensure_collection()

    async def on_event(routing_key: str, payload: dict) -> None:
        if routing_key == "ticket.created":
            await indexer.index_ticket(payload, status="open")
        elif routing_key == "ticket.resolved":
            await indexer.index_ticket(payload, status=resolved_index_status(payload))
        elif routing_key == "ticket.commented":
            await indexer.index_comment(payload)
        elif (
            routing_key == "ticket.status_changed"
            and pick(payload, "newStatus", "NewStatus") == "Resolved - Pending Confirmation"
        ):
            followups.record(
                str(pick(payload, "ticketId", "TicketId")),
                str(pick(payload, "referenceNumber", "ReferenceNumber") or ""),
                [str(u) for u in (pick(payload, "recipientUserIds", "RecipientUserIds") or [])],
            )

    consumer = IndexConsumer(settings, dedup, on_event)
    consumer_task = asyncio.create_task(consumer.run(asyncio.Event()))

    app.state.settings = settings
    app.state.llm = llm
    app.state.rag = rag
    app.state.similarity = similarity
    app.state.classifier = classifier
    app.state.indexer = indexer
    app.state.jwt_validator = jwt_validator
    app.state.dedup = dedup
    app.state.followups = followups
    app.state.are_models_ready = are_models_ready

    async def warm_up_model() -> None:
        try:
            for attempt in range(1, 61):
                ready, missing = await are_models_ready(settings)
                if ready:
                    break
                await asyncio.sleep(5 * attempt)
            else:
                logger.warning("Model warm-up skipped: still missing %s after 10 minutes", missing)
                return
            await llm.warmup()
            await embeddings.warmup()
            logger.info("Model warm-up complete (%s, %s loaded)", settings.chat_model, settings.embedding_model)
        except Exception as exc:  # noqa: BLE001
            logger.warning("Model warm-up failed: %s", exc)

    warmup_task = asyncio.create_task(warm_up_model())

    logger.info(
        "AI service started (chat=%s, embeddings=%s, queue=%s)",
        settings.chat_model,
        settings.embedding_model,
        settings.index_queue,
    )
    yield
    warmup_task.cancel()
    consumer_task.cancel()
    try:
        await consumer_task
    except asyncio.CancelledError:
        pass
    dedup.close()
    followups.close()


def create_app() -> FastAPI:
    app = FastAPI(title="Helpdesk AI Service", lifespan=lifespan)
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=False,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    app.include_router(chat.router)
    app.include_router(reindex.router)
    app.include_router(similar.router)
    app.include_router(analyze.router)
    app.include_router(followup.router)
    app.include_router(summarize.router)

    @app.get("/health")
    async def health():
        return {"status": "ok"}

    @app.get("/health/ready")
    async def ready(request: Request):
        settings: Settings = request.app.state.settings
        checks = {}
        async with httpx.AsyncClient(timeout=5) as client:
            for name, url in (("qdrant", f"{settings.qdrant_url}/healthz"), ("ollama", f"{settings.ollama_url}/api/tags")):
                try:
                    resp = await client.get(url)
                    checks[name] = resp.status_code
                except Exception as exc:  # noqa: BLE001
                    checks[name] = f"error: {exc}"
        models_ok, missing = await are_models_ready(settings)
        checks["models"] = "missing: " + ", ".join(missing) if missing else "ok"
        healthy = checks.get("qdrant") == 200 and checks.get("ollama") == 200 and models_ok
        if healthy:
            return {"status": "ok", "checks": checks}
        raise HTTPException(status_code=503, detail=checks)

    return app


app = create_app()
