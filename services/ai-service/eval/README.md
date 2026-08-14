# AI Service Eval Harness

Small, committed evaluation set that measures the AI service's three headline
metrics using the **real production code paths** (no fakes, no HTTP/JWT):

| Metric | Symbol | Definition |
|---|---|---|
| Retrieval accuracy | **X%** | recall@k — share of eval queries where at least one expected KB article lands in the top-k hits (default k=5). Precision@5 and MRR are also reported |
| Ticket classification accuracy | **Y%** | exact match — predicted category AND priority both correct (hybrid rule + LLM classifier) |
| Average response latency | **<Z ms** | mean time-to-first-token of a chat completion via `LlmClient.generate` (the standard latency metric for a streaming API; p50/p95 and mean full-response time are also reported) |

The eval is self-contained and **in-process**: it drives `Classifier`,
`LlmClient`, `EmbeddingClient`, `VectorStore` and `Indexer` directly, so the
numbers reflect exactly what the running service would do.

## Latest measured results (August 2026, warm models)

```
Retrieval accuracy: 99% (112/113 queries recall@5 · precision@5 22.8% · MRR 0.957)
Ticket classification accuracy: 56% (57/102 exact match · stable across 3 runs)
Average response latency: <497 ms mean TTFT (p50 455 ms; mean full response 6.6 s)
```

These numbers were produced after two service changes measured by this harness:
a priority rubric in the LLM classification prompt **and** a guard so the LLM
only fills priority when the rules could not determine the category (rules stay
authoritative per design rule #4). Together they moved exact-match
classification from 42-44% to 56% on the original+expanded data.

## Layout

```
eval/
  __init__.py
  run_eval.py                 # CLI runner (argparse)
  README.md
  data/
    classification_eval.jsonl # 102 labeled tickets (id, title, description, category, priority, note)
    corpus.jsonl              # 150 KB articles (id, title, body, category)
    retrieval_eval.jsonl      # 113 queries (id, query, expected_doc_ids)
```

## Prerequisites

- `services/ai-service/.venv` created (`./scripts.sh test-ai` does this) and activated.
- Full run (`--mode all`) needs the docker stack up (`./scripts.sh up`) so Ollama
  (embeddings + chat) and Qdrant are reachable. The runner defaults to
  `http://localhost:11434` / `http://localhost:6333` because docker service
  names don't resolve from the host; override with `OLLAMA_URL` / `QDRANT_URL`.
- The first full run indexes 150 articles into the **dedicated `helpdesk_eval`
  collection** — the production `helpdesk_index` collection is never touched.
  Warm-up takes a while on first use (models load into memory).

## Running

```bash
# Offline: classification only, no LLM (rule-based + defaults). No stack needed.
python -m eval.run_eval --mode classify --no-llm

# Full run with the stack up (classify + retrieve + latency).
python -m eval.run_eval --mode all

# Full run, then delete the eval collection (leaves no trace in Qdrant).
python -m eval.run_eval --mode all --cleanup

# Individual modes.
python -m eval.run_eval --mode retrieve --top-k 5
python -m eval.run_eval --mode latency --repeat 20
```

Flags: `--mode all|classify|retrieve|latency`, `--no-llm`, `--top-k` (default 5),
`--repeat` (default 20), `--cleanup`.

## Output

Per-eval logs show per-case misses (id, expected vs predicted, method) so
failures are actionable, then a one-line summary:

```
Retrieval accuracy: X% · Ticket classification accuracy: Y% · Average response latency: <Z ms (mean TTFT)
```

## Data notes

The eval data intentionally includes adversarial cases the current code gets
wrong, so the metrics stay meaningful:

- **Cross-newline critical miss**: `CRITICAL_PATTERNS` uses `.*` which never
  matches a newline, so `c57` ("VPN is down" / "the entire office cannot
  connect") is NOT caught by the rules and only the LLM can recover it.
- **Rule-hits**: `c36`/`c54`/`c58` exercise the keyword rules (server down,
  data loss, network) that must always win per design rule #4.
- **LLM-only category/priority**: `c56`, `c59`, `c60`, `c61`, `c62`, `c64` have
  no rule keyword and require the LLM.
- **Known rule misfires**: `c45` ("installer" → Software rule, human label
  Access) and `c48`/`c52` ("network"/"printer" keyword trap) — these will score
  as misses and are a deliberate honesty signal, not a bug.
- **Retrieval miss**: `q111` ("admin rights") matches no article because the
  corpus lacks an article using the word "admin" — a real retrieval gap, not a
  data bug.

## Getting the resume bullet numbers

After a full `--mode all` run with warm models, the summary line prints the
numbers directly (mean TTFT = "average response latency"). Example format:

> Developed an LLM-powered RAG service using Python, FastAPI, embeddings, and
> vector search, achieving 99% retrieval accuracy, 56% ticket classification
> accuracy, and <497 ms average response latency

## Lint / CI

- Not collected by pytest (no `test_*.py` files) and not linted by
  `ruff check app tests` — CI is unaffected. Run `ruff check --no-cache eval`
  from `services/ai-service/` to lint it manually.
