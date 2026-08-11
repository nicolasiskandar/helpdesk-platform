import logging

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel

from app.api.dependencies import get_current_user
from app.core.jwt import JwtClaims

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["ai"])


class AnalyzeRequest(BaseModel):
    title: str
    description: str = ""


class AnalyzeResult(BaseModel):
    categoryId: int
    category: str
    priorityId: int
    priority: str
    method: str


@router.post("/analyze", response_model=AnalyzeResult)
async def analyze(
    req: AnalyzeRequest,
    request: Request,
    _: JwtClaims = Depends(get_current_user),  # noqa: B008
):
    title = req.title.strip()
    if not title:
        raise HTTPException(status_code=400, detail="Title cannot be empty.")

    settings = request.app.state.settings
    classifier = request.app.state.classifier
    llm = request.app.state.llm
    models_ok, _ = await request.app.state.are_models_ready(settings)
    return await classifier.classify(title, req.description, llm=llm if models_ok else None)
