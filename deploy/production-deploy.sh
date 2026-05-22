#!/usr/bin/env bash
set -euo pipefail

APP_NAME="${APP_NAME:-career-vault-api}"
IMAGE="${IMAGE:-ghcr.io/atilaalcantara/career-vault:latest}"
HOST_PORT="${HOST_PORT:-5000}"
CONTAINER_PORT="${CONTAINER_PORT:-8080}"
ENV_FILE="${ENV_FILE:-.env}"
DOCKER_CMD="${DOCKER_CMD:-sudo docker}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE"
  exit 1
fi

$DOCKER_CMD pull "$IMAGE"
$DOCKER_CMD stop "$APP_NAME" >/dev/null 2>&1 || true
$DOCKER_CMD rm "$APP_NAME" >/dev/null 2>&1 || true

$DOCKER_CMD run -d \
  --name "$APP_NAME" \
  --restart unless-stopped \
  --env-file "$ENV_FILE" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:$CONTAINER_PORT \
  -p "$HOST_PORT:$CONTAINER_PORT" \
  "$IMAGE"
