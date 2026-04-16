# CLAUDE.md

## Project Overview

AuctionKings is a real-time auction platform — ASP.NET Core 10, Clean Architecture, DDD, SignalR, PostgreSQL + Redis.

## Common Commands

```bash
# Infrastructure (run first)
cd docker && docker-compose -f docker-compose.infra.yml up -d

# Build & Run — API: http://localhost:5081, Swagger: http://localhost:5081/swagger
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
- `src/services/AuctionKings.Application` — CQRS commands/queries/handlers (note: `.csproj` is misspelled `Applciation`)
- `src/services/AuctionKings.Domain` — aggregates, entities, value objects, domain events
- `src/services/AuctionKings.Infrastructure` — repositories, EF Core, external integrations
- `src/common/AuctionKings.Shared` — `Entity`, `AggregateRoot` base classes
- `src/workers/AuctionKings.BackgroundWorkers` — OutboxProcessor, AuctionCloseWorker (polls every 10s), CleanupExpiredDataWorker (hourly purge)
- `test/` — UnitTests, IntegrationTests, ArchitectureTests, LoadTests

**Key patterns:** CQRS, Outbox Pattern (with SKIP LOCKED batch processing), two SignalR hubs (`AuctionHub`, `NotificationHub`), Redis backplane, optimistic concurrency via PostgreSQL `xmin`, idempotency via `X-Request-Id` header, Mapster DTOs, FluentValidation pipeline behaviors.

**SignalR note:** `SignalRNotificationService` lives in the API layer (not Infrastructure) to avoid circular dependency.

**Infrastructure:** PostgreSQL 16 (port 5432), Redis 7 (port 6379), LocalStack 4.3 (port 4566), Moto 5.1.4 (port 5555). Secrets in `/docker/secrets/` (not committed). DB: `AuctionKingsDB`, user: `dev`.

**Health endpoints:** `/health/live` (liveness), `/health/ready` (DB + Redis + Outbox), `/health/db` (database only).

## Build Configuration

- NuGet versions: centralized in `Directory.Packages.props` — do NOT add versions in individual `.csproj` files
- `Directory.Build.props` — `net10.0`, C# 14, nullable enabled, analyzers enforced
- Code style: `.editorconfig`
- `TreatWarningsAsErrors` is commented out — warnings do not fail the build

## Implementation Status

| Task | Description                                                                                  | Status         |
| ---- | -------------------------------------------------------------------------------------------- | -------------- |
| T01  | Base classes (Entity, AggregateRoot, Result)                                                 | ✅ Done        |
| T02  | Domain model (Auction, Bid, VOs, Domain Events)                                              | ✅ Done        |
| T03  | Application layer (MediatR, CQRS, pipeline behaviors)                                        | ✅ Done        |
| T04  | Commands (CreateAuction, PlaceBid, CloseAuction)                                             | ✅ Done        |
| T05  | Queries (GetAuctionDetails, GetActiveAuctions, GetAuctionBids)                               | ✅ Done        |
| T06  | Infrastructure (EF Core, repositories, UnitOfWork, migrations)                               | ✅ Done        |
| T07  | Outbox processor + idempotency                                                               | ✅ Done        |
| T08  | Redis cache + rate limiter                                                                   | ✅ Done        |
| T09  | SignalR hubs + ConnectionTracker + Redis backplane                                           | ✅ Done        |
| T10  | REST controllers (Auctions, Bids, Users) + Swagger                                           | ✅ Done        |
| T11  | Middleware & filters (GlobalExceptionFilter, SecurityHeaders, RequestLogging, CorrelationId) | ✅ Done        |
| T12  | Health checks (DB, Redis, Outbox)                                                            | ✅ Done        |
| T13  | Background workers (AuctionCloseWorker, CleanupExpiredDataWorker)                            | ✅ Done        |
| T14  | Unit tests                                                                                   | ✅ Done        |
| T15  | Integration tests (Testcontainers — concurrency, idempotency)                                | ✅ Done        |
| T16  | Architecture tests (NetArchTest)                                                             | ✅ Done        |
| T17  | CI pipeline (GitHub Actions)                                                                 | ✅ Done        |
| T18  | Load tests (NBomber)                                                                         | ✅ Done        |
| T19  | Security, Observability, DevOps, Docs (P0+P1 complete)               | ✅ Done        |

## Custom Instructions

- Do not suppress warnings; format code per `.editorconfig`
- Always search for **agents** and **skills** to help implement tasks
- Full architecture spec (v2.0 roadmap, AWS deployment): `/architecture/ARCHITECTURE.md`
