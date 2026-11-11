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

Both options below start from the same first step:

```bash
# Generates docker/secrets/postgres_password.txt + redis_password.txt (if missing),
# and writes .env.local (repo root) + docker/.env.local from them. Idempotent — safe to
# re-run, never overwrites a file that already exists.
./scripts/setup-local-env.sh
```

### Option A — Full Docker Stack

Starts API, background workers, PostgreSQL, Redis, LocalStack, Prometheus, and Grafana in one command.

The `api`/`worker` containers (unlike the infra-only containers) read `POSTGRES_PASSWORD`, `REDIS_PASSWORD`, and `JWT_SECRET` as plain environment variables, from `docker/.env.local` — `docker compose` does **not** auto-load it, so you must pass it explicitly with `--env-file`:

```bash
cd docker
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

### Option B — Local Development (dotnet run, recommended for iterating)

```bash
# 1. Start infrastructure (PostgreSQL, Redis, LocalStack, Prometheus, Grafana)
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# 2. Restore and build
dotnet restore && dotnet build

# 3. Run the API (http://localhost:5081) — applies EF Core migrations automatically in Development
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# 4. Run background workers in a separate terminal (needed for outbox dispatch and auction auto-close)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

Do **not** manually copy `.env` to `.env.local` and fill in `Jwt__Secret` for this path —
`appsettings.Development.json` already provides a valid dev JWT signing key, and an empty
`Jwt__Secret` env var overrides it, crashing auth on the first request. `setup-local-env.sh`
generates a `.env.local` that leaves JWT alone for exactly this reason.

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

Run the bootstrap script once per checkout — it generates secrets and both `.env.local` files for you:

```bash
./scripts/setup-local-env.sh
```

`.env` (repo root) is committed and documents every variable `dotnet run` reads; `docker/.env`
documents what `docker compose` interpolates. Both `.env.local` files are git-ignored and hold
real generated values. If you prefer to edit them by hand instead of using the script, `.env`
and `docker/.env` are the templates — but leave `Jwt__Secret`/`Issuer`/`Audience` out of the
root `.env.local`: `appsettings.Development.json` already provides a valid dev key, and setting
an empty one there overrides it and crashes auth on the first request.

- `dotnet run` loads `.env.local` automatically (via `DotNetEnv` in `Program.cs`) — no extra step needed.
- `docker compose` does **not** auto-load `.env.local` (only `.env`). Pass it explicitly: `docker compose --env-file .env.local up -d`.

### Database

- Host: `localhost:5432`, DB: `AuctionKingsDB`, user: `dev`
- Password: `docker/secrets/postgres_password.txt`

### Redis

- Host: `localhost:6379`
- Password: `docker/secrets/redis_password.txt`

### CORS

Browser origins allowed to call the REST API and SignalR hubs are listed in `Cors:AllowedOrigins`
(empty by default, meaning no cross-origin browser access). Entries must be exact origins — `https://host[:port]`,
no path, trailing slash, or wildcard — or the API refuses to start.

```jsonc
// appsettings.{Environment}.json
"Cors": { "AllowedOrigins": ["http://localhost:5173"] }
```

As environment variables: `Cors__AllowedOrigins__0=https://app.example.com`, `Cors__AllowedOrigins__1=...`.
Non-browser clients (the .NET SignalR client, k6 load tests, curl) are not affected. See ADR-004 in `docs/decisions.md`.

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
