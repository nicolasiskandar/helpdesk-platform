#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<EOF
Usage: ./scripts.sh <command>

Commands:
  setup                  Generate RSA keys and create .env file
  up                     Start all services (SQL Server, Identity, Ticket, RabbitMQ, Frontend)
  down                   Stop all services
  logs                   Tail logs from all services
  frontend-dev           Run frontend in dev mode (local, no Docker)
  frontend-build         Build frontend for production
  test                   Run all unit tests (Identity, Ticket, AI)
  test-identity          Run Identity Service tests only
  test-ticket            Run Ticket Service tests only
  test-ai                Run AI Service tests only (ruff + pytest)
  coverage               Run tests and show code coverage
  clean                  Remove test results and build artifacts
  jenkins                Start the Jenkins CI/CD controller (Docker-in-Docker)
  help                   Show this help
EOF
}

cmd_setup() {
  mkdir -p infra/certs
  openssl genpkey -algorithm RSA -out infra/certs/private.pem -pkeyopt rsa_keygen_bits:2048
  openssl rsa -in infra/certs/private.pem -pubout -out infra/certs/public.pem
  echo "RSA keys generated in infra/certs/"

  if [ ! -f .env ]; then
    cp infra/.env.example .env
    echo "Created .env from infra/.env.example — edit it before running docker compose up."
  else
    echo ".env already exists — skipping."
  fi

  local ai_service_key
  ai_service_key="$(openssl rand -hex 32)"
  if grep -q "^AI_SERVICE_KEY=change-me-generate-with-openssl-rand-hex-32" .env || ! grep -q "^AI_SERVICE_KEY=.\+" .env; then
    if grep -q "^AI_SERVICE_KEY=" .env; then
      sed -i "s|^AI_SERVICE_KEY=.*|AI_SERVICE_KEY=${ai_service_key}|" .env
    else
      printf "\nAI_SERVICE_KEY=%s\n" "${ai_service_key}" >> .env
    fi
    echo "AI_SERVICE_KEY generated and written to .env"
  else
    echo "AI_SERVICE_KEY already set — keeping it."
  fi
}

cmd_up() {
  docker compose up --build -d
  echo ""
  echo "Services starting:"
  echo "  Frontend:       http://localhost:3000"
  echo "  API Gateway:    http://localhost:5000"
  echo "  Identity API:   http://localhost:5010 (direct)"
  echo "  Ticket API:     http://localhost:5011 (direct)"
  echo "  Swagger (ID):   http://localhost:5010/swagger"
  echo "  Swagger (TKT):  http://localhost:5011/swagger"
  echo "  Jaeger UI:      http://localhost:16686"
  echo "  Prometheus:     http://localhost:9090"
  echo "  Grafana:        http://localhost:3001 (admin/admin)"
  echo "  RabbitMQ:       http://localhost:15672 (guest/guest)"
  echo "  SQL Server:     localhost:1433"
}

cmd_down() {
  docker compose down
}

cmd_logs() {
  docker compose logs -f
}

cmd_frontend_dev() {
  cd frontend
  if [ ! -d node_modules ]; then
    echo "Installing dependencies..."
    pnpm install
  fi
  pnpm dev
}

cmd_frontend_build() {
  cd frontend
  pnpm install
  pnpm build
}

cmd_test() {
  dotnet test tests/IdentityService.Tests/
  dotnet test tests/TicketService.Tests/
  cmd_test_ai
}

cmd_test_identity() {
  dotnet test tests/IdentityService.Tests/
}

cmd_test_ticket() {
  dotnet test tests/TicketService.Tests/
}

cmd_test_ai() {
  local ai_dir="services/ai-service"
  if [ ! -d "$ai_dir/.venv" ]; then
    echo "Creating Python virtualenv for the AI service..."
    python3 -m venv "$ai_dir/.venv"
    "$ai_dir/.venv/bin/pip" install -q -e "$ai_dir[dev]"
  fi
  (
    cd "$ai_dir"
    .venv/bin/ruff check --no-cache app tests
    .venv/bin/pytest -q
  )
}

cmd_coverage() {
  dotnet tool install --global dotnet-reportgenerator-globaltool 2>/dev/null || true
  mkdir -p ./TestResults/Report
  dotnet test tests/IdentityService.Tests/ --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults > /dev/null 2>&1
  dotnet test tests/TicketService.Tests/ --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults > /dev/null 2>&1
  ~/.dotnet/tools/reportgenerator \
    -reports:'./TestResults/*/coverage.cobertura.xml' \
    -targetdir:./TestResults/Report \
    -reporttypes:TextSummary > /dev/null 2>&1
  cat ./TestResults/Report/Summary.txt
}

cmd_clean() {
  rm -rf ./TestResults
  dotnet clean services/identity-service/src/IdentityService.Api/ > /dev/null 2>&1
  dotnet clean services/ticket-service/src/TicketService.Api/ > /dev/null 2>&1
  echo "Cleaned."
}

cmd_jenkins() {
  docker compose -f infra/jenkins/docker-compose.yml up --build -d
  echo ""
  echo "Jenkins starting:"
  echo "  Jenkins UI:     http://localhost:8080"
  echo "  Setup guide:    infra/jenkins/README.md"
}

case "${1:-help}" in
  setup)            cmd_setup ;;
  up)               cmd_up ;;
  down)             cmd_down ;;
  logs)             cmd_logs ;;
  frontend-dev)     cmd_frontend_dev ;;
  frontend-build)   cmd_frontend_build ;;
  test)             cmd_test ;;
  test-identity)    cmd_test_identity ;;
  test-ticket)      cmd_test_ticket ;;
  test-ai)          cmd_test_ai ;;
  coverage)         cmd_coverage ;;
  clean)            cmd_clean ;;
  jenkins)          cmd_jenkins ;;
  help|*)           usage ;;
esac
