# IT Help Desk & Ticketing Management System

Microservices-based IT Help Desk platform. Employees submit tickets, IT agents resolve them,
admins manage the system, and an AI assistant helps deflect and triage issues.

## Architecture

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the full system design, service
boundaries, data ownership rules, and design principles.

## Quick Start

### Prerequisites

- Docker and Docker Compose v2+
- OpenSSL (to generate JWT signing keys)

### 1. Setup

```bash
./scripts.sh setup
```

This generates RSA keys in `infra/certs/` and creates `.env` from the example.

### 2. Configure environment

```bash
# Edit .env and set a strong MSSQL_SA_PASSWORD
vim .env
```

### 3. Start the stack

```bash
docker compose up --build
```

This will:
- Pull SQL Server 2022 image
- Build and start the Identity Service
- Run EF Core migrations automatically (Development mode)
- Seed the Roles table (Admin, IT Support Agent, Employee, Manager)

The Identity Service is available at `http://localhost:5000`.

### 4. Verify health

```bash
curl http://localhost:5000/health
# {"status":"healthy","service":"identity-service"}
```

### 5. Open Swagger UI

Navigate to `http://localhost:5000/swagger` in your browser to explore and test the API interactively.

## API Endpoints

### POST /api/auth/register

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#",
    "fullName": "System Administrator"
  }'
```

Response (200):
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "refreshToken": "a1b2c3d4e5f6...",
  "expiresAt": "2026-07-15T18:30:02Z"
}
```

**Password rules:**
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character (`!@#$%^&*...`)

### POST /api/auth/login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!@#"
  }'
```

Response (200):
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "refreshToken": "x9y8z7w6...",
  "expiresAt": "2026-07-15T18:32:13Z"
}
```

Invalid credentials return 401:
```json
{
  "message": "Invalid email or password."
}
```

### GET /api/auth/me

```bash
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

Response (200):
```json
{
  "id": "fa2d0daf-887b-466a-be2b-210cec9b4f3d",
  "email": "admin@example.com",
  "fullName": "System Administrator",
  "role": "Employee",
  "isActive": true,
  "createdAt": "2026-07-15T18:17:12Z",
  "lastLoginAt": "2026-07-15T18:17:13Z"
}
```

### POST /api/auth/refresh

Refresh tokens are **single-use with rotation**. Each call returns a new refresh token
and revokes the old one. Attempting to reuse a revoked token fails.

```bash
curl -X POST http://localhost:5000/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "<REFRESH_TOKEN>"
  }'
```

Response (200):
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "refreshToken": "newRefreshToken123...",
  "expiresAt": "2026-07-15T18:32:13Z"
}
```

Revoked token returns 401:
```json
{
  "message": "Refresh token is expired or has been revoked."
}
```

### POST /api/auth/logout

Revokes the provided refresh token.

```bash
curl -X POST http://localhost:5000/api/auth/logout \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "<REFRESH_TOKEN>"
  }'
```

Response: `204 No Content`

### GET /.well-known/jwks.json

Returns the public RSA key in JWKS format for JWT validation.

```bash
curl http://localhost:5000/.well-known/jwks.json
```

Response (200):
```json
{
  "keys": [
    {
      "kty": "RSA",
      "kid": "identity-rsa-key",
      "use": "sig",
      "alg": "RS256",
      "n": "...",
      "e": "AQAB"
    }
  ]
}
```

## Validation Errors

The API returns structured validation errors with FluentValidation:

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email": "x", "password": "1", "fullName": ""}'
```

Response (400):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["A valid email address is required."],
    "FullName": ["Full name is required."],
    "Password": [
      "Password must be at least 8 characters long.",
      "Password must contain at least one uppercase letter.",
      "Password must contain at least one lowercase letter.",
      "Password must contain at least one digit.",
      "Password must contain at least one special character."
    ]
  },
  "traceId": "00-c8571dda...-00"
}
```

## JWT & Inter-Service Auth

### How other services validate tokens

Per the architecture (`docs/ARCHITECTURE.md` section 8), downstream services do **not** call
back to Identity Service on every request. Instead:

1. **The API Gateway** validates the JWT once on the way in using the public key.
2. **Downstream services** receive the validated JWT and can optionally re-validate locally
   using the same public key.

The public RSA key is available at:
```
GET /.well-known/jwks.json
```

This follows the JWKS standard. Other services can fetch this endpoint once at startup and
cache the key, or embed the public PEM directly (preferred for zero network dependency).

### Token lifetimes

| Token | Lifetime | Storage |
|-------|----------|---------|
| Access token | 15 minutes | Client-side (memory) |
| Refresh token | 7 days | Server-side (DB, hashed) |

### Key format

Identity Service signs with RS256 (asymmetric). The private key stays inside the Identity
Service container. The public key is exposed for validation. Services that need to validate
tokens should:

1. Fetch `/.well-known/jwks.json` at startup
2. Match the `kid` header in JWTs against the JWKS key set
3. Cache the key and only refresh periodically (e.g. every 24 hours)

### Refresh token security

Refresh tokens are stored **SHA256-hashed** in the database. The raw token is only sent
to the client once. On refresh/logout, the incoming token is hashed before lookup — the
database never sees plaintext refresh tokens.

## Database Tables

| Table | Description |
|-------|-------------|
| `Users` | User accounts (id, email, password hash, full name, role, status) |
| `Roles` | Seed data: Admin, IT Support Agent, Employee, Manager |
| `RefreshTokens` | Single-use refresh tokens with rotation, hashed in DB |
| `UserActivityLog` | Audit trail for login, token refresh, logout events |

## Project Structure

```
helpdesk-platform/
├── compose.yaml
├── .env
├── docs/ARCHITECTURE.md
├── infra/
├── scripts.sh
├── services/
│   └── ...
├── tests/
│   └── IdentityService.Tests/
└── README.md
```

## Testing

Unit tests use **xUnit**, **Moq**, and **FluentAssertions**.

### Commands

```bash
./scripts.sh setup       # Generate RSA keys and create .env
./scripts.sh test        # Run all unit tests
./scripts.sh coverage    # Run tests and show code coverage
./scripts.sh clean       # Remove test results and build artifacts
./scripts.sh help        # Show all available commands
```

### Coverage

| Class | Coverage |
|-------|----------|
| Domain entities | 100% |
| AuthService | 98.4% |
| PasswordHasherService | 100% |
| JwtTokenService | 90.5% |
| All validators | 100% |
| All DTOs | 100% |
| EF Core / Repositories | 0% (integration test territory) |

### Test breakdown

| File | Tests | What's tested |
|------|-------|---------------|
| `AuthServiceTests.cs` | 10 | Register, login, refresh, logout, get-user (mocked dependencies) |
| `PasswordHasherTests.cs` | 4 | Hash, verify, salt uniqueness (real implementation) |
| `JwtTokenServiceTests.cs` | 5 | Token generation, claims, validation (real RSA keys) |
| `AuthValidatorTests.cs` | 15 | All FluentValidation rules for each DTO |
| **Total** | **34** | |

## Tech Stack

- **Runtime**: .NET 8 (LTS)
- **Database**: SQL Server 2022 (Docker, Express edition)
- **ORM**: EF Core 8, code-first migrations
- **Auth**: JWT RS256 (asymmetric), PasswordHasher from ASP.NET Core Identity
- **Validation**: FluentValidation
- **Testing**: xUnit, Moq, FluentAssertions
- **Container**: Multi-stage Dockerfile (SDK build, ASP.NET runtime)

## Stopping the stack

```bash
docker compose down
# Add -v to also remove the database volume:
# docker compose down -v
```
