# Jenkins CI/CD

The repo has a declarative `Jenkinsfile` at the root. Jenkins runs in Docker
(`infra/jenkins/docker-compose.yml`) with a `docker:dind` sidecar.

## Architecture

```
                  ┌─────────────────────────────── helpdesk-jenkins ───────────────────────────────┐
                  │                                                                                │
   Jenkins UI     │   jenkins (controller)              docker (dind sidecar)                       │
   :8080          │   ────────────────────────          ──────────────────────                     │
                  │   workspace /var/jenkins_home ─────► shares jenkins-data volume ◄── daemon can  │
                  │   DOCKER_HOST ───────────────► tcp://docker:2375            │   see workspace  │
                  │        │                                                        │              │
                  │   build/test stages: ephemeral containers on dind              │              │
                  │        │  dotnet/sdk:9.0, node:20-alpine (reuseNode)           │              │
                  │   image build/push: docker build/push → dind → GHCR            │              │
                  │        │                                                        │              │
                  │   Deploy: /var/run/docker.sock (host) ────► host docker daemon                │
                  │        └─ /opt/helpdesk-deploy  (same path on host & controller)                │
                  └────────────────────────────────────────────────────────────────────────────────┘
```

- **Controller** (`jenkins`): official image + docker CLI + compose plugin +
  preinstalled plugins (see `Dockerfile` / `plugins.txt`). Mounts the host
  Docker socket (for deploy) and `/opt/helpdesk-deploy:/opt/helpdesk-deploy`.
- **Sidecar** (`docker`): `docker:dind`, `privileged: true`, no TLS. Shares the
  `jenkins-data` volume so the controller's workspace is visible to the daemon —
  required for ephemeral agents and `docker build` to work.
- **Two daemons, two jobs**:
  - dind = build/test + image build/push (isolated, doesn't touch host images)
  - host socket = deploy only (`docker compose` against the host daemon)
- **GHCR**: images pushed to `ghcr.io/nicolasiskandar/helpdesk-platform-*`.
  `main` → `latest`; version tags (`v1.2.3`) → that tag.

## Pipeline

Every branch/PR: backend build (gateway + identity + ticket + notification),
all 3 xUnit test suites, frontend build.

`main` and version tags additionally:
1. Build & push the 5 images to GHCR (login with `ghcr` write token).
2. Deploy: sync the repo to `/opt/helpdesk-deploy`, restore `.env`, pull the
   images with the `ghcr-deploy` read token, and run
   `infra/jenkins/deploy/remote-deploy.sh <tag>` which does
   `docker compose -f compose.yaml -f infra/jenkins/deploy/docker-compose.images.yml pull && up -d --no-build`
   on the **host** daemon.

## Prerequisites

- Docker + Docker Compose v2 (with `docker-compose-plugin`)
- A GitHub account with GHCR access for the `nicolasiskandar/helpdesk-platform`
  packages, and two Personal Access Tokens:
  - **`ghcr`** — `write:packages`, `read:packages` (push images)
  - **`ghcr-deploy`** — `read:packages` only (pull images on deploy)
- A copy of your `infra/.env.example`-derived `.env` (for the `helpdesk-env`
  credential; this is how the pipeline injects DB/JWT secrets at deploy time).

## 1. Start Jenkins

```bash
./scripts.sh jenkins        # = docker compose -f infra/jenkins/docker-compose.yml up --build -d
```

First boot builds the controller image and starts both containers.

## 2. First-run setup

```bash
docker exec jenkins cat /var/jenkins_home/secrets/initialAdminPassword
```

Open http://localhost:8080, enter the password, create an admin user. The
required plugins are already installed — skip the "Install suggested plugins"
screen (choose "Select plugins to install" → nothing extra needed, or just
continue; extra plugins are harmless).

## 3. Configure credentials

In Jenkins → **Manage Jenkins → Credentials → (global)** add:

| ID | Kind | Values |
|----|------|--------|
| `ghcr` | Username with password | Username = GitHub user, Password = PAT with `write:packages` |
| `ghcr-deploy` | Username with password | Username = GitHub user, Password = PAT with `read:packages` |
| `helpdesk-env` | Secret text | The full contents of your local `.env` file |

## 4. Create the pipeline job

1. **New Item** → name it (e.g. `helpdesk-platform`) → **Multibranch Pipeline**.
2. Under **Branch Sources → Add source → Git**: repo URL
   `https://github.com/nicolasiskandar/helpdesk-platform.git` (or your fork).
3. **Discover branches** (all branches) and **Discover tags** (all tags).
4. **Build Configuration**: Mode = "by Jenkinsfile", Script Path = `Jenkinsfile`.
5. Save. Scan triggers run on push; each branch + tag gets its own job run.

> A plain **Pipeline** job also works if you don't need branch/tag indexing:
> Pipeline → SCM → Git → same repo, Script Path `Jenkinsfile`. In that mode
> `main`-only logic relies on the checked-out branch name matching `main`.

## Deploy details

- Deploy syncs the **committed** repo tree with `git archive` to
  `/opt/helpdesk-deploy` — the deploy workspace is mounted at the **same path**
  on host and controller, so the host daemon resolves compose's `./infra/...`
  bind sources to the real files (a path mismatch made the daemon auto-create
  empty dirs instead). Not a raw tar of the workspace. This keeps build cruft
  (`node_modules`, `.pnpm-store`, `bin`, `obj`, `.next`, `@tmp` dirs) out of the
  deploy dir and is fast enough not to trip the durable-task watchdog.
  Gitignored files (`.env`, `infra/certs`) are intentionally absent: `.env` is
  written from the `helpdesk-env` credential and certs are generated by
  `remote-deploy.sh`.
- The image-only override lives at
  `infra/jenkins/deploy/docker-compose.images.yml` (services reference
  `ghcr.io/nicolasiskandar/helpdesk-platform-*:<tag>` instead of `build:`).
  It is intentionally named so it isn't picked up by gitignore (`docker-compose.override.yml` is ignored) and is **not** used by local dev.
- `remote-deploy.sh` forces `DOCKER_HOST=unix:///var/run/docker.sock` so the
  stack is deployed on the **host** daemon (the controller's default
  `DOCKER_HOST` points at dind).
- `remote-deploy.sh` sets `COMPOSE_PROJECT_NAME=helpdesk-platform` so the
  deploy **reconciles** the existing stack instead of fighting it: the old
  locally-started stack owns the `helpdesk-*` container names and the
  `helpdesk-platform_*` data volumes, so `up -d --no-build` recreates the 5 app
  services with the new GHCR images and restarts the unchanged infra
  containers, keeping existing DB data. (A fresh project name would fail with
  `container name already in use` and start empty volumes.)
- Ports/URLs are the same as the dev stack (5000, 5010, 5011, 3000, …) — a
  deploy replaces whatever is running on the host.
- `infra/certs/` is gitignored and therefore absent from the deploy checkout.
  `remote-deploy.sh` generates an RSA keypair there if missing (one-time; the
  files persist on the host). Fresh keys invalidate existing JWT sessions once.
- **`helpdesk-env` credential must hold a BASE64-encoded `.env`.** Jenkins'
  "Secret text" Secret field is a single-line input, so pasting a raw
  multi-line `.env` strips every newline (the whole file ends up on one line
  and compose can't read it). Store the output of `base64 -w 0 .env` instead;
  the Deploy stage decodes it before deploying.

## Troubleshooting

- **`docker build` context path not found** — the dind sidecar must be running
  and sharing the `jenkins-data` volume with the controller
  (`docker compose -f infra/jenkins/docker-compose.yml ps`).
- **`docker: command not found`** in a stage — the controller image wasn't
  rebuilt with the docker CLI; run `./scripts.sh jenkins` again (it uses
  `--build`).
- **Deploy fails: `.env` is missing** — create the `helpdesk-env` secret-text
  credential with your `.env` contents.
- **Deploy fails with `ERROR: .env is missing '<VAR>='`** — the `helpdesk-env`
  credential is empty, stale, or its newlines were stripped by the single-line
  Secret field. Update it with the base64 encoding of your full `.env`
  (`base64 -w 0 .env`) and re-run.
- **`wrapper script does not seem to be touching the log file` / exit code -1** —
  the durable-task watchdog killed a shell step that ran long with no output.
  The controller's `JAVA_OPTS` now raises the heartbeat interval
  (`-Dorg.jenkinsci.plugins.durabletask.BourneShellScript.HEARTBEAT_CHECK_INTERVAL=86400`),
  and the deploy sync uses `git archive` (fast, no build cruft). If it recurs,
  check the stage's step isn't silently archiving a large workspace dir.
- **`unauthorized to access repository` on push** — refresh the `ghcr` PAT with
  `write:packages`; on pull, the `ghcr-deploy` PAT needs `read:packages`.
- **Plugins missing** — plugins are declared in `plugins.txt`; add new ones
  there and restart the controller (`docker compose -f ... restart jenkins`).
- **`docker push` fails with `timeout awaiting response headers`** — transient
  GHCR network stall on slow/flaky links. The pipeline now builds all images
  first and pushes each with automatic retries (4 attempts), so usually a
  re-run just works. Persistent failures on the same layer suggest a network
 /MTU problem — try raising the dind MTU (`--mtu=1400`) or run
  `./scripts.sh jenkins` to rebuild the controller.
- **Deploy fails with `rm: cannot remove '/opt/helpdesk-deploy': Device or
  resource busy`** — `/opt/helpdesk-deploy` is a bind mount; you cannot delete
  the mount point itself from inside the container. The Deploy stage now clears
  the directory contents only
  (`find "$DEPLOY_DIR" -mindepth 1 -maxdepth 1 -exec rm -rf {} +`), so this
  should not recur.
- **`Could not find file '/root/.nuget/packages/.../*.nnk'` during restore** —
  NuGet's global-packages folder is not safe for concurrent extraction. This
  happens when two `dotnet restore`s of the same package run at once (the
  backend stages used to run in parallel sharing the `nuget-cache` volume).
  The pipeline now runs the backend stages **sequentially** (only the frontend
  runs in parallel), so this should not recur. If a cache is already corrupted,
  wipe it and let it be recreated fresh:
  `docker exec jenkins-docker docker volume rm nuget-cache`.
- **Caches** — NuGet packages, the pnpm store, and corepack downloads are cached
  in named volumes on the dind sidecar (`nuget-cache`, `pnpm-store`,
  `corepack-cache`), and dind's `/var/lib/docker` lives on
  `jenkins-dind-storage`, so caches and pulled base images survive daemon
  restarts. `docker compose down -v` wipes them.
