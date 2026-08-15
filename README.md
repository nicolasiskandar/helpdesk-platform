# IT Help Desk & Ticketing Management System

A microservices-based IT Help Desk platform. Employees submit tickets, IT agents resolve them,
admins manage the system, and an AI assistant helps deflect, triage, summarize, and suggest
troubleshooting steps for issues.

This document is the **single source of truth** for the application: overview, architecture,
quick start, API reference, data model, messaging, testing, and CI/CD. Related documents:

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — condensed architecture summary + pointers
- [`docs/IT_Help_Desk_Diagrams.drawio`](docs/IT_Help_Desk_Diagrams.drawio) — ERDs for the relational services
- [`infra/jenkins/README.md`](infra/jenkins/README.md) — Jenkins CI/CD setup guide
- `AGENTS.md` (local only, gitignored) — fast-start guide for AI coding agents / contributors

---

## Features

- **Ticketing**: create, track, comment, attach files, assign, escalate, and resolve tickets
  with full audit history and TKT-XXXXXX reference numbers.
- **AI assistant**: RAG chat, category/priority classification, similar-ticket lookup, ticket
  summarization, and step-by-step troubleshooting grounded in the knowledge base + similar
  resolved tickets.
- **Knowledge base**: published/draft KB articles, searchable, with view tracking (Admin CRUD).
- **Global search**: keyword search over closed tickets (Meilisearch) and KB articles, combined
  in the topbar.
- **Notifications**: in-app (SignalR) + email, per-channel preferences, comment deep-linking.
- **Workflow**: self-assignment (pickup), multi-agent assignment, reopen, AI-confirmed close,
  pending-ticket access restrictions, private comments.
- **Reporting**: 6-month stats, SLA compliance, agent workload, resolution trends.
- **Observability**: distributed tracing (Jaeger), metrics (Prometheus/Grafana), structured
  logging with correlation IDs.
- **CI/CD**: Jenkins pipeline builds, tests, pushes images to GHCR, and deploys the stack.

## Architecture at a glance

### Services

| Service | Framework | Datastore | Direct port | Responsibility |
|---|---|---|---|---|
| API Gateway | .NET 8 (YARP) | — | 5000 | Single entry point; routes and validates JWTs |
| Identity Service | .NET 8 Web API | SQL Server (`IdentityDb`) | 5010 | Auth, user/role management, settings |
| Ticket Service | .NET 8 Web API | SQL Server (`TicketDb`) | 5011 | Ticket CRUD, assignment, workflow, KB articles, reporting |
| Notification Service | .NET 8 Web API | PostgreSQL (`NotificationDb`) | 5012 | In-app (SignalR) + email notifications |
| AI Service | Python 3.12 / FastAPI | Qdrant + Ollama + SQLite | 5090 | RAG chat, triage, summarization, troubleshooting, indexing |
| Search Service | .NET 8 Web API | Meilisearch | 5013 | Keyword search over closed tickets |

The Identity, Ticket, and Notification services follow Clean Architecture layers:
`Domain` → `Application` → `Infrastructure` → `Api`. Search Service is a focused .NET API
(`services/search-service/SearchService.sln`). The AI service is Python/FastAPI (no `.sln`).

### Gateway routing

All API calls go through the gateway at `http://localhost:5000`:

| Route | Backend |
|---|---|
| `/api/auth/*`, `/api/users/*`, `/api/settings/*` | Identity |
| `/api/tickets/*`, `/api/kb-articles/*` | Ticket |
| `/api/notifications/*`, `/hubs/notifications/*` | Notification |
| `/api/ai/*` | AI (cluster `ActivityTimeout` 10 min for SSE streaming) |
| `/api/search/*` | Search |
| `/identity/swagger`, `/ticket/swagger`, `/notification/swagger` | Proxied per-service Swagger |

### Monorepo layout

```
helpdesk-platform/
├── compose.yaml                 # Full stack definition (18 services)
├── Jenkinsfile                  # CI/CD pipeline
├── scripts.sh                   # Developer command wrapper
├── docs/                        # Architecture docs + drawio ERDs
├── infra/                       # .env.example, RSA certs, observability + Jenkins configs
├── services/
│   ├── gateway/                 # YARP API Gateway
│   ├── identity-service/        # Auth, user management, settings
│   ├── ticket-service/          # Ticket CRUD, workflow, KB articles, reporting
│   ├── notification-service/    # Notifications, SignalR, email
│   ├── ai-service/              # FastAPI RAG, triage, summarization
│   └── search-service/          # Meilisearch-backed closed-ticket search
├── frontend/                    # Next.js 16 app (pnpm, React 19, shadcn/ui)
└── tests/                       # xUnit test projects (reference service sources directly)
```

## Tech stack

- **Runtime**: .NET 8 (LTS) + ASP.NET Core; Python 3.12 / FastAPI for the AI service
- **Frontend**: Next.js 16.2.6, React 19, shadcn/ui (`base-nova`), Tailwind CSS v4, Lucide icons
- **Databases**: SQL Server 2022 (Identity + Ticket), PostgreSQL 16 (Notification), Meilisearch (Search), Qdrant (AI vector store), SQLite (AI dedup + follow-up store)
- **ORM**: EF Core 8, code-first migrations
- **Auth**: JWT RS256 (asymmetric), ASP.NET Core `PasswordHasher`, refresh-token rotation
- **Validation**: FluentValidation
- **Messaging**: RabbitMQ (topic exchange, transactional outbox with DLQ + retry limits)
- **Gateway**: YARP 2.1.0 (reverse proxy)
- **AI/LLM**: Ollama (`llama3.2:3b` + `nomic-embed-text`), RAG over Qdrant
- **Observability**: OpenTelemetry → OTel Collector → Jaeger (traces) / Prometheus → Grafana (metrics); Serilog structured logging
- **Testing**: xUnit, Moq, FluentAssertions (backend); pytest + ruff (AI)
- **CI/CD**: Jenkins (Docker-in-Docker) → GHCR → `docker compose` deploy

## Quick start

### Prerequisites

- Docker and Docker Compose v2+
- OpenSSL (to generate JWT signing keys)
- Node.js 20+ with pnpm (only for running the frontend outside Docker)
- .NET 9 SDK (only for running backend tests — see the TFM note under [Testing](#testing))

### 1. Setup

```bash
./scripts.sh setup
```

This generates an RSA key pair in `infra/certs/`, creates `.env` from
`infra/.env.example` (keeping an existing `.env`), and fills random
`AI_SERVICE_KEY`, `SEARCH_SERVICE_KEY`, and `NOTIFICATION_SERVICE_KEY` values
(keeps keys that are already set).

### 2. Configure environment

```bash
vim .env
```

The only value you normally must change is `MSSQL_SA_PASSWORD` (SQL Server complexity:
8+ chars, upper, lower, digit, special). Required keys: `MSSQL_SA_PASSWORD`,
`AI_SERVICE_KEY`, `SEARCH_SERVICE_KEY`, `NOTIFICATION_SERVICE_KEY`,
`NEXT_PUBLIC_API_URL`, plus a valid RSA key pair at `infra/certs/private.pem` and
`infra/certs/public.pem`.

### 3. Start the stack

```bash
./scripts.sh up          # docker compose up --build -d
```

This builds and starts the full stack (SQL Server, RabbitMQ, gateway, all 6 services,
Ollama, Qdrant, Meilisearch, and the observability stack), runs EF Core migrations
automatically (Development mode), and seeds the `Roles` table.

On first boot the AI service pulls `llama3.2:3b` and `nomic-embed-text` in the
**background** — it returns 503 on `/api/ai/health/ready` until the models finish
downloading (this can take a few minutes). Don't restart it in a loop while warming up.

### 4. Verify health

```bash
curl http://localhost:5000/health                         # Gateway (liveness)
curl http://localhost:5010/health/ready                   # Identity (SQL Server)
curl http://localhost:5011/health/ready                   # Ticket (SQL Server + RabbitMQ)
curl http://localhost:5012/health/ready                   # Notification (PostgreSQL + RabbitMQ)
curl http://localhost:5090/api/ai/health/ready            # AI (Ollama + Qdrant + models; 503 until warm)
```

### 5. Explore

| Service | URL | Purpose |
|---|---|---|
| Frontend | http://localhost:3000 | Next.js app |
| API Gateway | http://localhost:5000 | Single entry point for all API calls |
| Swagger (Identity/Ticket/Notification) | `/identity/swagger`, `/ticket/swagger`, `/notification/swagger` via gateway | API docs |
| Jaeger UI | http://localhost:16686 | Distributed tracing |
| Prometheus | http://localhost:9090 | Metrics |
| Grafana | http://localhost:3001 | Dashboards (admin/admin) |
| RabbitMQ | http://localhost:15672 | Message broker (guest/guest) |
| Meilisearch | http://localhost:7700 | Keyword index |
| Qdrant | http://localhost:6333 | Vector store (dashboard on 6334) |
| Ollama | http://localhost:11434 | Local LLM server |
| Mailpit | http://localhost:8025 | Dev email catcher (SMTP on 1025) |

Direct service ports (bypassing the gateway): Identity=5010, Ticket=5011,
Notification=5012, Search=5013, AI=5090. SQL Server listens on `localhost:1433`,
Notification PostgreSQL on `localhost:5433`.

Use the **global search box** in the top-right of the app (desktop) to keyword-search
closed tickets and published KB articles as you type.

## Roles

| Role | Permissions |
|---|---|
| Admin | Full access — user management, system settings, delete any open ticket, see all private comments |
| IT Support Agent | Manage and resolve tickets, view all tickets, pick up open unassigned tickets, create private comments on assigned tickets |
| Employee | Create and track own tickets, pick up open unassigned tickets, delete own open tickets, create private comments on own tickets |
| Manager | Monitor team tickets and reports, assign/reassign agents, view agent workload (cannot delete tickets; sees private comments only if assigned) |

Seeded role IDs: Admin=1, IT Support Agent=2, Employee=3, Manager=4.

Key rules:

- **Single admin constraint**: only one Admin may exist. The system rejects creating or
  promoting a second Admin. An existing Admin can change away from Admin.
- **Ticket deletion**: only Admin or the ticket Creator, and only while the status is
  Open. Managers cannot delete tickets.
- **Self-assignment**: any authenticated user can pick up an open unassigned ticket
  (`POST /api/tickets/{id}/claim`).
- **Profile self-service**: users can update their own name/email (`PUT /api/auth/me`) and
  change their password (`POST /api/auth/change-password`).

## Frontend pages

All pages under the `(app)` route group are guarded by `components/auth-guard.tsx`
(unauthenticated users are redirected to `/login`).

| Route | Purpose |
|---|---|
| `/login`, `/register`, `/forgot-password`, `/reset-password` | Authentication |
| `/dashboard` | Home / overview |
| `/tickets` | Ticket list |
| `/tickets/new` | Create a ticket (uses AI analyze + similar-tickets) |
| `/tickets/queue` | Open unassigned tickets (pick-up queue) |
| `/tickets/{id}` | Ticket detail (comments, attachments, audit, AI summary + troubleshooting) |
| `/assistant` | AI chat assistant |
| `/knowledge-base` | KB articles (search, filters, Admin CRUD) |
| `/notifications`, `/notifications/settings` | Notifications + preference toggles |
| `/profile` | Profile self-service |
| `/reports` | KPIs + resolution chart (Admin/Manager) |
| `/admin/users`, `/admin/users/new` | User management (Admin) |
| `/admin/team-workload` | Per-agent open/resolved counts (Admin/Manager) |
| `/admin/settings` | System settings (Admin) |

Ticket statuses use the exact backend names: `Open`, `In Progress`,
`Resolved - Pending Confirmation`, `Closed`, `Resolved by AI`. Status IDs:
1=Open, 2=In Progress, 3=Resolved - Pending Confirmation, 4=Closed, 5=Resolved by AI.

## API reference

All requests go through the gateway at `http://localhost:5000` and require an
`Authorization: Bearer <ACCESS_TOKEN>` header unless noted. Errors return RFC 7807-style
JSON with a `message` and optional `errors` map (PascalCase backend keys).

### Authentication & users

| Method & path | Auth | Description |
|---|---|---|
| `POST /api/auth/register` | None | Create an account (rate limited: 5/hour) |
| `POST /api/auth/login` | None | Login (10/min); returns access + refresh token |
| `POST /api/auth/refresh` | None | Rotate a refresh token (30/min) |
| `POST /api/auth/logout` | None | Revoke a refresh token |
| `GET /api/auth/me` | Any | Current profile |
| `PUT /api/auth/me` | Any | Update own name/email |
| `POST /api/auth/change-password` | Any | Change password (requires current password; 10/5min) |
| `POST /api/auth/forgot-password` / `POST /api/auth/reset-password` | None | Password reset (shared 5/15min) |
| `GET /.well-known/jwks.json` | None | Public RSA key (JWKS) for JWT validation |
| `GET /api/users` | Any | List users (search, pagination, role/status filters) |
| `GET /api/users/{id}` | Admin/Manager | Get a user |
| `POST /api/users` | Admin | Create a user |
| `PUT /api/users/{id}` | Admin | Update a user (incl. `isActive`) |
| `PATCH /api/users/{id}/deactivate` | Admin | Deactivate a user |
| `DELETE /api/users/{id}` | Admin | Delete a user |
| `GET`/`PUT /api/settings` | Admin | System settings |

**Password rules**: min 8 chars, at least one uppercase, one lowercase, one digit, and one
special character.

**Rate limiting**: Identity auth endpoints are throttled by IP (fixed window, keyed on
`X-Forwarded-For`). Exceeded requests get `429` + `Retry-After`. There is no login lockout —
rate limiting is the brute-force mitigation.

**Refresh tokens** are single-use with rotation: each `refresh` call returns a new token and
revokes the old one. Tokens are stored SHA256-hashed; the raw token is never persisted.

#### Example: register

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

#### Example: login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{ "email": "example@example.com", "password": "example123#" }'
```

Invalid credentials return `401 { "message": "Invalid email or password." }`.

### Tickets & workflow

| Method & path | Auth | Description |
|---|---|---|
| `POST /api/tickets` | Any | Create a ticket (JSON or `multipart/form-data` with files) |
| `GET /api/tickets` | Any | List tickets (search, filters, pagination) |
| `GET /api/tickets/{id}` | Any* | Get a ticket |
| `GET /api/tickets/ref/{referenceNumber}` | Any* | Get by reference number (TKT-XXXXXX) |
| `GET /api/tickets/my` | Any | Current user's tickets |
| `GET /api/tickets/open-unassigned` | Any | Open tickets with no active assignment (pick-up queue) |
| `PUT /api/tickets/{id}` | Any* | Update a ticket |
| `PATCH /api/tickets/{id}/status` | Any* | Change status (workflow transitions) |
| `DELETE /api/tickets/{id}` | Admin or Creator | Delete — only when status is Open |
| `GET /api/tickets/{id}/assignments` | Any* | Assignment history |
| `POST /api/tickets/{id}/assignments` | Admin/Manager | Assign an agent |
| `DELETE /api/tickets/{id}/assignments/{agentUserId}` | Admin/Manager | Unassign an agent |
| `POST /api/tickets/{id}/claim` | Any | Pick up (self-assign) an open unassigned ticket |
| `POST /api/tickets/{id}/escalate` | Any* | Escalate a ticket |
| `GET /api/tickets/{id}/audit` | Any* | Audit log |
| `GET /api/tickets/categories` / `priorities` / `statuses` | Any | Lookup tables |
| `GET /api/tickets/agent-workload` | Admin/Manager | Per-agent open/resolved counts |
| `GET /api/tickets/statistics` | Admin/Manager | 6-month stats + trends |

\* Tickets that are neither Open nor Closed are visible/editable only by Admin, Manager, the
ticket creator, and agents with an active assignment. Everyone else gets **403** on any
ticket-scoped read or write (see [Access restrictions](#access-restrictions)).

#### Create a ticket

```bash
curl -X POST http://localhost:5000/api/tickets \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Printer not working",
    "description": "The 3rd floor printer is jammed",
    "categoryName": "Hardware",
    "priorityName": "Medium"
  }'
```

Create with an attachment (multipart):
```bash
curl -X POST http://localhost:5000/api/tickets \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -F "ticket={\"title\":\"Printer not working\",\"description\":\"The 3rd floor printer is jammed\",\"categoryName\":\"Hardware\",\"priorityName\":\"Medium\"};type=application/json" \
  -F "file=@screenshot.png"
```

**Idempotent create**: submitting an identical title + description + category within 5
minutes returns the existing ticket instead of creating a duplicate (absorbs
double-clicks/retries).

**Workflow / status transitions**: `Closed` (4) and `Resolved by AI` (5) can be reopened to
`In Progress` (2) by the ticket creator, Admin, or Manager. Assignments are many-to-many
(multiple agents can be assigned to one ticket).

#### Attachments & comments

| Method & path | Auth | Description |
|---|---|---|
| `GET /api/tickets/{id}/attachments` | Any* | List attachments |
| `POST /api/tickets/{id}/attachments` | Any* | Upload (extension allowlist enforced) |
| `GET /api/tickets/{id}/attachments/{attachmentId}` | Any* | Stream/download a file |
| `DELETE /api/tickets/{id}/attachments/{attachmentId}` | Admin, creator, or uploader | Delete file + row + audit entry |
| `GET /api/tickets/{id}/comments` | Any* | Comments (private ones filtered by role) |
| `POST /api/tickets/{id}/comments` | Any* | Add comment (multipart, optional files, optional recipients) |
| `GET /api/tickets/{id}/comments/{commentId}/attachments/{attachmentId}` | Any* | Download comment attachment |
| `DELETE /api/tickets/{id}/comments/{commentId}/attachments/{attachmentId}` | Comment author, creator, or admin | Delete |

Use fetch-based (not `<a href>`) downloads in the frontend because these endpoints require
`[Authorize]`. Uploads accept an extension allowlist (e.g. png/jpg/pdf/docx) and capture file
size. Ticket attachments live on disk at `uploads/{ticketId}/{guid}_{filename}`; comment
attachment metadata is stored in `CommentAttachments`.

### Knowledge base

| Method & path | Auth | Description |
|---|---|---|
| `GET /api/kb-articles` | Any | List (`search`, `category`, `page`, `pageSize`); published only unless Admin |
| `GET /api/kb-articles/{id}` | Any | Get (increments views) |
| `POST /api/kb-articles` | Admin | Create |
| `PUT /api/kb-articles/{id}` | Admin | Update |
| `DELETE /api/kb-articles/{id}` | Admin | Delete |

Articles have status `published` or `draft`. Validation via `KbArticleRequestValidator`.

### Notifications

| Method & path | Auth | Description |
|---|---|---|
| `GET /api/notifications` | Any | List notifications |
| `GET /api/notifications/unread-count` | Any | Unread count |
| `PATCH /api/notifications/{id}/read` | Any | Mark one read |
| `PATCH /api/notifications/read-all` | Any | Mark all read |
| `GET`/`PUT /api/notifications/preferences` | Any | Per-channel opt-in/out |

Clicking a notification marks it read and routes to `/tickets/{id}?comment={commentId}`
(comment deep-link) or `/tickets/{id}`. In-app delivery uses SignalR at
`/hubs/notifications`. Emails are sent to real recipient addresses resolved from Identity;
in dev they land in Mailpit.

### AI service

All AI endpoints are JWT-authenticated (except health). Streaming endpoints emit SSE events
(`meta`/`token`/`error`/`done`).

| Method & path | Auth | Description |
|---|---|---|
| `GET /api/ai/health/ready` | None | 503 until Qdrant + Ollama + both models are ready |
| `POST /api/ai/chat` | Any | RAG chat (SSE) |
| `POST /api/ai/analyze` | Any | Hybrid rule+LLM category/priority classifier |
| `POST /api/ai/similar-tickets` | Any | Semantic search over closed tickets |
| `POST /api/ai/summarize` | Any | Ticket-thread summary (SSE) |
| `POST /api/ai/troubleshooting` | Any | Step-by-step suggestions grounded in thread + RAG (SSE) |
| `POST /api/ai/reindex` | Admin | Re-index published KB articles |
| `POST /api/ai/reindex-tickets` | Admin | Backfill the vector store from the ticket DB |
| `POST /api/ai/confirm-resolved` | Any (guarded) | Confirm a pending-confirmation ticket as closed |

`confirm-resolved` is the AI service's **only** scoped write (design rule #3): it calls
`PATCH /api/tickets/{id}/status` with `X-AI-Service-Key` set to `AI_SERVICE_KEY`, and the
ticket service only accepts the 3→4 transition (`Resolved - Pending Confirmation` → `Closed`).

### Search

| Method & path | Auth | Description |
|---|---|---|
| `GET /api/search/tickets` | Any | Keyword search over closed tickets |

Query params: `q`, `category`, `priority`, `from`, `to`, `page`, `pageSize` (1–100). Uses
Meilisearch typo tolerance, filters, and newest-first sorting.

## Data model

Each service owns its data exclusively. A `UserId` in another service's tables is a plain
GUID — never a foreign key into Identity's `Users`. Referential integrity across services is
eventual, not DB-enforced. Full ERDs: `docs/IT_Help_Desk_Diagrams.drawio`.

### Identity Service (SQL Server — `IdentityDb`)

| Table | Description |
|---|---|
| `Users` | Email, password hash, full name, role, active flag |
| `Roles` | Seeded: Admin, IT Support Agent, Employee, Manager |
| `RefreshTokens` | Single-use, rotated, SHA256-hashed |
| `UserActivityLogs` | Login/refresh/logout audit trail |
| `Settings` | System settings (Admin-managed) |

JWT claims include `ClaimTypes.Name` (the user's full name) so downstream services write
human-readable audit entries (e.g. "Assigned to John Smith").

### Ticket Service (SQL Server — `TicketDb`)

| Table | Description |
|---|---|
| `Tickets` | Reference number (TKT-XXXXXX via DB sequence), title, description, category, priority, status, creator |
| `Categories`, `Priorities`, `Statuses` | Seeded lookup tables |
| `TicketAssignments` | Many-to-many assignment history |
| `TicketComments` | Public or private (`IsPrivate`; DB column `IsInternal` via `HasColumnName`) |
| `TicketAttachments`, `CommentAttachments` | File metadata (files on disk) |
| `TicketStatusHistory` | Status change audit |
| `TicketAuditLog` | Append-only; `ChangedByType` distinguishes `User` from `AI` |
| `KbArticles` | Title, excerpt, body, category, status, views, author |
| `OutboxMessages` | Transactional outbox with retry tracking + DLQ |

### Notification Service (PostgreSQL — `NotificationDb`)

| Table | Description |
|---|---|
| `Notifications` | Recipient, type, title, message, `TicketId`, `CommentId`, read state |
| `NotificationDeliveries` | Per-channel delivery tracking |
| `NotificationPreferences` | Per-user opt-in/out per channel |

### AI Service (Qdrant + SQLite)

Vector store `helpdesk_index` (768-dim) of ticket content + KB articles for semantic
similarity and RAG grounding; SQLite at `/data/dedup.db` for message dedup + the pending
confirmation follow-up store.

### Search Service (Meilisearch)

A projection of closed tickets only — reference number, title, description, category,
priority, close time. Never contains private comments, attachments, or user data.

## Messaging (RabbitMQ)

- **Single topic exchange**: `ticket.events`.
- **Routing keys**: `ticket.created`, `ticket.assigned`, `ticket.resolved`,
  `ticket.status_changed`, `ticket.deleted`, `ticket.unassigned`, `ticket.commented`, etc.
- **Consumer bindings**:
  - Notification service: binds `ticket.*` (reacts to most events).
  - AI indexing queue `ai-index.q`: binds `ticket.created`, `ticket.resolved`,
    `ticket.commented`, `ticket.status_changed`, `ticket.deleted`.
  - Search Service queue `search-index.q`: binds `ticket.resolved`, `ticket.status_changed`,
    `ticket.deleted` (upserts/deletes idempotent by ticket ID).
- **Transactional outbox (mandatory for Ticket writes)**: every domain event is inserted into
  the `Outbox` table in the same DB transaction as the business write. A background poller
  publishes rows to RabbitMQ. Never publish directly from a request handler. This guarantees
  at-least-once delivery.
- **Dead-letter queue**: failed messages retry up to `Outbox:MaxRetries` (default 5), then
  move to exchange `ticket.events.dlx` → queue `ticket.events.dlq`.
- **Deduplication contract**: every published message sets `MessageId` to the outbox row
  GUID; consumers must track seen `MessageId`s to deduplicate at-least-once deliveries.

Dev RabbitMQ runs the stock image with `guest`/`guest`. The AI service connects via
`RABBITMQ_URL`; the .NET services use `RabbitMQ__HostName`.

## Design rules (non-negotiable)

1. **No service queries another service's database.** Cross-service data access = scoped sync
   REST or event subscription only.
2. **Ticket service writes use the transactional outbox pattern.** Never publish directly
   from a request handler.
3. **AI service's write access is minimal** — it can only transition tickets from
   `Resolved - Pending Confirmation` to `Closed` (via `X-AI-Service-Key`).
4. **Priority classification keeps its rule-based override layer** — don't replace with a
   pure LLM.
5. **Ticket creation must never block on AI service availability** — fall back to
   `Uncategorized`/`Medium`.
6. **Assignment/workflow stays inside Ticket service** — it shares a transaction boundary
   with the ticket.
7. **Search service is optional** — it must never block ticket creation.

## Access restrictions

Tickets that are neither Open nor Closed are visible/editable only by Admin, Manager, the
ticket creator, and agents with an active assignment. Everyone else gets **403** on any
ticket-scoped read or write. List endpoints (`GET /api/tickets`, `/my`,
`/open-unassigned`, create) are not gated — the restriction applies per-ticket when opened.

**Private comments**: visible only to the ticket creator, currently assigned agent(s), and
admin. Only the ticket creator, assigned agent(s), or admin can create private comments.

**Comment recipients**: recipients may only be the ticket creator, Manager, or IT Support
Agent (Admin is excluded; other Employees cannot be targeted). A reply inherits the parent's
recipients when `recipientUserIds` is omitted, and a reply's recipients must be a subset of
the parent's recipients.

## Observability

All backend services export traces via OTLP gRPC to the **OTel Collector**, which fans out
to Jaeger (traces) and Prometheus (metrics):

```
Backend Services ──OTLP──▶ OTel Collector ──▶ Jaeger (traces)
                                  │
                                  └──▶ Prometheus (metrics) ◀── Grafana (dashboards)
```

- **Jaeger** (http://localhost:16686): distributed traces. Cross-service correlation via W3C
  `traceparent`/`tracestate` (gateway `TraceContextTransform` injects context into proxied
  requests; RabbitMQ messages carry trace headers).
- **Prometheus** (http://localhost:9090): scrapes the OTel Collector on `otel-collector:8889`
  (request latency, active requests, outbound HTTP latency, Kestrel stats).
- **Grafana** (http://localhost:3001, admin/admin): pre-provisioned "Helpdesk Overview"
  dashboard (request rate, p95 latency, active requests, status codes).
- **Logging**: Serilog structured logs enriched with `TraceId`/`SpanId`. Every request gets a
  correlation ID (`X-Correlation-ID` header; generated if absent), logged as
  `Method Path StatusCode ElapsedMs CorrelationId TraceId`.

### Health checks

| Endpoint | Checks |
|---|---|
| `/health` | Liveness (all services) |
| `/health/ready` (Identity) | SQL Server connectivity |
| `/health/ready` (Ticket) | SQL Server + RabbitMQ |
| `/health/ready` (Notification) | PostgreSQL + RabbitMQ |
| `/health/ready` (Search) | Meilisearch + RabbitMQ |
| `/api/ai/health/ready` | Ollama + Qdrant + both models (503 until warm) |

## Testing

Backend unit tests use **xUnit**, **Moq**, and **FluentAssertions**; the AI service uses
**pytest** + **ruff**.

```bash
./scripts.sh test            # Identity + Ticket + Search xUnit suites + AI (ruff + pytest)
./scripts.sh test-identity   # IdentityService.Tests only
./scripts.sh test-ticket     # TicketService.Tests only
./scripts.sh test-search     # SearchService.Tests only
./scripts.sh test-ai         # AI service only (ruff check --no-cache + pytest)
./scripts.sh coverage        # Identity + Ticket coverage → Cobertura report in TestResults/Report/
```

Run a single test:
```bash
dotnet test tests/IdentityService.Tests/ --filter "FullyQualifiedName~AuthServiceTests"
```

### Test suites

| Project | Tests | What's covered |
|---|---|---|
| `IdentityService.Tests` | ~127 | Auth (register/login/refresh/logout/profile), user CRUD + single-admin constraint, password hashing, JWT, validators, email lookup |
| `TicketService.Tests` | ~166 | Ticket CRUD, assignment/workflow, claim, access restriction, private comments + recipient subsets, outbox, KB articles |
| `NotificationService.Tests` | ~23 | Event processing, preferences, CRUD, SignalR, email delivery (run manually — no `scripts.sh` command) |
| `SearchService.Tests` | ~4 | Meilisearch filtering + result mapping |
| `ai-service` (pytest) | 105 | Chat, classifier, summarize, troubleshooting, similar-tickets, reindex, follow-up close, dedup, vector store, JWT |
| **Total** | **~425** | |

Counts are approximate and drift as tests are added.

### Gotchas

- **TFM mismatch**: test projects target **net9.0** while service projects target
  **net8.0**. `dotnet test` implicitly builds dependencies, so running tests requires the
  .NET 9 SDK (Dockerfiles use `dotnet/sdk:8.0`).
- **Root-owned caches**: docker builds leave root-owned `.ruff_cache/`/`.pytest_cache/`
  dirs, so plain `ruff check`/`pytest` on the host fail with permission errors. Use
  `./scripts.sh test-ai` (already passes `--no-cache`) or remove the caches with `sudo`.
- The first `test`/`test-ai` run creates the Python virtualenv at
  `services/ai-service/.venv` and installs dev dependencies.
- Notification tests must be run manually: `dotnet test tests/NotificationService.Tests/`.

## Developer commands

All commands go through `./scripts.sh`:

| Command | What it does |
|---|---|
| `./scripts.sh setup` | Generates RSA keys, creates `.env`, fills random service keys (keeps existing) |
| `./scripts.sh up` | `docker compose up --build -d` |
| `./scripts.sh down` | `docker compose down` |
| `./scripts.sh logs` | `docker compose logs -f` |
| `./scripts.sh frontend-dev` | `pnpm install` (if needed) + `pnpm dev` in `frontend/` |
| `./scripts.sh frontend-build` | `pnpm install` + `pnpm build` |
| `./scripts.sh test` | Identity + Ticket + Search xUnit + AI tests |
| `./scripts.sh test-identity` | Identity tests only |
| `./scripts.sh test-ticket` | Ticket tests only |
| `./scripts.sh test-search` | Search tests only |
| `./scripts.sh test-ai` | AI tests only (ruff + pytest) |
| `./scripts.sh coverage` | Tests + Cobertura coverage report |
| `./scripts.sh clean` | Remove `TestResults/` + `dotnet clean` |
| `./scripts.sh jenkins` | Start the Jenkins CI/CD controller (UI at http://localhost:8080) |
| `./scripts.sh help` | Show all commands |

Frontend notes: package manager is **pnpm** (never `npm install`). `next.config.mjs` uses
`output: "standalone"` and has TypeScript checking **enabled**, so `pnpm build` fails on type
errors; `pnpm lint` runs ESLint (flat config) in CI. `frontend-dev` runs Next outside Docker,
but the browser still hits the gateway — run `./scripts.sh up` first or every API call fails.

## CI/CD (Jenkins)

A declarative `Jenkinsfile` drives the pipeline. Jenkins runs in Docker
(`infra/jenkins/`) with a `docker:dind` sidecar — full setup guide:
[`infra/jenkins/README.md`](infra/jenkins/README.md).

- Every branch/PR: backend builds (all 5 services) + all 4 xUnit suites, AI build + tests,
  and a frontend build (`pnpm lint` + `pnpm build`).
- Only `main` and version tags: build & push the **7** images to GHCR
  (`ghcr.io/nicolasiskandar/helpdesk-platform-{name}`; `main` → `latest`, tags → tag name)
  and deploy the stack on the host.
- **Two daemons**: an isolated `docker:dind` sidecar handles build/test and image
  build/push; the **host Docker socket** is used only for the deploy step.
- The controller mounts `/opt/helpdesk-deploy` at the **same path** on host and controller
  (compose resolves bind sources against the host daemon's filesystem).
- Backend stages run **sequentially** (the shared `nuget-cache` volume isn't
  concurrency-safe); frontend and AI stages run in parallel.
- The repo is synced to the deploy workspace via `git archive` (committed tree only), the
  `.env` is restored from the base64-encoded `helpdesk-env` secret, and
  `infra/jenkins/deploy/remote-deploy.sh` validates required keys, regenerates missing RSA
  certs, and runs `docker compose up -d --no-build`.
- Jenkins credentials required: `ghcr` (write:packages), `ghcr-deploy` (read:packages),
  `helpdesk-env` (base64 `.env`).

## Troubleshooting & known gotchas

- **AI service not ready**: `/api/ai/health/ready` returns 503 until Ollama models are
  downloaded (background pull on first boot). Wait a few minutes; don't restart in a loop.
- **Slow container downloads / stalls**: the compose bridge network MTU is deliberately set
  to 1450 — the host's path MTU is < 1500. Don't remove it.
- **Slow `.NET` builds**: the service Dockerfiles share one NuGet cache via
  `RUN --mount=type=cache,target=/root/.nuget/packages`. Don't revert to plain
  `dotnet restore` (that's what made cold builds take ~15 min per service).
- **Ticket uploads volume permissions**: Docker initializes the `ticket-uploads` volume as
  `root:root`; the service `entrypoint.sh` chowns it before starting. Preserve that pattern
  if you modify the Dockerfile.
- **Missing EF migrations**: hand-written ticket migrations MUST carry
  `[DbContext(typeof(TicketDbContext))]` + `[Migration("timestamp_Name")]` attributes or EF
  silently skips them. If a migration isn't applied, run SQL manually via
  `docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd ...` or rebuild with
  `--no-cache`.
- **Search returns only one similar ticket**: the AI vector index only reacts to events going
  forward. After a Qdrant volume wipe, run `POST /api/ai/reindex-tickets` (Admin) to backfill
  from the ticket DB.
- **Notification DB schema changes**: the notification service uses `EnsureCreated()` in dev —
  schema changes need a manual `ALTER TABLE` (or volume recreate).
- **Identity service has no `appsettings.json`** — all runtime config comes from environment
  variables in `compose.yaml` (unlike ticket and gateway).
- **CORS is `AllowAll`** (any origin, any method) — dev-only behavior.
- `.env`, `infra/certs/`, and `AGENTS.md` are gitignored/local-only — never commit them.
  (`docs/` is tracked.)

## Stopping the stack

```bash
docker compose down
# Add -v to also remove database volumes:
# docker compose down -v
```
