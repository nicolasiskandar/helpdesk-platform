from pathlib import Path

from jose import JWTError, jwt
from pydantic import BaseModel

ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
NAME_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"


class JwtClaims(BaseModel):
    user_id: str
    email: str | None = None
    role: str | None = None
    name: str | None = None


class JwtValidator:
    """Decodes RS256 JWTs signed by the Identity service using the shared public key."""

    def __init__(self, public_key_path: str, audience: str):
        self._public_key = Path(public_key_path).read_text()
        self._audience = audience

    def decode(self, token: str) -> JwtClaims:
        try:
            claims = jwt.decode(
                token,
                self._public_key,
                algorithms=["RS256"],
                audience=self._audience,
            )
        except JWTError as exc:
            raise ValueError("Invalid token") from exc
        return JwtClaims(
            user_id=claims.get("sub", ""),
            email=claims.get("email"),
            role=claims.get(ROLE_CLAIM) or claims.get("role"),
            name=claims.get(NAME_CLAIM) or claims.get("name"),
        )
