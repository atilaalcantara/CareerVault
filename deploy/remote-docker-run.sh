#!/usr/bin/env bash
set -euo pipefail

APP_NAME="${APP_NAME:-career-vault-api}"
HOST_PORT="${HOST_PORT:-5000}"
CONTAINER_PORT="${CONTAINER_PORT:-8080}"
DOCKER_CMD="${DOCKER_CMD:-docker}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$APP_DIR"
export DOCKER_CONFIG="${DOCKER_CONFIG:-$APP_DIR/.docker-config}"
mkdir -p "$DOCKER_CONFIG"

$DOCKER_CMD build --tag "$APP_NAME" .
$DOCKER_CMD stop "$APP_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD rm "$APP_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD run -d \
  --name "$APP_NAME" \
  --restart unless-stopped \
  --env-file .env \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:$CONTAINER_PORT \
  -p "$HOST_PORT:$CONTAINER_PORT" \
  "$APP_NAME"
