# CareerVault

CareerVault is a lightweight .NET 10 Minimal API for capturing professional career memory from text and multimodal files. It sends the input to Gemini, receives a stable structured payload, stores it in PostgreSQL first, then syncs to Notion, and generates local embeddings asynchronously with a background worker.

The ingestion prompt is kept in Portuguese because the generated records are intended for a Portuguese personal Notion database.

## Features

- Multipart ingestion for text, audio, images, and PDFs
- Gemini REST integration with model fallback and retries
- Entity Framework Core persistence with PostgreSQL and `career_vault` schema
- pgvector storage for semantic search
- Local embeddings with `ElBruno.LocalEmbeddings`
- Notion REST integration for page creation
- Telegram bot webhook with session-based collection and confirmation
- In-memory Telegram processing queue
- Background worker for embedding generation and reprocessing
- Deterministic natural-language embedding text with versioned content hashing
- Docker-ready deployment
- GitHub Actions workflow for GHCR-based deployment

## Stack

- .NET 10
- ASP.NET Core Minimal APIs
- C#
- Entity Framework Core
- Docker
- PostgreSQL
- pgvector
- Gemini API
- Notion API
- Telegram Bot API
- ElBruno.LocalEmbeddings
- GitHub Container Registry

## Configuration

Use environment variables or an `.env` file. Do not commit real secrets.

```env
GEMINI__APIKEY=
NOTION__TOKEN=Bearer 
CONNECTIONSTRINGS__POSTGRES=Host=personal-postgres;Port=5432;Database=personal_db;Username=postgres;Password=CHANGE_ME;Search Path=career_vault,public
TELEGRAM__BOTTOKEN=
TELEGRAM__WEBHOOKSECRET=
TELEGRAM__ALLOWEDUSERIDS__0=
TELEGRAM__QUEUECAPACITY=100
LOCALEMBEDDINGS__MODEL=sentence-transformers/all-MiniLM-L6-v2
LOCALEMBEDDINGS__DIMENSIONS=384
LOCALEMBEDDINGS__CACHEDIRECTORY=/app/.cache/local-embeddings
EMBEDDINGWORKER__ENABLED=true
EMBEDDINGWORKER__INTERVALSECONDS=30
EMBEDDINGWORKER__BATCHSIZE=5
EMBEDDINGWORKER__MAXDEGREEOFPARALLELISM=1
EMBEDDINGWORKER__FAILEDRETRYDELAYMINUTES=5
RESUMEPROFILE__FULLNAME=
RESUMEPROFILE__HEADLINE=
RESUMEPROFILE__EMAIL=
RESUMEPROFILE__PHONE=
RESUMEPROFILE__LOCATION=
RESUMEPROFILE__LINKEDINURL=
RESUMEPROFILE__GITHUBURL=
RESUMEPROFILE__PORTFOLIOURL=
RESUMEPROFILE__BASESUMMARY=
```

## Run Locally

```bash
dotnet tool restore
dotnet restore
dotnet run
```

Swagger:

```text
http://localhost:5000/swagger
```

## Database Migrations

The application now uses EF Core migrations and applies pending migrations on startup.

Useful commands:

```bash
dotnet tool restore
dotnet dotnet-ef migrations add <MigrationName> --output-dir Data/Migrations
dotnet dotnet-ef database update
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
/confirmar send to Gemini, PostgreSQL, and Notion
/cancelar  discard the current collection
```

Supported inputs include text, Telegram voice messages, audio files, images, and PDFs.

## Docker

```bash
docker build -t career-vault-api .
docker run --rm -p 5000:8080 --env-file .env career-vault-api
```

Para persistir o cache do modelo local:

```bash
mkdir -p models-cache
docker compose up -d
```

O `docker-compose.yml` da aplicação já monta `./models-cache:/app/.cache/local-embeddings` e conecta na Docker network externa `personal-net`.

## PostgreSQL + pgvector na VPS

Use uma única instância PostgreSQL para vários projetos pessoais e separe por schemas.

Exemplos:

- `career_vault`
- `gamer_api`
- `future_project`

Na VPS, a infra do banco foi separada da aplicação e ficou em `~/apps/postgres`, ao lado dos outros serviços.

Resumo do fluxo:

1. Request entra pela API ou Telegram.
2. Gemini retorna `structuredEntry` + `notionPayload`.
3. A API salva primeiro no PostgreSQL.
4. A API tenta sincronizar com o Notion.
5. O worker em background gera ou reprocessa embeddings depois.

## Busca semântica

Endpoint inicial:

```http
POST /api/v1/search/semantic
```

Payload:

```json
{
  "query": "experiencia com kubernetes, troubleshooting e logs em producao",
  "limit": 10
}
```

O endpoint usa o mesmo provider local de embeddings e combina:

- busca vetorial no `pgvector`
- ranking textual leve com `tsvector` + `pg_trgm`

O contrato da API continua o mesmo; a combinacao hibrida acontece apenas no backend.

## Curriculo sob medida por vaga

Preview do pipeline:

```http
POST /api/v1/resumes/generate-preview
```

Gera o PDF final:

```http
POST /api/v1/resumes/generate
```

Payload:

```json
{
  "jobDescription": "descricao bruta da vaga",
  "templateId": "default-ats",
  "targetLanguage": "pt-BR"
}
```

Fluxo:

1. Gemini analisa a vaga e gera queries.
2. A API consulta a base profissional com busca semantica/hibrida.
3. Evidencias relevantes sao deduplicadas e priorizadas.
4. Gemini gera um draft estruturado do curriculo.
5. QuestPDF renderiza o PDF final no backend.

## Embedding text and rebuild

The embedding text is built by the application from the structured fields already stored in PostgreSQL. The app does not ask Gemini for a second "embedding-only" narrative. Instead, it creates a deterministic natural-language description from fields such as `title`, `content`, `summary`, `company`, `project`, `role`, `technologies`, and `tags`.

This keeps the pipeline stable, cheap, and easy to reprocess.

When the embedding text format changes, mark existing completed entries as `stale` and let the worker rebuild them in small batches:

```bash
dotnet run -- mark-embeddings-stale
```

Optional model override:

```bash
dotnet run -- mark-embeddings-stale sentence-transformers/all-MiniLM-L6-v2
```

After this command:

1. existing `completed` entries are moved to `stale`
2. the background worker picks them up on the next cycle
3. embeddings are regenerated with the current text format
4. `professional_entry_embeddings` is updated in place through `ON CONFLICT`

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

Deploy remoto validado na VPS:

- o app nasce primeiro na `personal-net` para conseguir resolver `personal-postgres` desde o boot
- em seguida ele tambem e conectado na `career-vault-net`
- o cache local do modelo e persistido em `~/apps/career-vault-api/models-cache`
- o banco fica separado em `~/apps/postgres`

## ARM64 notes

Checklist para VPS Linux ARM64:

- `uname -m`
- `docker version`
- `docker compose version`
- `docker run --rm --platform linux/arm64 mcr.microsoft.com/dotnet/runtime:8.0 uname -m`

O projeto evita fixar `amd64`, usa imagens oficiais multi-arch da Microsoft e mantém o cache do modelo fora do container para sobreviver a restart.

## Notes

CareerVault continua intencionalmente pequeno e prático: REST direto para integrações externas, uma única instância PostgreSQL compartilhada por schemas, e embeddings locais assíncronos sem gerar carga no request principal.
