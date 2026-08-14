"""CLI runner for the AI service eval harness.

Measures the three headline metrics with the REAL production code paths
(Classifier / LlmClient / EmbeddingClient / VectorStore / Indexer), in-process,
no HTTP/JWT/auth involved:

  [X] Retrieval accuracy  -> recall@top_k over the KB corpus (default top_k=5)
  [Y] Classification acc. -> exact match of predicted category AND priority
  [Z] Chat latency        -> p50 time-to-first-token (ms) via LlmClient.generate

Usage (run from services/ai-service/ with the venv activated):

  python -m eval.run_eval --mode classify --no-llm     # offline, no stack needed
  python -m eval.run_eval --mode all                   # full run (Ollama + Qdrant up)
  python -m eval.run_eval --mode all --cleanup         # ...and drop the eval collection

Environment:
  OLLAMA_URL            default http://localhost:11434 (host access to the stack)
  QDRANT_URL            default http://localhost:6333
  EVAL_CHAT_MODEL       default settings.chat_model (llama3.2:3b)
  EVAL_EMBED_MODEL      default settings.embedding_model (nomic-embed-text)

The retrieval eval uses its OWN Qdrant collection "helpdesk_eval" — the
production "helpdesk_index" collection is never touched.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import os
import statistics
import time
from pathlib import Path

from qdrant_client import AsyncQdrantClient

from app.core.config import Settings
from app.services.classifier import Classifier
from app.services.embeddings import EmbeddingClient
from app.services.indexer import Indexer
from app.services.llm import LlmClient
from app.services.vector_store import VectorStore

logging.basicConfig(
    level=logging.INFO,
    format="%(levelname)s %(name)s: %(message)s",
)
for name in ("httpx", "httpcore", "qdrant_client", "urllib3"):
    logging.getLogger(name).setLevel(logging.WARNING)

EVAL_DIR = Path(__file__).resolve().parent
DATA_DIR = EVAL_DIR / "data"
EVAL_COLLECTION = "helpdesk_eval"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--mode",
        choices=["all", "classify", "retrieve", "latency"],
        default="all",
        help="which evals to run (default: all)",
    )
    parser.add_argument(
        "--no-llm",
        action="store_true",
        help="run without the chat model (offline; skips latency)",
    )
    parser.add_argument(
        "--repeat",
        type=int,
        default=20,
        help="number of chat requests for the latency eval (default: 20)",
    )
    parser.add_argument(
        "--top-k",
        type=int,
        default=5,
        help="top-k hits for retrieval recall@k (default: 5)",
    )
    parser.add_argument(
        "--cleanup",
        action="store_true",
        help="delete the helpdesk_eval collection after the retrieval eval",
    )
    return parser.parse_args()


def make_settings() -> Settings:
    settings = Settings()
    settings.ollama_url = os.environ.get("OLLAMA_URL", "http://localhost:11434")
    settings.qdrant_url = os.environ.get("QDRANT_URL", "http://localhost:6333")
    settings.chat_model = os.environ.get("EVAL_CHAT_MODEL", settings.chat_model)
    settings.embedding_model = os.environ.get("EVAL_EMBED_MODEL", settings.embedding_model)
    return settings


def load_jsonl(path: Path) -> list[dict]:
    records: list[dict] = []
    with path.open(encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                records.append(json.loads(line))
    return records


async def run_classify(settings: Settings, llm: LlmClient | None) -> dict:
    logger = logging.getLogger("eval.classify")
    records = load_jsonl(DATA_DIR / "classification_eval.jsonl")
    classifier = Classifier(settings)

    correct = 0
    by_method: dict[str, dict] = {}
    misses: list[dict] = []
    for record in records:
        result = await classifier.classify(
            record["title"], record["description"], llm=llm
        )
        ok = result["category"] == record["category"] and result["priority"] == record["priority"]
        if ok:
            correct += 1
        else:
            misses.append(
                {
                    "id": record["id"],
                    "expected": f"{record['category']}/{record['priority']}",
                    "predicted": f"{result['category']}/{result['priority']}",
                    "method": result["method"],
                    "note": record.get("note", ""),
                }
            )
        method = result["method"]
        entry = by_method.setdefault(method, {"total": 0, "correct": 0})
        entry["total"] += 1
        entry["correct"] += int(ok)

    total = len(records)
    accuracy = correct / total * 100.0
    logger.info(
        "classification: %d/%d exact match (%.1f%%)", correct, total, accuracy
    )
    for method, entry in sorted(by_method.items()):
        logger.info(
            "  %s: %d/%d correct (%.1f%%)",
            method,
            entry["correct"],
            entry["total"],
            entry["correct"] / entry["total"] * 100.0 if entry["total"] else 0.0,
        )
    for miss in misses:
        logger.warning(
            "  %s expected %-14s predicted %-14s [%s] %s",
            miss["id"],
            miss["expected"],
            miss["predicted"],
            miss["method"],
            miss["note"],
        )
    return {"accuracy_pct": round(accuracy, 2), "correct": correct, "total": total}


async def run_retrieve(settings: Settings, top_k: int, cleanup: bool) -> dict:
    logger = logging.getLogger("eval.retrieve")
    corpus = load_jsonl(DATA_DIR / "corpus.jsonl")
    queries = load_jsonl(DATA_DIR / "retrieval_eval.jsonl")

    store = VectorStore(settings.qdrant_url, EVAL_COLLECTION, settings.vector_size)
    await store.ensure_collection()
    embeddings = EmbeddingClient(settings.ollama_url, settings.embedding_model)
    indexer = Indexer(store, embeddings)
    await indexer.wipe_kb()
    await indexer.index_kb_articles(corpus)

    hits: list[dict] = []
    for query in queries:
        vector = await embeddings.embed(query["query"])
        results = await store.search(vector, top_k=top_k)
        doc_ids = [point.payload.get("doc_id") for point in results]
        found = [expected for expected in query["expected_doc_ids"] if expected in doc_ids]
        hits.append(
            {
                "id": query["id"],
                "expected": query["expected_doc_ids"],
                "found": found,
                "top_k": doc_ids,
            }
        )

    correct = sum(1 for hit in hits if hit["found"])
    total = len(hits)
    recall = correct / total * 100.0

    precision_at_k = [
        len(hit["found"]) / len(hit["top_k"]) if hit["top_k"] else 0.0 for hit in hits
    ]
    reciprocal_ranks = []
    for hit in hits:
        for rank, doc_id in enumerate(hit["top_k"], start=1):
            if doc_id in hit["expected"]:
                reciprocal_ranks.append(1.0 / rank)
                break
        else:
            reciprocal_ranks.append(0.0)
    mean_precision = sum(precision_at_k) / total * 100.0
    mrr = sum(reciprocal_ranks) / total

    logger.info(
        "retrieval: %d/%d queries with a relevant doc in top-%d (recall %.1f%%, "
        "precision@%d %.1f%%, MRR %.3f)",
        correct, total, top_k, recall, top_k, mean_precision, mrr,
    )
    for hit in hits:
        if not hit["found"]:
            logger.warning(
                "  %s expected %s top-k=%s", hit["id"], hit["expected"], hit["top_k"]
            )

    if cleanup:
        client = AsyncQdrantClient(url=settings.qdrant_url)
        await client.delete_collection(collection_name=EVAL_COLLECTION)
        await client.close()
        logger.info("dropped collection %s", EVAL_COLLECTION)
    else:
        await store.delete_by_filter({"doc_type": "kb"})

    return {
        "recall_pct": round(recall, 2),
        "precision_pct": round(mean_precision, 2),
        "mrr": round(mrr, 3),
        "correct": correct,
        "total": total,
    }


async def run_latency(settings: Settings, repeat: int) -> dict:
    logger = logging.getLogger("eval.latency")
    llm = LlmClient(settings.ollama_url, settings.chat_model)
    await llm.warmup()

    prompt = (
        "You are a helpdesk assistant. Answer concisely: "
        "How do I reset my corporate email password?"
    )
    ttft_ms: list[float] = []
    total_ms: list[float] = []
    for _ in range(repeat):
        started = time.perf_counter()
        first = True
        async for _token in llm.generate(prompt, max_tokens=64, temperature=0.0):
            if first:
                ttft_ms.append((time.perf_counter() - started) * 1000.0)
                first = False
        total_ms.append((time.perf_counter() - started) * 1000.0)

    ttft = sorted(ttft_ms)
    total = sorted(total_ms)
    p50_ttft = statistics.median(ttft)
    p50_total = statistics.median(total)
    mean_ttft = sum(ttft) / len(ttft)
    mean_total = sum(total) / len(total)
    logger.info(
        "latency over %d requests: mean ttft=%.0fms (p50 %.0fms), mean total=%.0fms (p50 %.0fms)",
        repeat,
        mean_ttft,
        p50_ttft,
        mean_total,
        p50_total,
    )
    def percentile(sorted_ms: list[float], p: float) -> float:
        if not sorted_ms:
            return float("nan")
        index = round(p / 100.0 * (len(sorted_ms) - 1))
        return sorted_ms[index]

    return {
        "mean_ttft_ms": round(mean_ttft),
        "mean_total_ms": round(mean_total),
        "p50_ttft_ms": round(p50_ttft),
        "p50_total_ms": round(p50_total),
        "p95_ttft_ms": round(percentile(ttft, 95.0)),
        "requests": repeat,
    }


async def main() -> None:
    args = parse_args()
    settings = make_settings()
    llm: LlmClient | None = None
    if not args.no_llm:
        llm = LlmClient(settings.ollama_url, settings.chat_model)

    classify: dict | None = None
    retrieve: dict | None = None
    latency: dict | None = None

    if args.mode in ("classify", "all"):
        classify = await run_classify(settings, llm)
    if args.mode in ("retrieve", "all"):
        retrieve = await run_retrieve(settings, args.top_k, args.cleanup)
    if args.mode in ("latency", "all") and not args.no_llm:
        latency = await run_latency(settings, args.repeat)
    if args.no_llm and args.mode in ("latency", "all"):
        print("Skipped latency eval (--no-llm)")

    x = retrieve["recall_pct"] if retrieve else float("nan")
    y = classify["accuracy_pct"] if classify else float("nan")
    z = latency["mean_ttft_ms"] if latency else float("nan")
    print(
        f"\nRetrieval accuracy: {x:.0f}% · Ticket classification accuracy: {y:.0f}% "
        f"· Average response latency: <{z:.0f} ms (mean TTFT)"
    )


if __name__ == "__main__":
    asyncio.run(main())
