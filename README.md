# IT Help Desk & Ticketing Management System

Microservices-based IT Help Desk platform. Employees submit tickets, IT agents resolve them,
admins manage the system, and an AI assistant helps deflect and triage issues.

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
- Build and start the Identity Service, Ticket Service, and API Gateway
- Run EF Core migrations automatically (Development mode)
- Seed the Roles table (Admin, IT Support Agent, Employee, Manager)
- Start RabbitMQ, Jaeger, OTel Collector, Prometheus, and Grafana

The stack is available at:

| Service | URL | Purpose |
|---------|-----|---------|
| Frontend | http://localhost:3000 | Next.js app |
| API Gateway | http://localhost:5000 | Single entry point for all API calls |
| Identity API | http://localhost:5010 | Direct access (bypass gateway) |
| Ticket API | http://localhost:5011 | Direct access (bypass gateway) |
| Swagger (Identity) | http://localhost:5010/swagger | Identity API docs |
| Swagger (Ticket) | http://localhost:5011/swagger | Ticket API docs |
| Jaeger UI | http://localhost:16686 | Distributed tracing |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3001 | Dashboards (admin/admin) |
| RabbitMQ | http://localhost:15672 | Message broker (guest/guest) |
| SQL Server | localhost:1433 | Database |

### 4. Verify health

```bash
# Gateway
curl http://localhost:5000/health

# Identity Service (includes SQL Server check)
curl http://localhost:5010/health/ready

# Ticket Service (includes SQL Server + RabbitMQ checks)
curl http://localhost:5011/health/ready
```

### 5. Explore

- Open `http://localhost:5000/swagger` for the Gateway-proxied Identity API
- Open `http://localhost:16686` for Jaeger traces
- Open `http://localhost:9090` for Prometheus metrics
- Open `http://localhost:3001` for Grafana dashboards (admin/admin)

## API Endpoints

All API calls go through the Gateway at `http://localhost:5000`. The gateway routes:

- `/api/auth/*` → Identity Service
- `/api/users/*` → Identity Service
- `/api/tickets/*` → Ticket Service

### POST /api/auth/register

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "example@example.com",
    "password": "example123#",
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
    "email": "example@example.com",
    "password": "example123#"
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
  "email": "example@example.com",
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

### PUT /api/auth/me

Update the current user's profile (name, email). Requires authentication.

```bash
curl -X PUT http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "New Name",
    "email": "newemail@example.com"
  }'
```

### POST /api/auth/change-password

Change the current user's password. Requires the current password for verification.

```bash
curl -X POST http://localhost:5000/api/auth/change-password \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "currentPassword": "OldPassword123!",
    "newPassword": "NewPassword456!"
  }'
```

### GET /.well-known/jwks.json

Returns the public RSA key in JWKS format for JWT validation.

```bash
curl http://localhost:5000/.well-known/jwks.json
```

### User Management Endpoints (Admin / Manager)

```bash
# List all users (all authenticated users, paginated, filterable)
curl "http://localhost:5000/api/users?page=1&pageSize=20&search=admin" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Get user by ID
curl http://localhost:5000/api/users/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Create a new user (Admin only)
curl -X POST http://localhost:5000/api/users \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "agent@helpdesk.com",
    "password": "Agent123!",
    "fullName": "New Agent",
    "roleName": "IT Support Agent"
  }'

# Update a user (Admin only)
curl -X PUT http://localhost:5000/api/users/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "updated@helpdesk.com",
    "fullName": "Updated Name",
    "roleName": "Employee",
    "isActive": true
  }'

# Deactivate a user (Admin only)
curl -X PATCH http://localhost:5000/api/users/<id>/deactivate \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Activate a user (Admin only — use PUT with isActive: true)
curl -X PUT http://localhost:5000/api/users/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{ "isActive": true }'

# Delete a user (Admin only)
curl -X DELETE http://localhost:5000/api/users/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

**Single admin constraint**: Only one Admin user is allowed. The system rejects creating a second Admin or promoting a user to Admin if one already exists. An existing Admin can change their own role away from Admin.

### Ticket Endpoints

```bash
# Create a ticket
curl -X POST http://localhost:5000/api/tickets \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Printer not working",
    "description": "The 3rd floor printer is jammed",
    "categoryName": "Hardware",
    "priorityName": "Medium"
  }'

# Create a ticket with attachment
curl -X POST http://localhost:5000/api/tickets \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -F "ticket={\"title\":\"Printer not working\",\"description\":\"The 3rd floor printer is jammed\",\"categoryName\":\"Hardware\",\"priorityName\":\"Medium\"};type=application/json" \
  -F "file=@screenshot.png"

# List all tickets
curl http://localhost:5000/api/tickets \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# List open unassigned tickets (for ticket queue / self-assignment)
curl http://localhost:5000/api/tickets/open-unassigned \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Get ticket by ID
curl http://localhost:5000/api/tickets/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Assign an agent to a ticket (Admin/Manager)
curl -X POST http://localhost:5000/api/tickets/<id>/assignments \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{ "agentUserId": "<agent-user-id>" }'

# Unassign the agent from a ticket (Admin/Manager)
curl -X DELETE http://localhost:5000/api/tickets/<id>/assignments/<agent-user-id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Pick up (self-assign) an open unassigned ticket (any role)
curl -X POST http://localhost:5000/api/tickets/<id>/claim \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Get ticket attachments
curl http://localhost:5000/api/tickets/<id>/attachments \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Download a ticket attachment
curl http://localhost:5000/api/tickets/<ticketId>/attachments/<attachmentId> \
  -H "Authorization: Bearer <ACCESS_TOKEN>" -o file

# Get comments for a ticket (role-based: private comments visible only to
# ticket creator, assigned agent, and admin)
curl http://localhost:5000/api/tickets/<id>/comments \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Add a comment to a ticket
curl -X POST http://localhost:5000/api/tickets/<id>/comments \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{ "content": "Working on it", "isPrivate": false }'

# Get agent workload stats (Admin/Manager)
curl http://localhost:5000/api/tickets/agent-workload \
  -H "Authorization: Bearer <ACCESS_TOKEN>"

# Delete a ticket (Admin or ticket Creator only, must be Open status)
curl -X DELETE http://localhost:5000/api/tickets/<id> \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

## Observability

### Architecture

All backend services export traces and metrics via OTLP gRPC to an **OTel Collector**,
which fans out to Jaeger (traces) and Prometheus (metrics):

```
Backend Services ──OTLP──▶ OTel Collector ──▶ Jaeger (traces)
                                  │
                                  └──▶ Prometheus (metrics) ◀── Grafana (dashboards)
```

### Distributed Tracing (Jaeger)

All three backend services export traces to the OTel Collector via OTLP (gRPC on port 4317).
The collector forwards traces to Jaeger. Traces include HTTP requests, EF Core queries,
outbound HTTP calls, and RabbitMQ publishes with W3C `traceparent`/`tracestate` propagation.
The Gateway's `TraceContextTransform` injects trace context into proxied requests so
downstream services continue the same trace.

Open Jaeger UI at `http://localhost:16686` and select a service to view traces.

### Metrics (Prometheus + Grafana)

The OTel Collector exposes 25 metrics on port 8889 which Prometheus scrapes. Key metrics:

- `helpdesk_http_server_request_duration_seconds` — request latency histogram
- `helpdesk_http_server_active_requests` — concurrent in-flight requests
- `helpdesk_http_client_request_duration_seconds` — outbound HTTP call latency
- `helpdesk_dns_lookup_duration_seconds` — DNS resolution time
- `helpdesk_kestrel_*` — Kestrel connection pool stats

Open Prometheus at `http://localhost:9090` to query metrics.
Open Grafana at `http://localhost:3001` for the pre-provisioned **Helpdesk Overview**
dashboard (request rate, p95 latency, active requests, HTTP status codes).

### Health Checks

| Endpoint | Service | Checks |
|----------|---------|--------|
| `/health` | All services | Liveness (always 200) |
| `/health/ready` | Identity Service | SQL Server connectivity |
| `/health/ready` | Ticket Service | SQL Server + RabbitMQ connectivity |

### Request Logging

Every request gets a correlation ID (`X-Correlation-ID` header). If the caller doesn't
provide one, the service generates it. The correlation ID is logged with every request
along with method, path, status code, elapsed time, and trace ID.

Log format:
```
[14:32:01 INF] HTTP GET /api/tickets responded 200 in 45ms [abc123] TraceId=00-abc123...
```

## CI/CD (Jenkins)

A declarative `Jenkinsfile` drives the CI/CD pipeline. Jenkins runs in Docker
(`infra/jenkins/`) with a `docker:dind` sidecar — see
[`infra/jenkins/README.md`](infra/jenkins/README.md) for the full setup guide.

```bash
./scripts.sh jenkins   # Start the Jenkins controller (UI at http://localhost:8080)
```

Every branch/PR gets: backend build (gateway + identity + ticket + notification),
all 3 xUnit test suites, and a frontend build. `main` and version tags
additionally build & push the 5 Docker images to GHCR
(`ghcr.io/nicolasiskandar/helpdesk-platform-*`; `main` → `latest`, version tags → tag
name) and deploy the stack on the host.

Deployment specifics:

- **Two daemons**: an isolated `docker:dind` sidecar handles build/test and image
  build/push; the **host Docker socket** (`unix:///var/run/docker.sock`) is used only
  for the deploy step.
- The controller mounts `/opt/helpdesk-deploy:/opt/helpdesk-deploy` at the **same
  path** on host and controller so the host daemon resolves compose's `./infra/...`
  bind sources correctly.
- The repo is synced to the deploy workspace via `git archive` (committed tree only),
  and the `.env` is restored from the **base64-encoded** `helpdesk-env` secret
  (Jenkins' secret-text field is single-line). `remote-deploy.sh` validates the 6
  required keys, generates missing RSA certs, and runs `up --no-build`.
- Backend build/test stages run **sequentially** because they share a `nuget-cache`
  volume that isn't concurrency-safe; the frontend stage still runs in parallel.

See [`infra/jenkins/README.md`](infra/jenkins/README.md) for the full setup guide.

## Database Tables

### Identity Service

| Table | Description |
|-------|-------------|
| `Users` | User accounts (id, email, password hash, full name, role, status) |
| `Roles` | Seed data: Admin, IT Support Agent, Employee, Manager |
| `RefreshTokens` | Single-use refresh tokens with rotation, hashed in DB |
| `UserActivityLog` | Audit trail for login, token refresh, logout events |

### Ticket Service

| Table | Description |
|-------|-------------|
| `Tickets` | Ticket records with reference numbers (TKT-XXXXXX) |
| `TicketComments` | Public or private comments (private = ticket creator + assigned agent + admin only) |
| `TicketAssignments` | Agent assignment history |
| `TicketAttachments` | File metadata for ticket attachments (files stored on disk) |
| `TicketStatusHistory` | Status change audit trail |
| `TicketAuditLog` | Full audit trail for all ticket changes (who, what, when) |
| `Categories` | Ticket categories (seeded) |
| `Priorities` | Ticket priorities with levels (seeded) |
| `Statuses` | Ticket statuses (seeded) |
| `OutboxMessages` | Transactional outbox for domain events (with retry tracking + DLQ) |

## Project Structure

```
helpdesk-platform/
├── compose.yaml
├── Jenkinsfile                   # CI/CD pipeline (Jenkins)
├── .env
├── docs/ARCHITECTURE.md
├── infra/
│   ├── .env.example
│   ├── certs/                    # RSA keys (gitignored)
│   ├── jenkins/                  # Jenkins controller (Docker-in-Docker)
│   │   ├── Dockerfile
│   │   ├── plugins.txt
│   │   ├── docker-compose.yml
│   │   ├── README.md
│   │   └── deploy/               # Image-only compose override + remote-deploy.sh
│   ├── jaeger/
│   │   └── jaeger.yml            # Jaeger v2 config (OTLP, in-memory storage)
│   ├── otel-collector/
│   │   └── otel-collector.yml    # OTLP receiver → Jaeger (traces) + Prometheus (metrics)
│   ├── prometheus/
│   │   └── prometheus.yml        # Scrapes OTel Collector metrics
│   └── grafana/
│       ├── dashboards/
│       │   └── helpdesk-overview.json
│       └── provisioning/
│           ├── dashboards/
│           │   └── dashboards.yml
│           └── datasources/
│               └── prometheus.yml
├── scripts.sh
├── services/
│   ├── gateway/                  # YARP API Gateway
│   ├── identity-service/         # Auth, user management
│   ├── ticket-service/           # Ticket CRUD, workflow
│   ├── ai-service/               # (planned)
│   ├── notification-service/     # (planned)
│   └── search-service/           # (planned)
├── frontend/                     # Next.js app
├── tests/
│   ├── IdentityService.Tests/
│   └── TicketService.Tests/
└── README.md
```

## Testing

Unit tests use **xUnit**, **Moq**, and **FluentAssertions**.

### Commands

```bash
./scripts.sh setup            # Generate RSA keys and create .env
./scripts.sh up               # Start all services
./scripts.sh down             # Stop all services
./scripts.sh logs             # Tail logs from all services
./scripts.sh frontend-dev     # Run frontend locally (no Docker)
./scripts.sh test             # Run all unit tests (201 tests)
./scripts.sh coverage         # Run tests and show code coverage
./scripts.sh clean            # Remove test results and build artifacts
./scripts.sh jenkins          # Start the Jenkins CI/CD controller
./scripts.sh help             # Show all available commands
```

### Test breakdown

| File | Tests | What's tested |
|------|-------|---------------|
| `AuthServiceTests.cs` | 23 | Register, login, refresh, logout, get-user, update-profile, change-password |
| `UserServiceTests.cs` | 30 | User CRUD, single admin constraint, search/filter/paging |
| `PasswordHasherTests.cs` | 4 | Hash, verify, salt uniqueness |
| `JwtTokenServiceTests.cs` | 10 | Token generation, claims, validation, Name claim |
| `AuthValidatorTests.cs` | 18 | All FluentValidation rules |
| `UserValidatorTests.cs` | 27 | CreateUser, UpdateUser, UpdateProfile, ChangePassword validators |
| `TicketBusinessServiceTests.cs` | 48 | Ticket CRUD, assignment, workflow, self-assignment, open unassigned query, unassign outbox, comment visibility, status transition validation |
| `NotificationBusinessServiceTests.cs` | 16 | Event processing, preferences, notifications CRUD, SignalR, email delivery |
| **Total** | **201** | |

## Tech Stack

- **Runtime**: .NET 8 (LTS)
- **Database**: SQL Server 2022 (Docker, Express edition)
- **ORM**: EF Core 8, code-first migrations
- **Auth**: JWT RS256 (asymmetric), PasswordHasher from ASP.NET Core Identity
- **Validation**: FluentValidation
- **Messaging**: RabbitMQ (topic exchange, transactional outbox pattern with DLQ + retry limits)
- **Gateway**: YARP 2.1.0 (reverse proxy)
- **Tracing**: OpenTelemetry → OTel Collector → Jaeger (OTLP gRPC)
- **Metrics**: OpenTelemetry → OTel Collector → Prometheus → Grafana
- **Logging**: Serilog (structured, with TraceId/SpanId enrichment)
- **Testing**: xUnit, Moq, FluentAssertions
- **Frontend**: Next.js 16, React 19, shadcn/ui, Tailwind CSS v4
- **Container**: Multi-stage Dockerfile (SDK build, ASP.NET runtime)

## Stopping the stack

```bash
docker compose down
# Add -v to also remove database volumes:
# docker compose down -v
```
