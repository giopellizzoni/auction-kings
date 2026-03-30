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
- `src/workers/AuctionKings.BackgroundWorkers` — Outbox processor
- `test/` — UnitTests, IntegrationTests, ArchitectureTests, LoadTests

**Key patterns:** CQRS, Outbox Pattern, two SignalR hubs (`AuctionHub`, `NotificationHub`), Redis backplane, optimistic concurrency via PostgreSQL `xmin`.

**Infrastructure:** PostgreSQL 16 (port 5432), Redis 7 (port 6379). Secrets in `/docker/secrets/` (not committed). DB: `AuctionKingsDB`, user: `dev`.

## Build Configuration

- NuGet versions: centralized in `Directory.Packages.props` — do NOT add versions in individual `.csproj` files
- `Directory.Build.props` — `net10.0`, C# 14, nullable enabled, analyzers enforced
- Code style: `.editorconfig`
- `TreatWarningsAsErrors` is commented out — warnings do not fail the build

## Custom Instructions

- Do not suppress warnings; format code per `.editorconfig`
- Always search for **agents** and **skills** to help implement tasks
- Full architecture spec (v2.0 roadmap, AWS deployment): `/architecture/ARCHITECTURE.md`
