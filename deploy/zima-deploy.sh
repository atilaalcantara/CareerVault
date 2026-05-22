#!/usr/bin/env bash
set -euo pipefail

SERVER_HOST="${SERVER_HOST:-192.168.2.101}"
SERVER_USER="${SERVER_USER:-atilao45}"
REMOTE_DIR="${REMOTE_DIR:-/DATA/AppData/career-vault-api}"
IMAGE_SERVICE="${IMAGE_SERVICE:-career-vault-api}"
HOST_PORT="${HOST_PORT:-5001}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARCHIVE_PATH="/tmp/career-vault-api.tar.gz"

if [[ ! -f "$ROOT_DIR/.env" ]]; then
  echo "Arquivo .env nao encontrado em $ROOT_DIR."
  echo "Crie a partir do .env.example antes do deploy."
  exit 1
fi

echo "Gerando pacote..."
tar \
  --exclude=".git" \
  --exclude="bin" \
  --exclude="obj" \
  --exclude=".vs" \
  --exclude=".vscode" \
  -czf "$ARCHIVE_PATH" \
  -C "$ROOT_DIR" .

echo "Criando pasta remota..."
ssh "$SERVER_USER@$SERVER_HOST" "mkdir -p '$REMOTE_DIR'"

echo "Enviando arquivos..."
scp "$ARCHIVE_PATH" "$SERVER_USER@$SERVER_HOST:$REMOTE_DIR/app.tar.gz"

echo "Publicando container..."
ssh "$SERVER_USER@$SERVER_HOST" "
  set -e
  cd '$REMOTE_DIR'
  tar -xzf app.tar.gz
  rm app.tar.gz
  if command -v docker compose >/dev/null 2>&1; then
    docker compose up -d --build
  elif command -v docker-compose >/dev/null 2>&1; then
    docker-compose up -d --build
  else
    APP_NAME='$IMAGE_SERVICE' HOST_PORT='$HOST_PORT' bash deploy/remote-docker-run.sh
  fi
"

echo "Deploy finalizado."
echo "API: http://$SERVER_HOST:$HOST_PORT"
