#!/usr/bin/env bash
# Deploy the helpdesk stack from prebuilt GHCR images.
#
# Intended to run from inside the Jenkins controller container, which mounts:
#   /var/run/docker.sock    -> host Docker socket (target daemon for the deploy)
#   /opt/helpdesk-deploy    -> full repo checkout, at the SAME path on host and
#                              controller (compose resolves ./infra/... bind
#                              sources against the project dir, so the paths
#                              must match for the host daemon to see the files)
#
# The controller's default DOCKER_HOST points at the dind sidecar, so we force
# the host socket here. Override with DOCKER_HOST=... if deploying elsewhere.
#
# Usage:
#   remote-deploy.sh <image-tag>
set -euo pipefail

export DOCKER_HOST="${DOCKER_HOST:-unix:///var/run/docker.sock}"

# Reuse the original project name so `up -d` reconciles the existing stack in
# place. The old locally-started stack (project 'helpdesk-platform') still owns
# the helpdesk-* container names and the helpdesk-platform_* data volumes; a new
# project (e.g. 'helpdesk-deploy' from this directory's name) would conflict on
# the container names and create fresh empty volumes. Reusing the project name
# recreates the 6 app services with the new GHCR images, restarts the unchanged
# infra containers, and keeps the existing DB volumes.
export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-helpdesk-platform}"

TAG="${1:?usage: remote-deploy.sh <image-tag>}"

# repo root = infra/jenkins/deploy/ -> ../../../ 
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT"

if [ ! -f .env ]; then
  echo "ERROR: $REPO_ROOT/.env is missing. The deploy stage must write it (from the 'helpdesk-env' credential) before deploying." >&2
  exit 1
fi

# Fail fast if the .env doesn't actually carry the keys the stack needs — a
# stale/empty 'helpdesk-env' credential would otherwise surface as blank
# passwords and connection failures at runtime.
for k in MSSQL_SA_PASSWORD MSSQL_DATABASE JWT_ISSUER JWT_AUDIENCE JWT_ACCESS_TOKEN_EXPIRY_MINUTES JWT_REFRESH_TOKEN_EXPIRY_DAYS AI_SERVICE_KEY SEARCH_SERVICE_KEY NOTIFICATION_SERVICE_KEY; do
  grep -q "^${k}=." .env || {
    echo "ERROR: $REPO_ROOT/.env is missing '${k}='. Update the 'helpdesk-env' Jenkins credential with your full .env contents." >&2
    exit 1
  }
done

# infra/certs is gitignored, so it is absent from the Jenkins checkout. The
# identity/ticket/notification services mount it read-only and need the RS256
# keypair — generate it once if missing (files persist on the host across
# deploys).
if [ ! -f infra/certs/private.pem ] || [ ! -f infra/certs/public.pem ]; then
  echo ">>> Generating JWT RSA keypair in $REPO_ROOT/infra/certs"
  mkdir -p infra/certs
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out infra/certs/private.pem
  openssl rsa -pubout -in infra/certs/private.pem -out infra/certs/public.pem
fi

echo ">>> Deploying helpdesk stack (IMAGE_TAG=$TAG) via ${DOCKER_HOST}"
export IMAGE_TAG="$TAG"

echo ">>> Pulling images"
docker compose \
  -f compose.yaml \
  -f infra/jenkins/deploy/docker-compose.images.yml \
  pull --quiet

echo ">>> Starting stack"
docker compose \
  -f compose.yaml \
  -f infra/jenkins/deploy/docker-compose.images.yml \
  up -d --no-build

echo ">>> Deploy complete. Services:"
docker compose -f compose.yaml ps
