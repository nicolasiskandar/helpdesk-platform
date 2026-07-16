#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<EOF
Usage: ./scripts.sh <command>

Commands:
  setup       Generate RSA keys and create .env file
  test        Run all unit tests
  coverage    Run tests and show code coverage
  clean       Remove test results and build artifacts
  help        Show this help
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
}

cmd_test() {
  dotnet test tests/IdentityService.Tests/
}

cmd_coverage() {
  dotnet tool install --global dotnet-reportgenerator-globaltool 2>/dev/null || true
  mkdir -p ./TestResults/Report
  dotnet test tests/IdentityService.Tests/ --collect:"XPlat Code Coverage" \
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
  echo "Cleaned."
}

case "${1:-help}" in
  setup)    cmd_setup ;;
  test)     cmd_test ;;
  coverage) cmd_coverage ;;
  clean)    cmd_clean ;;
  help|*)   usage ;;
esac
