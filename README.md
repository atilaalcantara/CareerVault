# CareerVault

CareerVault is a lightweight .NET 10 Minimal API for capturing professional career memory from text and multimodal files. It sends the input to Gemini, receives a structured Notion page payload, and stores it in a Notion database.

The ingestion prompt is kept in Portuguese because the generated records are intended for a Portuguese personal Notion database.

## Features

- Multipart ingestion for text, audio, images, and PDFs
- Gemini REST integration with model fallback and retries
- Notion REST integration for page creation
- Telegram bot webhook with session-based collection and confirmation
- In-memory Telegram processing queue
- Docker-ready deployment
- GitHub Actions workflow for GHCR-based deployment

## Stack

- .NET 10
- ASP.NET Core Minimal APIs
- C#
- Docker
- Gemini API
- Notion API
- Telegram Bot API
- GitHub Container Registry

## Configuration

Use environment variables or an `.env` file. Do not commit real secrets.

```env
GEMINI__APIKEY=
NOTION__TOKEN=Bearer 
TELEGRAM__BOTTOKEN=
TELEGRAM__WEBHOOKSECRET=
TELEGRAM__ALLOWEDUSERIDS__0=
TELEGRAM__QUEUECAPACITY=100
```

## Run Locally

```bash
dotnet restore
dotnet run
```

Swagger:

```text
http://localhost:5000/swagger
```

## HTTP Ingestion

```http
POST /api/memory/ingest
```

Multipart fields:

- `files`: audio, image, or PDF files
- `context`: optional text context

Example:

```bash
curl -X POST http://localhost:5000/api/memory/ingest \
  -F "files=@audio.m4a" \
  -F "files=@document.pdf" \
  -F "context=Registro do dia para minha memoria profissional"
```

## Telegram Webhook

```http
POST /api/telegram/webhook
```

Bot flow:

```text
/iniciar   start a collection
/enviar    review before submission
/confirmar send to Gemini and Notion
/cancelar  discard the current collection
```

Supported inputs include text, Telegram voice messages, audio files, images, and PDFs.

## Docker

```bash
docker build -t career-vault-api .
docker run --rm -p 5000:8080 --env-file .env career-vault-api
```

## Deployment

The repository includes a GitHub Actions workflow that builds a `linux/arm64` Docker image, publishes it to GHCR, and deploys it to a VPS over SSH.

Image:

```text
ghcr.io/atilaalcantara/career-vault:latest
```

Required GitHub Actions secrets:

```text
VPS_HOST
VPS_USER
VPS_SSH_KEY
VPS_PORT
```

Runtime secrets remain on the VPS in:

```text
~/apps/career-vault-api/.env
```

## Notes

CareerVault is intentionally small and practical: no database, no SDKs for external APIs, and no heavy architecture layers. It uses REST integrations and keeps the deployment model simple.
