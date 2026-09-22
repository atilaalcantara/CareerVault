#!/usr/bin/env bash
set -euo pipefail

APP_NAME="${APP_NAME:-career-vault-api}"
IMAGE="${IMAGE:-ghcr.io/atilaalcantara/career-vault:latest}"
HOST_PORT="${HOST_PORT:-5000}"
HOST_BIND="${HOST_BIND:-127.0.0.1}"
CONTAINER_PORT="${CONTAINER_PORT:-8080}"
ENV_FILE="${ENV_FILE:-.env}"
DOCKER_CMD="${DOCKER_CMD:-sudo docker}"
LEGACY_APP_NAMES="${LEGACY_APP_NAMES:-memoria-profissional-api}"
NETWORK_NAME="${NETWORK_NAME:-career-vault-net}"
POSTGRES_NETWORK_NAME="${POSTGRES_NETWORK_NAME:-personal-net}"
PRIMARY_NETWORK_NAME="${PRIMARY_NETWORK_NAME:-personal-net}"
MODEL_CACHE_DIR="${MODEL_CACHE_DIR:-/home/ubuntu/apps/career-vault-api/models-cache}"
INFISICAL_PROJECT_ID="${INFISICAL_PROJECT_ID:-a6d87435-2c71-4077-adc3-cced6e880143}"
INFISICAL_ENV="${INFISICAL_ENV:-production}"
INFISICAL_PATH="${INFISICAL_PATH:-/career-vault-api}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE"
  exit 1
fi

# infisical-export-path handles Infisical authentication (via the
# centralized /etc/infisical/infisical-credentials.json) and builds the
# secrets file manually, one "KEY=VALUE" line per secret with no
# quoting/escaping. This deliberately avoids `infisical export --format
# dotenv`, whose escaping is not understood by Docker's --env-file parser
# and silently corrupts values containing certain special characters.
INFISICAL_ENV_FILE="$(mktemp)"
cleanup() { rm -f "$INFISICAL_ENV_FILE"; }
trap cleanup EXIT
/usr/local/bin/infisical-export-path "$INFISICAL_PATH" "$INFISICAL_ENV_FILE" "$INFISICAL_ENV" "$INFISICAL_PROJECT_ID"

$DOCKER_CMD pull "$IMAGE"
$DOCKER_CMD network create "$NETWORK_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD network create "$POSTGRES_NETWORK_NAME" >/dev/null 2>&1 || true
mkdir -p "$MODEL_CACHE_DIR"
$DOCKER_CMD stop "$APP_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD rm "$APP_NAME" >/dev/null 2>&1 || true

for legacy_app_name in $LEGACY_APP_NAMES; do
  if [[ "$legacy_app_name" != "$APP_NAME" ]]; then
    $DOCKER_CMD stop "$legacy_app_name" >/dev/null 2>&1 || true
    $DOCKER_CMD rm "$legacy_app_name" >/dev/null 2>&1 || true
  fi
done

$DOCKER_CMD run -d \
  --name "$APP_NAME" \
  --restart unless-stopped \
  --network "$PRIMARY_NETWORK_NAME" \
  --network-alias "$APP_NAME" \
  --env-file "$ENV_FILE" \
  --env-file "$INFISICAL_ENV_FILE" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:$CONTAINER_PORT \
  -p "$HOST_BIND:$HOST_PORT:$CONTAINER_PORT" \
  -v "$MODEL_CACHE_DIR:/app/.cache/local-embeddings" \
  "$IMAGE"

$DOCKER_CMD network connect "$NETWORK_NAME" "$APP_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD network connect "$POSTGRES_NETWORK_NAME" "$APP_NAME" >/dev/null 2>&1 || true
