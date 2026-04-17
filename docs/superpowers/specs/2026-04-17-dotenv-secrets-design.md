# Design: `.env`-based Secrets Management

**Date:** 2026-04-17  
**Status:** Approved

## Problem

Connection strings and other secrets (Postgres password, Redis password, JWT secret, AWS endpoints) were either committed in `appsettings.Development.json` / `appsettings.LocalStack.json`, or missing entirely (workers had no `appsettings` at all). Neither state is acceptable for a shared repo.

## Goal

- Secrets never committed to git
- A single `.env` template at repo root documents every required variable
- `.env.local` (git-ignored) holds real local values, loaded automatically on `dotnet run`
- CI/CD and Docker continue to use environment variables injected by the host — no code changes required there

## Approach

Use the `DotNetEnv` NuGet package. At startup, each `Program.cs` calls:

```csharp
DotNetEnv.Env.TraversePath().Load(".env.local");
```

`TraversePath()` walks up from the working directory until it finds `.env.local`. If the file doesn't exist (CI, Docker), the call is a no-op — host-injected environment variables are already in place. Variables loaded from `.env.local` set real process environment variables, which .NET's built-in `EnvironmentVariablesConfigurationProvider` picks up using `__` as the section separator.

## File Structure

```
/ (repo root)
├── .env                  ← committed; template with comments, empty values
├── .env.local            ← NOT committed (.gitignore already covers this)
├── src/
│   ├── services/AuctionKings.API/
│   │   ├── appsettings.json                  ← unchanged; empty passwords as fallback
│   │   ├── appsettings.Development.json      ← ConnectionStrings block removed
│   │   ├── appsettings.LocalStack.json       ← passwords removed; AWS endpoints kept
│   │   └── Program.cs                        ← Env.TraversePath().Load(".env.local") added
│   └── workers/AuctionKings.BackgroundWorkers/
│       ├── appsettings.json                  ← unchanged
│       ├── appsettings.Development.json      ← ConnectionStrings block removed
│       └── Program.cs                        ← Env.TraversePath().Load(".env.local") added
└── Directory.Packages.props                  ← DotNetEnv version added
```

## `.env` Template (committed)

```dotenv
# PostgreSQL connection string
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=

# Redis connection string
ConnectionStrings__Redis=localhost:6379

# JWT settings (API only)
Jwt__Secret=
Jwt__Issuer=
Jwt__Audience=
Jwt__ExpiresInMinutes=60

# AWS local endpoints (LocalStack / Moto)
AWS__SQS__ServiceURL=http://localhost:4566
AWS__SQS__BidQueueUrl=http://localhost:4566/000000000000/auction-bids-queue
AWS__Cognito__ServiceURL=http://localhost:5555
```

## `.env.local` (not committed — developer creates from `.env`)

```dotenv
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=<postgres_password>
ConnectionStrings__Redis=localhost:6379,password=<redis_password>
Jwt__Secret=dev-only-secret-key-at-least-32-chars!!
Jwt__Issuer=auction-kings-dev
Jwt__Audience=auction-kings-dev
AWS__SQS__ServiceURL=http://localhost:4566
AWS__SQS__BidQueueUrl=http://localhost:4566/000000000000/auction-bids-queue
AWS__Cognito__ServiceURL=http://localhost:5555
```

Actual passwords are in `docker/secrets/postgres_password.txt` and `docker/secrets/redis_password.txt`.

## NuGet Package

`DotNetEnv` added to `Directory.Packages.props` (centralized version), referenced in:
- `AuctionKings.API.csproj`
- `AuctionKings.BackgroundWorkers.csproj`

## Configuration Priority (high → low)

1. Host environment variables (CI, Docker)
2. `.env.local` (local dev, loaded by `DotNetEnv`)
3. `appsettings.{Environment}.json`
4. `appsettings.json`

## README Update

The "Configuration" section is updated to explain the pattern. Option B (local dev) gets a new first step:

```bash
cp .env .env.local
# Fill in .env.local with your secrets (see docker/secrets/)
```

## Out of Scope

- Production secrets management (handled by AWS Secrets Manager / ECS task definitions)
- Test project configuration (integration tests use Testcontainers; no `.env.local` needed)
