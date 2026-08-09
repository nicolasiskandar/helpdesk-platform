import asyncio
import os

import httpx

PULL_TIMEOUT = httpx.Timeout(connect=15.0, read=600.0, write=30.0, pool=15.0)
MAX_ATTEMPTS = 3


def normalize_model_name(name: str) -> str:
    """Ollama reports a model without an explicit tag as 'name:latest'."""
    return name if ":" in name else f"{name}:latest"


async def list_models(base_url: str) -> set[str]:
    async with httpx.AsyncClient(timeout=10) as client:
        resp = await client.get(f"{base_url}/api/tags")
        resp.raise_for_status()
        return {m["name"] for m in resp.json().get("models", [])}


async def pull(model: str, base_url: str) -> None:
    url = f"{base_url}/api/pull"
    async with httpx.AsyncClient(timeout=PULL_TIMEOUT) as client:
        async with client.stream("POST", url, json={"model": model}) as response:
            response.raise_for_status()
            async for _ in response.aiter_bytes():
                pass
        print(f"Pulled model {model}", flush=True)


async def main() -> None:
    base_url = os.environ.get("OLLAMA_URL", "http://ollama:11434").rstrip("/")
    models = [
        normalize_model_name(os.environ.get("AI_CHAT_MODEL", "llama3.2:3b")),
        normalize_model_name(os.environ.get("AI_EMBEDDING_MODEL", "nomic-embed-text")),
    ]

    try:
        present = await list_models(base_url)
        print(f"Models already present: {sorted(present)}", flush=True)
    except Exception as exc:  # noqa: BLE001
        print(f"Could not list models (Ollama not ready yet): {exc}", flush=True)
        return

    for model in models:
        if model in present:
            print(f"{model} already present, skipping", flush=True)
            continue
        for attempt in range(1, MAX_ATTEMPTS + 1):
            try:
                await pull(model, base_url)
                break
            except Exception as exc:  # noqa: BLE001
                print(f"Pull {model} attempt {attempt}/{MAX_ATTEMPTS} failed: {exc}", flush=True)
                await asyncio.sleep(5)
        else:
            print(f"GAVE UP pulling {model} after {MAX_ATTEMPTS} attempts", flush=True)


if __name__ == "__main__":
    asyncio.run(main())
