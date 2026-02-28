# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AuctionKings is a real-time auction platform built with ASP.NET Core 10, following Clean Architecture and Domain-Driven Design (DDD). It uses SignalR for real-time bidding and PostgreSQL + Redis for persistence and caching.

## Common Commands

### Infrastructure (start before running the API)

```bash
cd docker && docker-compose -f docker-compose.infra.yml up -d
```

### Build & Run

```bash
dotnet restore
dotnet build
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj
# API: http://localhost:5081, Swagger: http://localhost:5081/swagger
```

### Testing

```bash
dotnet test                                               # all tests
dotnet test test/AuctionKings.UnitTests/                  # unit tests only
dotnet test test/AuctionKings.IntegrationTests/           # integration tests only
dotnet test test/AuctionKings.ArchitectureTests/          # architecture tests only
dotnet test test/AuctionKings.LoadTests/                  # load tests only
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover  # with coverage
```

## Architecture

### Layer Structure

Clean Architecture with strict dependency direction (API → Application → Domain ← Infrastructure):

- **`src/services/AuctionKings.API`** — ASP.NET Core Web API: controllers, SignalR hubs, filters, middleware
- **`src/services/AuctionKings.Application`** — Business logic: CQRS commands/queries/handlers
- **`src/services/AuctionKings.Domain`** — Domain aggregates, entities, value objects, domain events
- **`src/services/AuctionKings.Infrastructure`** — Data access: repositories, EF Core, external service integrations
- **`src/common/AuctionKings.Shared`** — Base classes: `Entity`, `AggregateRoot`
- **`src/workers/AuctionKings.BackgroundWorkers`** — Outbox processor and other background jobs
- **`test/`** — UnitTests, IntegrationTests, ArchitectureTests, LoadTests

### Key Domain Concepts

- **Aggregates**: `Auction`, `Bid` (planned)
- **Pattern**: CQRS with commands (`PlaceBidCommand`, `CreateAuctionCommand`, `CloseAuctionCommand`) and queries (`GetAuctionDetailsQuery`, `GetActiveAuctionsQuery`)
- **Event Sourcing** with Outbox Pattern for event consistency
- **Real-time**: Two SignalR hubs — `AuctionHub` (bidding updates) and `NotificationHub` (user notifications); Redis backplane for distributed deployments
- **Concurrency**: Optimistic concurrency via PostgreSQL `xmin` column + aggregate version tokens

### Infrastructure

| Service       | Purpose                       | Docker Port |
| ------------- | ----------------------------- | ----------- |
| PostgreSQL 16 | Write model / relational data | 5432        |
| Redis 7       | Caching + SignalR backplane   | 6379        |

Secrets are in `/docker/secrets/` (not committed). DB: `AuctionKingsDB`, user: `dev`.

### Build Configuration

- Centralized NuGet versions in `Directory.Packages.props` — add packages there, not in individual `.csproj` files
- Global build settings in `Directory.Build.props` — targets `net10.0`, C# 14, nullable enabled, analyzers enforced
- Code style rules in `.editorconfig`

### Current State

### Build Configuration

- Centralized NuGet versions in `Directory.Packages.props` — add packages there, not in individual `.csproj` files
- Global build settings in `Directory.Build.props` — targets `net10.0`, C# 14, nullable enabled, analyzers enforced
- Code style rules in `.editorconfig`

### Current State

Most controllers (`AuctionsController`, `BidsController`, `UsersControllers`), hubs, and filters are scaffolded stubs awaiting implementation. The domain base classes and infrastructure setup are in place.

Full architecture specification (in Portuguese) is in `/architecture/ARCHITECTURE.md`, covering the planned v2.0 roadmap including AWS deployment (ECS Fargate, RDS, DynamoDB, Cognito), saga pattern, and full observability stack.

### Custom Instructions

Check the `.claude` directory for skills
