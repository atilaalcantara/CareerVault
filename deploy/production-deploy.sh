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

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE"
  exit 1
fi

$DOCKER_CMD pull "$IMAGE"
$DOCKER_CMD network create "$NETWORK_NAME" >/dev/null 2>&1 || true
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
  --network "$NETWORK_NAME" \
  --network-alias "$APP_NAME" \
  --env-file "$ENV_FILE" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:$CONTAINER_PORT \
  -p "$HOST_BIND:$HOST_PORT:$CONTAINER_PORT" \
  "$IMAGE"
