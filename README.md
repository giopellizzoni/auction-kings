# AuctionKings

A real-time auction platform built with ASP.NET Core 10, featuring WebSocket-based bidding, Domain-Driven Design, and comprehensive testing.

## Tech Stack

- **Framework**: ASP.NET Core 10.0
- **Language**: C# with nullable reference types
- **Architecture**: Clean Architecture + DDD
- **Real-time**: SignalR (WebSockets) + Redis backplane
- **Database**: PostgreSQL 16
- **Cache**: Redis 7
- **Background workers**: .NET Worker Service
- **Testing**: xUnit + Testcontainers + NBomber

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## Running the Project

### Option A — Full Docker Stack (recommended)

Starts API, background workers, PostgreSQL, Redis, LocalStack, Prometheus, and Grafana in one command:

```bash
cd docker
docker compose up
```

Services available after startup (~15 seconds):

| Service | URL |
|---------|-----|
| API | http://localhost:5081 |
| Swagger UI | http://localhost:5081/swagger |
| Grafana | http://localhost:3000 (admin / admin) |
| Prometheus | http://localhost:9090 |
| LocalStack | http://localhost:4566 |

### Option B — Local Development (dotnet run)

```bash
# 1. Start infrastructure (PostgreSQL, Redis, LocalStack, Prometheus, Grafana)
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# 2. Restore and build
dotnet restore && dotnet build

# 3. Run the API (http://localhost:5081)
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# 4. (Optional) Run background workers in a separate terminal
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

### Verify the stack is healthy

```bash
curl -s http://localhost:5081/health/ready | jq .
```

Expected: `{"status":"Healthy",...}`

## Running Tests

```bash
dotnet test                                           # all tests
dotnet test test/AuctionKings.UnitTests/              # unit
dotnet test test/AuctionKings.IntegrationTests/       # integration (requires Docker)
dotnet test test/AuctionKings.ArchitectureTests/      # architecture enforcement
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Project Structure

```
auction-kings/
├── src/
│   ├── services/
│   │   ├── AuctionKings.API/              # REST API & SignalR Hubs
│   │   ├── AuctionKings.Application/      # CQRS commands, queries, behaviors
│   │   ├── AuctionKings.Domain/           # Domain model, aggregates, events
│   │   └── AuctionKings.Infrastructure/   # EF Core, Redis, repositories
│   ├── common/
│   │   └── AuctionKings.Shared/           # Base classes (Entity, AggregateRoot, Result)
│   └── workers/
│       └── AuctionKings.BackgroundWorkers/ # OutboxProcessor, AuctionCloseWorker
├── test/
│   ├── AuctionKings.UnitTests/
│   ├── AuctionKings.IntegrationTests/
│   ├── AuctionKings.ArchitectureTests/
│   └── AuctionKings.LoadTests/
├── docker/                                # Docker Compose files, secrets, Grafana config
└── infra/                                 # Terraform (AWS staging)
```

## Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/architecture.md) | System design, DDD, CQRS, Outbox, SignalR, AWS roadmap |
| [Decisions](docs/decisions.md) | Architecture Decision Records (ADRs) |
| [Runbook](docs/runbook.md) | Health checks, metrics, troubleshooting, AWS staging ops |
| [Demo Script](docs/demo-script.md) | Repeatable 5-minute demo with curl commands |
| [Cognito Integration](docs/cognito-integration.md) | AWS Cognito migration guide |
| [Audit](docs/audit.md) | Security and code audit findings |

## Configuration

### Database

- Host: `localhost:5432`, DB: `AuctionKingsDB`, user: `dev`
- Password: `docker/secrets/postgres_password.txt`

### Redis

- Host: `localhost:6379`
- Password: `docker/secrets/redis_password.txt`

### Environment variables

Key variables are in `docker/.env`. Secrets are in `docker/secrets/` (not committed).

## Stopping Services

```bash
# Stop Docker stack
cd docker && docker compose down

# Remove volumes (clears DB, Redis, Prometheus, Grafana data)
cd docker && docker compose down -v
```

## Contributing

1. Branch from `develop`
2. Run tests: `dotnet test`
3. Submit a pull request

## License

[MIT License](LICENSE)
