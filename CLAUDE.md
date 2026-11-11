# CLAUDE.md

## Project Overview

AuctionKings is a real-time auction platform — ASP.NET Core 10, Clean Architecture, DDD, SignalR, PostgreSQL + Redis.

## Common Commands

```bash
# First time on a checkout: generate docker/secrets/*.txt + .env.local (idempotent)
./scripts/setup-local-env.sh

# Infrastructure (run first)
cd docker && docker-compose -f docker-compose.infra.yml up -d

# Build & Run — API: http://localhost:5081, Swagger: http://localhost:5081/swagger
# EF Core migrations apply automatically on startup in Development.
dotnet restore && dotnet build
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# Background Workers (separate process)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj

# Tests
dotnet test                          # all
dotnet test test/AuctionKings.UnitTests/
dotnet test test/AuctionKings.IntegrationTests/
dotnet test test/AuctionKings.ArchitectureTests/
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Architecture

Clean Architecture: `API → Application → Domain ← Infrastructure`

- `src/services/AuctionKings.API` — controllers, SignalR hubs, filters, middleware
- `src/services/AuctionKings.Application` — CQRS commands/queries/handlers
- `src/services/AuctionKings.Domain` — aggregates, entities, value objects, domain events
- `src/services/AuctionKings.Infrastructure` — repositories, EF Core, external integrations
- `src/common/AuctionKings.Shared` — `Entity`, `AggregateRoot` base classes
- `src/workers/AuctionKings.BackgroundWorkers` — OutboxProcessor (polls every 5s), AuctionCloseWorker (polls every 10s), CleanupExpiredDataWorker (hourly purge)
- `test/` — UnitTests, IntegrationTests, ArchitectureTests, LoadTests

**Key patterns:** CQRS, Outbox Pattern (with SKIP LOCKED batch processing), two SignalR hubs (`AuctionHub`, `NotificationHub`), Redis backplane, optimistic concurrency via an app-level `Auction.Version` int column (**not** PostgreSQL `xmin` — Npgsql 10 doesn't reliably read `xid` system columns back via `RETURNING`, see the comment in `AuctionConfiguration.cs`), idempotency via `X-Request-Id` GUID header (required only on actions marked `[Idempotent]` — currently `BidsController.PlaceBid` — enforced by the globally registered `IdempotencyFilter`), CORS via an explicit origin allowlist (`Cors:AllowedOrigins`, see `API/Cors/` and ADR-004), Mapster DTOs, FluentValidation pipeline behaviors.

**SignalR note:** `SignalRNotificationService` lives in the API layer (not Infrastructure) to avoid circular dependency.

**Infrastructure:** PostgreSQL 16 (port 5432), Redis 7 (port 6379), LocalStack 4.3 (port 4566), Moto 5.1.4 (port 5555), Prometheus (port 9090), Grafana (port 3000, admin/admin). Secrets in `/docker/secrets/` (not committed) — `postgres_password.txt` and `redis_password.txt` must be created locally; run `./scripts/setup-local-env.sh` once per checkout to generate them and wire up `.env.local` (root) and `docker/.env.local` automatically. DB: `AuctionKingsDB`, user: `dev`.

**Full Docker stack (`docker compose up`, not just the infra file):** the `api`/`worker` containers need `POSTGRES_PASSWORD`, `REDIS_PASSWORD` (same values as the files above), and `JWT_SECRET` as plain env vars. `docker compose` does not auto-load `.env.local` — run `docker compose --env-file .env.local up -d`, or the app crashes on startup (JWT `IDX10703` / Redis `NOAUTH`). See `docker/.env` for the template and README.md → Configuration for details.

**Health endpoints:** `/health/live` (liveness), `/health/ready` (DB + Redis + Outbox), `/health/db` (database only).

## Build Configuration

- NuGet versions: centralized in `Directory.Packages.props` — do NOT add versions in individual `.csproj` files
- `Directory.Build.props` — `net10.0`, C# 14, nullable enabled, analyzers enforced
- Code style: `.editorconfig`
- `TreatWarningsAsErrors` is commented out — warnings do not fail the build

## AWS Services

### Staging (provisioned in `infra/terraform/staging/`)

| Service             | Purpose                                                                   |
| ------------------- | ------------------------------------------------------------------------- |
| VPC                 | Isolated network with public/private subnets                              |
| ALB                 | Public HTTP :80 entry point                                               |
| ECS Fargate         | API + Background Workers (0.25 vCPU / 512 MB each)                        |
| ECR                 | Docker image registry for API and Worker                                  |
| RDS PostgreSQL 16   | Primary database — `db.t4g.micro`, private subnet, encrypted              |
| ElastiCache Redis 7 | Cache, rate limiting, SignalR backplane — `cache.t4g.micro`, TLS + AUTH   |
| SQS                 | `bid-events` queue + DLQ                                                  |
| Secrets Manager     | DB password, Redis AUTH token, JWT secret                                 |
| CloudWatch Logs     | Container logs under `/ecs/auction-kings-staging-*`                       |
| IAM (OIDC)          | GitHub Actions deploy role + ECS task execution role — no long-lived keys |

### Production Roadmap (v2.0 — not yet built)

| Service                  | Purpose                                                   |
| ------------------------ | --------------------------------------------------------- |
| CloudFront               | CDN in front of ALB; serves Blazor WASM frontend          |
| Cognito User Pools       | RS256 JWT auth, replacing local HS256                     |
| DynamoDB                 | Immutable domain event store + snapshots (event sourcing) |
| SNS / EventBridge        | Fan-out messaging for auction events                      |
| X-Ray                    | Distributed tracing across ECS services                   |
| Multi-AZ RDS             | High-availability PostgreSQL                              |
| ElastiCache cluster mode | Scaled Redis                                              |

**Notes:** No API Gateway — ALB routes directly to ECS (required for WebSocket/SignalR). RDS and ElastiCache are in private subnets; only ALB is public. SignalR scaling uses the Redis backplane across API replicas.

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

- `ci.yml` — build + tests on PR/push
- `docker-publish.yml` — build and push API/Worker images
- `infra-staging.yml` / `infra-staging-destroy.yml` — Terraform apply/destroy for staging
- `deploy-staging.yml` — deploy to ECS Fargate staging (GitHub OIDC, no long-lived keys)

## Documentation

- `docs/architecture.md` — full architecture spec, v2.0 roadmap, AWS deployment
- `docs/decisions.md` — ADRs with context and trade-offs
- `docs/cognito-integration.md` — Cognito migration guide (auth is currently local HS256 JWT)
- `docs/runbook.md` — operational commands, health checks, troubleshooting

## Custom Instructions

- Do not suppress warnings; format code per `.editorconfig`
- Always search for **agents** and **skills** to help implement tasks
