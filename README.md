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

Starts API, background workers, PostgreSQL, Redis, LocalStack, Prometheus, and Grafana in one command.

The `api`/`worker` containers (unlike the infra-only containers) read `POSTGRES_PASSWORD`, `REDIS_PASSWORD`, and `JWT_SECRET` as plain environment variables — `docker compose` does **not** auto-load `.env.local`, so you must pass it explicitly with `--env-file`:

```bash
cd docker
cp .env .env.local
# Edit .env.local — set POSTGRES_PASSWORD/REDIS_PASSWORD to match
# secrets/postgres_password.txt and secrets/redis_password.txt, and set
# JWT_SECRET (e.g. `openssl rand -base64 48 | tr -d '\n'`).

docker compose --env-file .env.local up -d
```

If you forget `--env-file .env.local`, those three variables interpolate to empty strings — the API will crash on startup with a JWT `IDX10703: key length is zero` error and fail to authenticate with Redis (`NOAUTH`).

Services available after startup (~15 seconds):

| Service    | URL                                     |
| ---------- | --------------------------------------- |
| API        | <http://localhost:5081>                 |
| Swagger UI | <http://localhost:5081/swagger>         |
| Grafana    | <http://localhost:3000> (admin / admin) |
| Prometheus | <http://localhost:9090>                 |
| LocalStack | <http://localhost:4566>                 |

### Option B — Local Development (dotnet run)

```bash
# 0. Copy env template and fill in secrets
cp .env .env.local
# Edit .env.local — passwords are in docker/secrets/

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

```text
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

| Document                                           | Description                                              |
| -------------------------------------------------- | -------------------------------------------------------- |
| [Architecture](docs/architecture.md)               | System design, DDD, CQRS, Outbox, SignalR, AWS roadmap   |
| [Decisions](docs/decisions.md)                     | Architecture Decision Records (ADRs)                     |
| [Runbook](docs/runbook.md)                         | Health checks, metrics, troubleshooting, AWS staging ops |
| [Demo Script](docs/demo-script.md)                 | Repeatable 5-minute demo with curl commands              |
| [Cognito Integration](docs/cognito-integration.md) | AWS Cognito migration guide                              |
| [Audit](docs/audit.md)                             | Security and code audit findings                         |

## Configuration

Copy the env template and fill in your secrets before running locally:

```bash
cp .env .env.local
# Edit .env.local — passwords are in docker/secrets/
```

`.env` is committed and documents every required variable. `.env.local` is git-ignored and holds real values.

- `dotnet run` loads `.env.local` automatically (via `DotNetEnv` in `Program.cs`) — no extra step needed.
- `docker compose` does **not** auto-load `.env.local` (only `.env`). Pass it explicitly: `docker compose --env-file .env.local up -d`.

### Database

- Host: `localhost:5432`, DB: `AuctionKingsDB`, user: `dev`
- Password: `docker/secrets/postgres_password.txt`

### Redis

- Host: `localhost:6379`
- Password: `docker/secrets/redis_password.txt`

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
