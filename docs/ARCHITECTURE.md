# IT Help Desk & Ticketing Management System — Architecture Reference

This document is the single source of truth for the backend architecture. It is written to be
read by humans and by AI coding agents working on any part of this system. If you are an AI
agent picking up a task, read this fully before writing code — it explains not just *what*
exists but *why*, so you don't accidentally violate a boundary that isn't obvious from the code
alone.

## 1. System overview

A microservices-based IT Help Desk application. Employees submit tickets, IT agents resolve
them, admins manage the system, and an AI assistant helps deflect and triage issues before and
after a ticket is created.

**Backend stack**: C# / ASP.NET Core Web API for all business services. Python / FastAPI for the
AI service. RabbitMQ for async messaging. SQL Server and PostgreSQL for relational storage
(split per service — see §3). A vector database for the AI service's semantic search.

**Core architectural principle**: each service owns its data exclusively. No service queries
another service's database directly. Cross-service knowledge is exchanged either via
synchronous REST calls (only when an immediate response is required) or via events published to
RabbitMQ. This is what makes the services independently deployable and independently
understandable — an agent working on Notification service never needs to know Ticket service's
schema.

## 2. Services

| Service | Language/Framework | Database | Responsibility |
|---|---|---|---|
| API Gateway | .NET (YARP) | — | Single entry point, routes requests, validates JWTs |
| Identity Service | ASP.NET Core | SQL Server | Authentication, authorization, user/role management |
| Ticket Service | ASP.NET Core | SQL Server | Ticket CRUD, assignment, workflow, audit trail, admin dashboard/reporting |
| Notification Service | ASP.NET Core | PostgreSQL | In-app + email notifications, delivery tracking |
| AI Service | Python / FastAPI | Vector DB (embeddings) | RAG-based chat, similarity search, categorization, priority suggestion, suggested replies |
| Search Service | .NET / ASP.NET Core | Meilisearch | Keyword/full-text search over closed tickets, complements AI's semantic search |

### Why these boundaries and not others

Assignment and workflow live **inside** Ticket service rather than as a separate service. They
share a transaction boundary with the ticket itself (e.g. "assign this ticket to this agent"
needs to be atomic), and splitting them would force a distributed-transaction/saga pattern to
simulate what a single DB transaction gives for free. Not worth the complexity here.

AI service is separate because it is a different language/runtime (Python, not .NET) — a forced,
legitimate boundary, not a stylistic one.

Notification service is separate because it is fundamentally event-driven and asynchronous by
nature, and decoupling it means Ticket service never blocks on notification delivery.

The admin dashboard is **not** a separate service. It's a reporting module inside Ticket
service that aggregates its own data plus read-calls to Identity (user/role counts) and
Notification (delivery stats). If cross-service aggregation ever gets expensive, that's the
signal to extract a dedicated read-model/reporting service — don't build that up front.

## 3. Data ownership

Each service has its own database. No foreign keys cross service boundaries — a `UserId` field
in Ticket service's `Tickets` table is a plain GUID, not a foreign key into Identity's `Users`
table. Referential integrity across services is eventual, not enforced at the DB level.

### Identity Service (SQL Server)
- `Roles` — Admin, IT Support Agent, Employee, Manager
- `Users` — email, password hash, full name, role, active flag
- `RefreshTokens` — supports JWT rotation
- `UserActivityLogs` — login/activity audit trail

**JWT claims**: The access token includes `ClaimTypes.Name` (user's full name) in addition to
`sub`, `email`, `role`, `jti`, `iss`, `aud`. Downstream services extract the name claim for
audit logging (e.g. "Assigned to John Smith" instead of "Assigned agent {guid}").

**User management**: Full CRUD at `GET/POST /api/users`, `GET/PUT/DELETE /api/users/{id}` with
search, pagination, role/status filtering. `GET /api/users` requires any authenticated user.
`GET /api/users/{id}` requires Admin or Manager. Write operations require Admin.
Profile self-service at `PUT /api/auth/me` and
`POST /api/auth/change-password`.

**Single admin constraint**: Only one user with `RoleId == 1` (Admin) is allowed. The system
rejects creating a second Admin or promoting a user to Admin if one already exists.

### Ticket Service (SQL Server)
- `Categories`, `Priorities`, `Statuses` — lookup tables
- `Tickets` — core entity: reference number, title, description, category, priority, status, creator
- `TicketAssignments` — many-to-many, since multiple agents can be assigned to one ticket
- `TicketComments` — public or private (`IsPrivate`). Private comments are visible only to the ticket creator, currently assigned agent(s), and admin. The DB column remains `IsInternal`; mapped via `HasColumnName`.
- `TicketAttachments` — file metadata (actual files stored on disk via `LocalFileStorageService` at `uploads/{ticketId}/{guid}_{filename}`)
- `TicketAuditLog` — append-only. Every field change is recorded with who changed it, from
  what value, to what value, and when. Critically, `ChangedByType` distinguishes `User` from
  `AI` — this is how the system records that the AI assistant closed a ticket, not a human.
- `Outbox` — see §4, transactional outbox pattern

### Notification Service (PostgreSQL)
- `Notifications` — recipient, type, title, message, related ticket ID (no FK), read state
- `NotificationDeliveries` — per-channel delivery tracking (email/in-app), status
- `NotificationPreferences` — per-user opt-in/opt-out per channel

### AI Service
Not relational in the traditional sense — holds a vector store of indexed ticket content
(title + description + resolution notes) plus KB articles, used for semantic similarity search
and RAG grounding. Populated via events, not by querying Ticket service's database (see §5).

Full ERDs for all three relational services are maintained in `IT_Help_Desk_Diagrams.drawio`
alongside this document.

## 4. Inter-service communication

Two patterns, used deliberately for different reasons. Don't default to one pattern everywhere.

### Synchronous REST — only when an immediate response is required
- Frontend → Gateway → Identity/Ticket/Notification for normal CRUD operations.
- Ticket Service → AI Service, for `/analyze` (category + priority suggestion at ticket
  creation) — short timeout (~1-2s), and if AI is slow/unavailable the ticket is still created
  (`Uncategorized` / `Medium`), never blocked on AI availability.
- Frontend → AI Service directly, for chat, suggested replies, and similarity search. These
  bypass Ticket Service entirely — they're read-only interactions that don't need to go through
  the ticket's owning service.

### Asynchronous events via RabbitMQ — everything else
- **One topic exchange**: `ticket.events`. A topic exchange (not one exchange per event type)
  allows routing by pattern without proliferating exchanges.
- **Routing keys**: `ticket.created`, `ticket.assigned`, `ticket.resolved`, `ticket.status_changed`,
  `ticket.unassigned`, etc. — one per domain event Ticket service publishes.
- **Each consumer owns its own durable queue**, bound with whatever routing pattern it needs:
  - Notification service's queue binds broadly (`ticket.*`) since it reacts to most events.
  - AI service's indexing queue binds `ticket.created`, `ticket.resolved`,
    `ticket.commented`, and `ticket.status_changed` — it needs to index ticket content
    (plus published KB articles) but ignores purely assignment-related events.
- **Competing consumers**: if a service scales to multiple replicas, they all pull from the same
  queue and RabbitMQ load-balances between them automatically. No special code needed for this —
  it's inherent to queue-based consumption.

### The transactional outbox pattern (mandatory for Ticket service writes)
Ticket service never publishes an event as a separate step after committing a database write.
That creates a window where the DB write succeeds but the publish fails (process crash, network
blip), leaving the rest of the system unaware a ticket was ever created.

Instead: every write that needs to notify the rest of the system inserts a row into an `Outbox`
table **in the same database transaction** as the business write (ticket insert, audit log
insert, outbox insert — one transaction, one commit). A background poller separately reads
undispatched outbox rows and publishes them to the RabbitMQ exchange, then marks them
dispatched. This guarantees at-least-once delivery of every event that was ever committed.

**Any new write path in Ticket service that needs to raise a domain event must follow this
pattern.** Do not call the RabbitMQ client directly from inside a request handler.

## 5. How the AI service gets its data

AI service never queries Ticket service's database. It subscribes to `ticket.created` and
`ticket.resolved` events and indexes the relevant content (title, description, resolution notes)
into its own vector store. This keeps AI service fully decoupled — Ticket service does not know
or care that AI indexing exists, it just publishes domain events it already needs to publish for
other reasons.

This same principle is what makes the Meilisearch-backed search service a clean bolt-on: it
subscribes to the same events independently, with no changes required to Ticket service or to AI
service.

## 6. Core workflows

### 6.1 Ticket creation (with AI pre-check)
1. Employee describes their issue to the AI chat/search entry point
2. AI service runs a similarity search against previously resolved tickets.
3. If a strong match is found, the employee is shown the existing resolution and can self-serve
   without creating a ticket.
4. If no match, or the employee proceeds anyway, Ticket service:
   - Validates required fields server-side.
   - Calls AI service's `/analyze` endpoint (sync, with fallback) for category + priority.
   - Generates a reference number via a DB sequence (never a "count + 1" query — race condition
     risk).
   - Commits `Tickets` insert + `TicketAuditLog` insert + `Outbox` insert in a single
     transaction.
5. Outbox poller publishes `TicketCreated`.
6. Notification service alerts the relevant IT queue/agent. AI service indexes the new ticket.

### 6.2 Assignment and multi-agent resolution
1. One or more agents are assigned to a ticket (`TicketAssignments` supports many-to-many).
2. Any authenticated user can **pick up (self-assign)** an open unassigned ticket via `POST /tickets/{ticketId}/claim`. This validates the ticket is Open with no active assignment, creates the assignment, transitions to In Progress, and publishes `ticket.assigned` + `ticket.status_changed` outbox events — all atomically.
3. Admin or Manager can assign/reassign agents via the ticket detail dropdown (`POST /tickets/{ticketId}/assign`). Unassigning is done via `POST /tickets/{ticketId}/unassign`.
4. When one agent resolves the ticket, Ticket service updates status, writes the audit log
   entry, and publishes `TicketResolved` with the list of other still-active assignees.
5. Notification service consumes this and notifies every other assigned agent that the ticket
   was resolved and no further action is needed.

**Design rule**: Ticket service decides *who* needs notifying (it owns that domain knowledge).
Notification service only decides *how* (email vs in-app vs both). Notification service must
never reach into Ticket service's data to figure out who else was assigned.

### 6.3 AI follow-up and auto-close
This is triggered once a ticket reaches a new status: `Resolved – Pending Confirmation` (set by
an agent, or directly by the AI assistant if it resolved the issue itself).

1. AI service's chatbot follows up with the employee (in-app or via notification) asking if the
   issue is actually resolved.
2. If the employee confirms: AI service calls `PATCH /tickets/{id}/status` on Ticket service,
   authenticated with a distinct **AI service identity** — a token scoped to *only* perform this
   one transition (`Resolved – Pending Confirmation` → `Closed`), nothing broader. Ticket
   service writes the audit log entry attributed to `AI Assistant`, not the employee — this
   matters for anyone reviewing ticket history later.
3. If the employee says it's not resolved: the ticket reopens to `In Progress` and reassigns to
   the original agent, with a notification.

**Security note for any agent touching AI service's auth**: AI service's write access to Ticket
service must remain tightly scoped to this one status transition. It should never be given
general ticket-edit permissions — a misbehaving or hallucinating AI response has a much smaller
blast radius if it can only flip one specific status field under a specific precondition.

### 6.4 Self-service resolution without a ticket
If the AI chatbot resolves an employee's issue before any ticket is created, a lightweight
ticket record should still be created directly in `Resolved by AI` status. This preserves
visibility into AI deflection rate for the admin dashboard — don't let purely-AI-resolved issues
go untracked.

### 6.5 Ticket deletion
Only open tickets can be deleted. The authorization rules are:
- **Admin** can delete any open ticket.
- **Ticket Creator** can delete their own open ticket.
- **Manager** cannot delete tickets (read/reporting role only).
- **IT Support Agent** cannot delete tickets unless they are also the ticket Creator.

Deletion cascades to all related data in the same transaction: comments, attachments, audit log
entries, and assignments. A `TicketDeleted` domain event is published via the outbox pattern so
subscribers (AI indexing, search indexing) can purge the ticket from their stores.

## 7. AI service — feature surface

| Feature | Caller | Pattern | Notes |
|---|---|---|---|
| Categorization + priority | Ticket service | Sync, combined into one `/analyze` call | Priority uses a hybrid approach — see below |
| Suggested replies | Frontend, direct | Sync | Agent-facing, RAG over KB + similar resolved tickets |
| Chat assistant | Frontend, direct | Sync, streaming | Employee-facing, pre- and post-ticket |
| Similarity search | Frontend, direct | Sync | Powers the pre-ticket "does this already exist" check |

**Priority suggestion is not pure LLM output.** A cheap rule-based layer runs first — a
keyword/regex matcher for known-critical terms (server down, outage, security breach, data
loss, VPN down for the entire office) that force-sets `Critical` regardless of what the model
says. The LLM is only deferred to for the ambiguous middle ground. This exists because
miscategorized priority has real operational consequences, and a pure-LLM classifier is a single
point of failure for the highest-stakes classification in the system. Any agent modifying
priority logic must preserve this rule-based override layer.

**The chat assistant should be skippable, not mandatory.** Always surface the AI's best guess at
similar tickets/answers, but let the employee proceed to manual ticket creation immediately if
nothing fits — don't force a chat flow before ticket creation is allowed.

**Ollama model usage**: start with a single general-purpose local model for all four features.
Only split into separate model profiles (a small fast model for `/analyze`, a larger one for
chat/RAG) if classification latency becomes a problem in practice — this is a tuning decision,
not an architectural one, and should not be over-engineered up front.

## 8. Authentication and authorization

- API Gateway validates the user's JWT once, on the way in.
- Downstream services do not re-validate against Identity service on every call. Instead, the
  validated JWT is passed downstream and each service validates it locally against a shared
  signing key / JWKS endpoint.
- AI service holds a **separate, narrowly scoped service identity** for its one write operation
  (closing a ticket after employee confirmation, §6.3). This is not the same credential as its
  general read access to indexed ticket content.

## 9. Roles

| Role | Permissions |
|---|---|
| Admin | Full system access — manage users, delete any open ticket, system configuration, see all private comments |
| IT Support Agent | Manage and resolve assigned tickets, view all tickets, pick up open unassigned tickets, create private comments on assigned tickets |
| Employee | Create and track own tickets, pick up open unassigned tickets, delete own open tickets, create private comments on own tickets |
| Manager | Monitor team tickets and reports, assign/reassign agents, view agent workload stats (cannot delete tickets, cannot see private comments unless assigned) |

**Single admin constraint**: Only one Admin user is allowed in the system. The backend rejects
creating a second Admin or promoting a user to Admin if one already exists. An existing Admin
can change their own role away from Admin, but cannot be overwritten by another Admin promotion.

**Ticket deletion policy**: Only Admin or the ticket Creator can delete a ticket, and only when
the ticket status is Open. Managers — despite having broader read/reporting access — are
explicitly excluded from deletion. This prevents accidental or malicious data loss while
preserving the Manager role's read-only oversight function.

**Self-assignment (ticket pickup)**: Any authenticated role can pick up an open unassigned
ticket via `POST /tickets/{ticketId}/claim`. This is used by the Ticket Queue page in the
frontend, which shows all open unassigned tickets with a "Pick Up" button.

**Agent workload**: Admin and Manager can view per-agent open/resolved ticket counts via
`GET /tickets/agent-workload`. The frontend exposes this as the Team Workload page at
`/admin/team-workload`.

**Profile self-service**: All authenticated users can update their own name and email via
`PUT /api/auth/me` and change their password via `POST /api/auth/change-password` (requires
current password verification).

## 10. Search service — Meilisearch projection

The Search Service is a .NET 8 HTTP service backed by Meilisearch

### 10.1 Indexing

The service owns durable queue `search-index.q`, bound to `ticket.resolved`,
`ticket.status_changed`, and `ticket.deleted` on `ticket.events`. Only a `ticket.resolved` event
whose `ResolvedStatusName` is `Closed` is indexed. Its document uses the ticket ID as the primary
key and contains the reference number, title, description, category, priority, and close time.

`ticket.status_changed` removes a document when a ticket leaves `Closed`, and `ticket.deleted`
removes it as a final safety measure. Private comments, attachments, and user data are never
placed in the Meilisearch index. The consumer waits for every Meilisearch task to succeed before
acknowledging RabbitMQ; primary-key upserts and deletes are idempotent under redelivery.

### 10.2 Query API

Gateway routes authenticated `GET /api/search/tickets` requests to Search Service. The endpoint
accepts `q`, `category`, `priority`, `from`, `to`, `page`, and `pageSize` (1–100), and returns
paginated closed-ticket results with a 300-character description excerpt. Meilisearch handles
full-text matching, typo tolerance, field priority, filtering, and newest-first sorting.

Semantic similarity remains `POST /api/ai/similar-tickets`; Search Service is a separate keyword
search capability and is never a dependency of ticket creation or AI features.

### 10.3 Frontend global search

The topbar renders a `GlobalSearch` box (`frontend/components/global-search.tsx`, desktop-only
at the `lg` breakpoint) that fires a debounced (300ms) combined search once the query is ≥2
characters: it calls `GET /api/search/tickets` (closed tickets via Search Service) and
`GET /api/kb-articles?search=…&pageSize=5` (published KB articles), then shows a grouped
Tickets/Articles dropdown with keyboard navigation. Selecting a result routes to
`/tickets/{id}` or `/knowledge-base?article={id}`. The dropdown is anchored to the input via
base-ui's `anchor` prop with `initialFocus={false}` so focus stays in the search box while
typing — it must never wrap the input in a `Popover.Trigger` (that injects `type="button"` and
prevents focus, breaking typing). Clicking outside closes the dropdown but preserves the query.

## 11. Design principles to preserve

These are the load-bearing decisions in this system. An AI agent or new team member changing
code in a way that violates one of these should stop and flag it rather than proceeding silently:

1. **No service queries another service's database.** Cross-service data access is either a
   scoped sync REST call or an event subscription — never a direct DB connection.
2. **Every Ticket service write that other services need to know about goes through the outbox
   pattern.** Never publish directly from a request handler.
3. **AI service's write access to ticket data is minimal and scoped.** It can transition a
   ticket from pending-confirmation to closed and nothing else.
4. **Priority classification keeps its rule-based override layer.** Don't replace it with a pure
   LLM call.
5. **Ticket creation must never block on AI service availability.** Fallback to
   `Uncategorized`/`Medium` and continue.
6. **Assignment/workflow stays inside Ticket service.** Don't split it into a separate service
   later without a concrete reason tied to independent scaling — coupling it to a distributed
   transaction pattern is a net loss for a system this size.
   dependency for ticket creation or AI semantic similarity.

## 12. CI/CD & deployment

A declarative `Jenkinsfile` at the repo root drives the pipeline. Jenkins runs in Docker
(`infra/jenkins/`) with a `docker:dind` sidecar — the full setup guide is in
`infra/jenkins/README.md`, and `./scripts.sh jenkins` starts the controller.

### Topology

Two separate Docker daemons are used, with deliberately disjoint responsibilities:

- **DinD sidecar** (`tcp://docker:2375`): isolated CI daemon. Build/test stages run on
  ephemeral Docker agents (`dotnet/sdk:9.0` for backend — tests target net9.0 while services
  target net8.0 — `node:20-alpine` for frontend, `python:3.12-slim` for AI). Image build &
  push to GHCR also run here.
- **Host daemon** (`unix:///var/run/docker.sock`): used **only** for the deploy step, mounted
  into the controller. The deploy runs `docker compose` against the host's actual stack.

Build/test agents share named cache volumes (`nuget-cache`, `pnpm-store`, `corepack-cache`) so
dependency downloads survive between runs. Backend stages run **sequentially** (the shared
`nuget-cache` isn't concurrency-safe); the frontend and AI stages run in parallel.

### Image publishing

Only `main` and version tags build & push the 7 images to
`ghcr.io/nicolasiskandar/helpdesk-platform-{gateway,identity-service,ticket-service,notification-service,ai-service,search-service,frontend}`.
`main` → `latest`; version tags → the tag name. Pushing requires the `ghcr` credential
(write:packages).

### Deploy flow (main + tags only)

1. **Sync**: the committed tree is synced to `/opt/helpdesk-deploy` via `git archive` (only
   committed files — no `node_modules`, build artifacts, or `.git`).
2. **Restore `.env`**: the `helpdesk-env` credential holds the **base64-encoded** `.env`
   (Jenkins' secret-text field is single-line, so newlines are stripped). The pipeline runs
   `base64 -d`, verifies the 6 required keys, and exits early if any are missing.
3. **Pull images**: `docker compose pull` with the `ghcr-deploy` credential (read:packages
   only).
4. **Deploy**: `infra/jenkins/deploy/remote-deploy.sh <tag>` sets
   `COMPOSE_PROJECT_NAME=helpdesk-platform`, forces `DOCKER_HOST=unix:///var/run/docker.sock`,
   regenerates missing RSA certs (`infra/certs/` is gitignored and absent from the checkout),
   and runs `docker compose -f compose.yaml -f infra/jenkins/deploy/docker-compose.images.yml
   up -d --no-build`.

### Why the same-path bind mount

The controller mounts `/opt/helpdesk-deploy:/opt/helpdesk-deploy` — the **same path** on host
and controller. Compose resolves relative bind sources (`./infra/prometheus/prometheus.yml`,
etc.) against the *host* daemon's filesystem, so the deploy checkout must live at a path the
host daemon can see. Any other host/controller path pairing (e.g.
`~/helpdesk-deploy:/opt/helpdesk-deploy`) makes the host daemon resolve an empty or wrong
directory and silently mount it into containers.

### Secrets

- `.env` and `infra/certs/` are gitignored; they are injected/regenerated at deploy time, never
  stored in the repository.
- Jenkins credentials required: `ghcr` (write:packages), `ghcr-deploy` (read:packages),
  `helpdesk-env` (base64 `.env`).
