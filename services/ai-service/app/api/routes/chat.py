import json
import logging

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel
from sse_starlette.sse import EventSourceResponse

from app.api.dependencies import get_current_user
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])


class ChatRequest(BaseModel):
    message: str


@router.get("/health/ready")
async def health_ready(request: Request):
    settings = request.app.state.settings
    models_ok, missing = await request.app.state.are_models_ready(settings)
    if not models_ok:
        raise HTTPException(
            status_code=503,
            detail=f"AI models are still downloading (missing: {', '.join(missing)}).",
        )
    return {"status": "ok"}


@router.post("/chat")
async def chat(req: ChatRequest, request: Request, _: JwtClaims = Depends(get_current_user)):  # noqa: B008
    message = req.message.strip()
    if not message:
        raise HTTPException(status_code=400, detail="Message cannot be empty.")

    rag = request.app.state.rag
    llm = request.app.state.llm
    settings = request.app.state.settings

    models_ok, missing = await request.app.state.are_models_ready(settings)
    if not models_ok:
        raise HTTPException(
            status_code=503,
            detail=f"AI models are still downloading (missing: {', '.join(missing)}). Please try again in a moment.",
        )

    try:
        context = await rag.retrieve(message)
    except Exception as exc:  # noqa: BLE001
        logger.warning("RAG retrieval failed, answering without context: %s", exc)
        context = []

    async def event_stream():
        yield {"event": "meta", "data": json.dumps({"sources": len(context)})}
        try:
            prompt = rag.build_prompt(message, context)
            async for token in llm.generate(
                prompt, max_tokens=settings.max_tokens, temperature=settings.temperature
            ):
                yield {"event": "token", "data": json.dumps({"token": token})}
        except Exception as exc:  # noqa: BLE001
            yield {"event": "error", "data": json.dumps({"error": str(exc)})}
        finally:
            yield {"event": "done", "data": ""}

    return EventSourceResponse(event_stream())
