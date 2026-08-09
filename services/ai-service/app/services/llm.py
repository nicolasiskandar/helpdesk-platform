import json

import httpx


class LlmClient:
    """Streams token responses from an Ollama-compatible /api/generate endpoint."""

    def __init__(self, base_url: str, model: str):
        self._base_url = base_url.rstrip("/")
        self._model = model

    async def generate(self, prompt: str, *, max_tokens: int = 512, temperature: float = 0.3):
        url = f"{self._base_url}/api/generate"
        payload = {
            "model": self._model,
            "prompt": prompt,
            "stream": True,
            "keep_alive": 3600,
            "options": {"num_predict": max_tokens, "temperature": temperature},
        }
        timeout = httpx.Timeout(connect=15.0, read=600.0, write=30.0, pool=15.0)
        async with httpx.AsyncClient(timeout=timeout) as client, client.stream("POST", url, json=payload) as response:
            response.raise_for_status()
            async for line in response.aiter_lines():
                if not line:
                    continue
                try:
                    data = json.loads(line)
                except json.JSONDecodeError:
                    continue
                token = data.get("response")
                if token:
                    yield token
                if data.get("done"):
                    return

    async def warmup(self) -> None:
        """Loads the chat model into memory so the first user request is fast."""
        url = f"{self._base_url}/api/generate"
        payload = {
            "model": self._model,
            "prompt": "Reply with the single word: ready.",
            "stream": False,
            "keep_alive": 3600,
            "options": {"num_predict": 8},
        }
        async with httpx.AsyncClient(timeout=httpx.Timeout(connect=15.0, read=600.0, write=30.0, pool=15.0)) as client:
            resp = await client.post(url, json=payload)
            resp.raise_for_status()
