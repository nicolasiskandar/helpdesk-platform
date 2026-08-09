from fastapi import HTTPException, Request

from app.core.jwt import JwtClaims, JwtValidator


def get_current_user(request: Request) -> JwtClaims:
    validator: JwtValidator = request.app.state.jwt_validator
    auth = request.headers.get("Authorization", "")
    if not auth.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    token = auth[len("Bearer "):].strip()
    try:
        return validator.decode(token)
    except ValueError as exc:
        raise HTTPException(status_code=401, detail="Invalid token") from exc
