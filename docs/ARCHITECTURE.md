# IT Help Desk & Ticketing Management System — Architecture Reference

This document is a condensed architecture overview. **The single source of truth is
[`README.md`](../README.md)** — read its "Architecture at a glance", "Messaging (RabbitMQ)",
"AI service", "Search", "Access restrictions", and "CI/CD (Jenkins)" sections for the full
details. Full ERDs for the relational services live in
[`IT_Help_Desk_Diagrams.drawio`](IT_Help_Desk_Diagrams.drawio).

## System overview

A microservices-based IT Help Desk. Employees submit tickets, IT agents resolve them, admins
manage the system, and an AI assistant helps deflect, triage, summarize, and suggest
troubleshooting steps.

**Core architectural principle**: each service owns its data exclusively. No service queries
another service's database directly. Cross-service knowledge is exchanged either via
synchronous REST (only when an immediate response is required) or via events published to
RabbitMQ. This keeps services independently deployable and independently understandable.

## Services

| Service | Framework | Datastore | Direct port | Responsibility |
|---|---|---|---|---|
| API Gateway | .NET 8 (YARP) | — | 5000 | Single entry point, routing, JWT validation |
| Identity Service | .NET 8 Web API | SQL Server | 5010 | Auth, user/role management, settings |
| Ticket Service | .NET 8 Web API | SQL Server | 5011 | Ticket CRUD, assignment, workflow, KB articles, reporting |
| Notification Service | .NET 8 Web API | PostgreSQL | 5012 | In-app (SignalR) + email notifications |
| AI Service | Python 3.12 / FastAPI | Qdrant + Ollama + SQLite | 5090 | RAG chat, triage, summarization, troubleshooting |
| Search Service | .NET 8 Web API | Meilisearch | 5013 | Keyword search over closed tickets |

Identity, Ticket, and Notification follow Clean Architecture (`Domain` → `Application` →
`Infrastructure` → `Api`).

### Why these boundaries

- Assignment and workflow stay **inside** Ticket service — they share a transaction boundary
  with the ticket; splitting them would force a distributed-transaction/saga pattern.
- AI is separate because it is a different language/runtime (Python).
- Notification is separate because it is event-driven and asynchronous — Ticket never blocks
  on notification delivery.
- The admin dashboard/reporting is **not** a separate service — it's a module inside Ticket
  that aggregates its own data plus scoped read-calls to Identity and Notification.

## Data ownership

Each service has its own database. A `UserId` in another service's tables is a plain GUID,
never a foreign key into Identity's `Users`. Referential integrity across services is
eventual, not DB-enforced. Per-service schemas: see README "Data model" and the drawio ERDs.

## Inter-service communication

Two deliberate patterns:

1. **Synchronous REST** — only when an immediate response is required (frontend → gateway →
   services; Ticket → AI `/analyze` with fallback; frontend → AI chat/summarize/troubleshoot).
2. **Asynchronous events via RabbitMQ** — everything else. One topic exchange `ticket.events`;
   routing keys `ticket.created`, `ticket.assigned`, `ticket.resolved`,
   `ticket.status_changed`, `ticket.deleted`, `ticket.unassigned`, `ticket.commented`, etc.
   Each consumer owns its own durable queue bound to the patterns it needs.

**Transactional outbox (mandatory for Ticket writes)**: every domain event is inserted into
the `Outbox` table in the same DB transaction as the business write; a background poller
publishes to RabbitMQ. This guarantees at-least-once delivery. Messages set `MessageId` to the
outbox row GUID; consumers must deduplicate. Failed messages retry up to `Outbox:MaxRetries`
(default 5) then move to the `ticket.events.dlx` dead-letter queue. Never publish directly
from a request handler.

## Core workflows (summary)

- **Ticket creation**: description → AI similarity check (self-service if a strong match) →
  optional AI `/analyze` for category/priority (never blocking) → single transaction
  (Tickets + audit + outbox) → events consumed by Notification + AI indexing.
- **Assignment / pickup**: Admin/Manager assign agents (many-to-many); any authenticated user
  can claim an open unassigned ticket (`POST /api/tickets/{id}/claim`), atomically creating
  the assignment and moving the ticket to In Progress.
- **AI follow-up / auto-close**: when a ticket reaches `Resolved - Pending Confirmation`, the
  AI chat follows up; on confirmation it calls the single scoped write
  (`POST /api/ai/confirm-resolved` → `PATCH /api/tickets/{id}/status` with `X-AI-Service-Key`)
  to move it 3→4 (`Closed`). Audit entries record `ChangedByType = AI`.
- **Deletion**: only Admin or the ticket Creator, and only when Open. Cascades to comments,
  attachments, audit entries, and assignments; publishes `ticket.deleted`.

## Search service — Meilisearch projection

A .NET 8 service that keeps a Meilisearch index of **closed** tickets. Consumes
`ticket.resolved` (indexes only when the terminal status is `Closed`), `ticket.status_changed`
(remove on leaving `Closed`), and `ticket.deleted`; waits for each Meilisearch task before
acking. Keyword search over the index complements AI semantic similarity
(`POST /api/ai/similar-tickets`). It is optional and never blocks ticket creation.

## Design rules (non-negotiable)

1. **No service queries another service's database.**
2. **Ticket service writes use the transactional outbox pattern.**
3. **AI service's write access is minimal** — only `Resolved - Pending Confirmation` →
   `Closed` via `X-AI-Service-Key`.
4. **Priority classification keeps its rule-based override layer** (no pure LLM).
5. **Ticket creation never blocks on AI availability** (fallback `Uncategorized`/`Medium`).
6. **Assignment/workflow stays inside Ticket service.**
7. **Search service is optional and never blocks ticket creation.**

## Authentication & authorization

- The Gateway validates the user's JWT once; downstream services validate locally against the
  shared RSA signing key (`/.well-known/jwks.json`). JWT is RS256, asymmetric, and includes
  `ClaimTypes.Name` (full name) for audit logging.
- AI service holds a separate, narrowly scoped credential (`AI_SERVICE_KEY`) for its one write
  operation only.
- Identity auth endpoints are IP rate-limited (429 + `Retry-After`); no login lockout.
- Roles: Admin (1), IT Support Agent (2), Employee (3), Manager (4). Single-admin constraint.

## Observability

OTLP gRPC → OTel Collector → Jaeger (traces) + Prometheus (metrics) → Grafana dashboards.
W3C `traceparent`/`tracestate` propagate across services (gateway transforms + RabbitMQ
message headers). Serilog structured logging with correlation IDs (`X-Correlation-ID`).
Readiness endpoints per service are listed in README "Observability".

## CI/CD & deployment

Jenkins (Docker-in-Docker) drives the pipeline — see README "CI/CD (Jenkins)" and
[`infra/jenkins/README.md`](../infra/jenkins/README.md). Two daemons: an isolated `docker:dind`
sidecar for build/test + image push to GHCR; the host Docker socket only for the deploy step.
Only `main` and version tags build & push the 7 images and deploy.
