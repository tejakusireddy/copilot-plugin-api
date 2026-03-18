# copilot-plugin-api
A production-grade Copilot-style conversational API built in C# / .NET 8 with Azure OpenAI, Redis, and SQL, designed for reliability, cost control, and LLM inference at scale.

[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![C# 12](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Azure OpenAI](https://img.shields.io/badge/Azure_OpenAI-GPT--4o-0078D4?logo=microsoftazure&logoColor=white)](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
[![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)](https://redis.io)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://www.docker.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)]()

## Architecture Overview
```mermaid
   flowchart TD
       A[Client<br/>POST /api/chat] --> B[Rate Limiter<br/>Redis token bucket · 100 req/min]
       B -->|429 if exceeded| Z1[HTTP 429]
       B --> C[Idempotency Check<br/>SHA-256 key · 60s TTL]
       C -->|duplicate requestId| Z2[Return cached response]
       C --> D[CopilotController<br/>Validates input · orchestrates pipeline]
       D -->|invalid input| Z3[HTTP 400]
       D --> E[Memory Service<br/>Last 20 turns · 24h TTL · token trimmed]
       D --> F[Semantic Cache<br/>Redis hash lookup]
       F -->|cache hit| Z4[Return cached response · skip LLM]
       F --> G[Prompt Builder<br/>System + history + query · token budget enforced]
       G --> H[LLM Orchestrator<br/>Retry + exponential backoff · graceful degradation]
       H -->|GPT-4o healthy| I[Azure OpenAI GPT-4o]
       H -->|GPT-4o throttled| J[Azure OpenAI GPT-4o-mini<br/>lower-cost fallback]
       H -->|both fail| Z5[degraded: true · partial response]
       I --> K[Response Formatter<br/>SSE streaming · JSON fallback]
       J --> K
       K --> L[Audit Logger<br/>SQL · tokens · latency · cost estimate]
       L --> M[Client receives response]
```

## How it works

Every inbound request passes through a strict pipeline:

1. **Rate limiter** checks a Redis token bucket, 100 requests per minute per user. Returns HTTP 429 immediately if exceeded.
2. **Idempotency check** looks up the requestId in Redis. If the same request was seen in the last 60 seconds, the cached response is returned immediately, no duplicate LLM call.
3. **Semantic cache** hashes the full prompt and checks Redis. A cache hit skips the LLM entirely, zero token cost.
4. **Memory service** fetches the last 20 conversation turns from Redis, trimmed dynamically to fit within the model token budget.
5. **Prompt builder** assembles system prompt + trimmed history + user message and enforces the token ceiling before the LLM call.
6. **LLM orchestrator** calls GPT-4o with retry and exponential backoff. Falls back to GPT-4o-mini under throttling. Returns a graceful partial response if both fail — never a hard 500.
7. **Response formatter** streams tokens back via SSE as they arrive, or returns full JSON if SSE is not requested.
8. **Audit logger** writes token count, model used, latency, and a cost estimate to SQL on every request.

## Key Design Decisions
- **Context-aware memory**: Redis-backed session history capped at 20 turns with 24h TTL and dynamic token trimming. This prevents unbounded Redis growth and keeps prompts within the model context window.
- **Semantic caching**: Prompt hash lookup in Redis before every LLM call. This eliminates redundant inference calls and directly reduces token cost.
- **Idempotent request handling**: SHA-256 keyed deduplication with 60s TTL. This handles network retries and client double-submits without duplicate LLM calls.
- **Graceful degradation**: LLM orchestrator retries GPT-4o with exponential backoff, falls back to GPT-4o-mini, and returns a partial response if both fail. The service does not return a hard 500 during normal throttling and dependency failure paths.
- **Cost visibility**: Every request logs token count, model used, and a cost estimate to SQL. This enables cost-per-user analysis and cache hit rate tracking over time.

## Tech Stack
| Layer | Technology |
| --- | --- |
| Runtime | .NET 8 (C#) — current LTS, Microsoft-native |
| Web framework | ASP.NET Core Minimal API + Controllers |
| LLM primary | Azure OpenAI — GPT-4o (gpt-4o deployment name) |
| LLM fallback | Azure OpenAI — GPT-4o-mini (lower-cost fallback) |
| Cache / memory | Redis 7 via StackExchange.Redis |
| Audit database | SQLite (local dev) / PostgreSQL (prod-ready) |
| ORM | Entity Framework Core 8 |
| Token counting | Microsoft.ML.Tokenizers (tiktoken-compatible) |
| Containerisation | Docker + docker-compose |
| Streaming | Server-Sent Events (SSE) via IAsyncEnumerable |
| Testing | xUnit + Moq |

## Prerequisites
- .NET 8 SDK
- Docker and docker-compose
- Azure OpenAI resource with `gpt-4o` and `gpt-4o-mini` deployments

## Local Setup

```bash
cp .env.example .env
# Open .env and fill in:
#   AzureOpenAI__ApiKey   — your Azure OpenAI API key
#   AzureOpenAI__Endpoint — your Azure OpenAI endpoint URI
#   AzureOpenAI__PrimaryDeployment=gpt-4o
#   AzureOpenAI__FallbackDeployment=gpt-4o-mini

docker-compose up -d
dotnet run --project src/
```

Four commands. That is the entire local setup.

> **Production note:** In production, supply environment variables directly through
> your orchestrator (Kubernetes, Azure App Service, etc.) — do not deploy a `.env` file.

## API Usage
Request:
```bash
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user-123",
    "sessionId": "session-abc",
    "requestId": "550e8400-e29b-41d4-a716-446655440000",
    "message": "Summarise the last sprint retrospective"
  }'
```

Success response:
```json
{
  "response": "The last sprint retrospective covered...",
  "cacheHit": false,
  "degraded": false,
  "modelUsed": "gpt-4o",
  "promptTokens": 412,
  "completionTokens": 187,
  "latencyMs": 1243.5
}
```

Rate limit response:
```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60
Content-Type: application/json

{
  "error": "Rate limit exceeded",
  "retryAfterSeconds": 60
}
```

## Running the tests
```bash
dotnet test tests/CopilotApi.Tests/
```

The test suite covers:
- Rate limiter: token bucket logic, fail-open on Redis unavailability
- Idempotency: SHA-256 key hashing, cache hit and miss paths
- Memory service: bounded history, TTL reset, token budget trimming
- LLM orchestrator: retry policy, fallback activation, cost calculation, Responsible AI log compliance

## Live result

The following shows a successful end-to-end request against the local `/api/chat` endpoint backed by Azure OpenAI GPT-4o:

![Successful chat API response](docs/images/chat-success.png)

Response includes model used, token counts, latency, and cost visibility fields — all logged to SQL via the audit logger.

## Environment Variables
| Variable | Required | Description |
| --- | --- | --- |
| `AzureOpenAI__ApiKey` | Yes | Authenticates requests to the Azure OpenAI resource. Maps to `AzureOpenAI:ApiKey` via the ASP.NET Core double-underscore convention. |
| `AzureOpenAI__Endpoint` | Yes | Specifies the Azure OpenAI endpoint URI. Maps to `AzureOpenAI:Endpoint`. |
| `AzureOpenAI__PrimaryDeployment` | No | Overrides the primary deployment name from `appsettings.json`. Defaults to `gpt-4o`. |
| `AzureOpenAI__FallbackDeployment` | No | Overrides the fallback deployment name from `appsettings.json`. Defaults to `gpt-4o-mini`. |
| `DATABASE_PROVIDER` | No | Selects the SQL provider. Supported values are `sqlite` and `postgresql`. Defaults to `sqlite`. |
| `DATABASE_CONNECTION_STRING` | Yes | Supplies the SQLite or PostgreSQL connection string used by Entity Framework Core. |
| `Redis__ConnectionString` | No | Overrides the Redis connection string from `appsettings.json`. Defaults to `localhost:6379`. |
| `ASPNETCORE_ENVIRONMENT` | No | Sets the hosting environment. Use `Development` locally, `Production` in deployment. |
| `ASPNETCORE_URLS` | No | Sets the listen address. Defaults to `http://+:8080` in Docker. |

## Project Structure
```text
copilot-plugin-api/
|-- docs/
|   `-- images/
|       `-- chat-success.png
|-- src/
|   |-- Configuration/
|   |   |-- AzureOpenAIConfig.cs
|   |   |-- CostsConfig.cs
|   |   |-- MemoryConfig.cs
|   |   |-- PromptConfig.cs
|   |   |-- RateLimitConfig.cs
|   |   |-- RedisConfig.cs
|   |   `-- SemanticCacheConfig.cs
|   |-- Controllers/
|   |   `-- CopilotController.cs
|   |-- Data/
|   |   |-- Migrations/
|   |   |   |-- 20260318035158_InitialCreate.cs
|   |   |   |-- 20260318035158_InitialCreate.Designer.cs
|   |   |   `-- AppDbContextModelSnapshot.cs
|   |   |-- AppDbContext.cs
|   |   |-- AuditLogConfiguration.cs
|   |   `-- AuditLogger.cs
|   |-- Models/
|   |   |-- ChatRequest.cs
|   |   |-- ChatResponse.cs
|   |   |-- ConversationTurn.cs
|   |   |-- LlmResult.cs
|   |   `-- PromptTooLargeException.cs
|   |-- Services/
|   |   |-- IdempotencyService.cs
|   |   |-- LlmOrchestratorService.cs
|   |   |-- MemoryService.cs
|   |   |-- PromptBuilderService.cs
|   |   |-- RateLimiterService.cs
|   |   `-- SemanticCacheService.cs
|   |-- CopilotPluginApi.csproj
|   `-- Program.cs
|-- tests/
|   `-- CopilotApi.Tests/
|       |-- CopilotApi.Tests.csproj
|       |-- IdempotencyTests.cs
|       |-- LlmOrchestratorTests.cs
|       |-- MemoryServiceTests.cs
|       `-- RateLimiterTests.cs
|-- .dockerignore
|-- .env.example
|-- .gitignore
|-- appsettings.Development.json.example
|-- appsettings.json
|-- CopilotPluginApi.sln
|-- docker-compose.yml
|-- Dockerfile
|-- LICENSE
`-- README.md
```

## What would production hardening add?

This project is architected for production but intentionally scoped for clarity. The next layer of hardening would include:

- **Polly ResiliencePipeline** replacing the manual retry loop in LlmOrchestratorService — circuit breaker, bulkhead isolation, and hedging policies
- **OpenTelemetry** distributed tracing across all service layers — trace ID propagated from client through to audit log
- **Vector similarity caching** via Azure AI Search or Qdrant — catches semantically equivalent prompts that hash differently
- **Kubernetes deployment** with HorizontalPodAutoscaler keyed on request queue depth — Redis-backed shared state makes horizontal scaling safe
- **Prompt injection detection** middleware — validates user input against known injection patterns before reaching the LLM

## License
MIT