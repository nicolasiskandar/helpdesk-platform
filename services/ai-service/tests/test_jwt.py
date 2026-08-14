import pytest
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from jose import jwt

from app.core.jwt import JwtValidator

ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
NAME_CLAIM = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
AUDIENCE = "it-helpdesk-api"


def _key_pair():
    private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    pem = private_key.public_key().public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo,
    )
    return private_key, pem


def _public_key_file(tmp_path, pem):
    path = tmp_path / "public.pem"
    path.write_bytes(pem)
    return str(path)


def test_decode_extracts_claims(tmp_path):
    private_key, pem = _key_pair()
    token = jwt.encode(
        {
            "sub": "user-123",
            "email": "a@b.c",
            "aud": AUDIENCE,
            ROLE_CLAIM: "Admin",
            NAME_CLAIM: "John Smith",
        },
        private_key,
        algorithm="RS256",
    )
    claims = JwtValidator(_public_key_file(tmp_path, pem), AUDIENCE).decode(token)
    assert claims.user_id == "user-123"
    assert claims.email == "a@b.c"
    assert claims.role == "Admin"
    assert claims.name == "John Smith"


def test_decode_rejects_wrong_audience(tmp_path):
    private_key, pem = _key_pair()
    token = jwt.encode(
        {
            "sub": "user-123",
            "email": "a@b.c",
            "aud": "some-other-audience",
            ROLE_CLAIM: "Employee",
        },
        private_key,
        algorithm="RS256",
    )
    with pytest.raises(ValueError):
        JwtValidator(_public_key_file(tmp_path, pem), AUDIENCE).decode(token)


def test_decode_rejects_bad_token(tmp_path):
    _, pem = _key_pair()
    with pytest.raises(ValueError):
        JwtValidator(_public_key_file(tmp_path, pem), AUDIENCE).decode("not-a-token")


def test_decode_verifies_issuer_when_configured(tmp_path):
    private_key, pem = _key_pair()
    token = jwt.encode(
        {"sub": "user-123", "aud": AUDIENCE, "iss": "it-helpdesk-identity"},
        private_key,
        algorithm="RS256",
    )
    validator = JwtValidator(
        _public_key_file(tmp_path, pem), AUDIENCE, issuer="it-helpdesk-identity"
    )
    assert validator.decode(token).user_id == "user-123"


def test_decode_rejects_wrong_issuer(tmp_path):
    private_key, pem = _key_pair()
    token = jwt.encode(
        {"sub": "user-123", "aud": AUDIENCE, "iss": "evil-issuer"},
        private_key,
        algorithm="RS256",
    )
    validator = JwtValidator(
        _public_key_file(tmp_path, pem), AUDIENCE, issuer="it-helpdesk-identity"
    )
    with pytest.raises(ValueError):
        validator.decode(token)
