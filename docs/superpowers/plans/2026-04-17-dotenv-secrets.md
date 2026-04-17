# `.env`-based Secrets Management — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load secrets from a git-ignored `.env.local` file at `dotnet run` time so no credentials are ever committed.

**Architecture:** `DotNetEnv` reads `.env.local` from the repo root (via `TraversePath()`) and sets real process environment variables before the .NET configuration system builds. .NET's built-in `EnvironmentVariablesConfigurationProvider` then picks them up using `__` as the section separator. Committed `appsettings` files retain non-sensitive defaults only.

**Tech Stack:** `DotNetEnv` 3.1.1 NuGet package, .NET 10 configuration system, existing `Directory.Packages.props` for centralized versioning.

---

## Files

| Action | Path |
|--------|------|
| Modify | `Directory.Packages.props` |
| Modify | `src/services/AuctionKings.API/AuctionKings.API.csproj` |
| Modify | `src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj` |
| Modify | `src/services/AuctionKings.API/Program.cs` |
| Modify | `src/workers/AuctionKings.BackgroundWorkers/Program.cs` |
| Modify | `src/services/AuctionKings.API/appsettings.Development.json` |
| Modify | `src/services/AuctionKings.API/appsettings.LocalStack.json` |
| Modify | `src/workers/AuctionKings.BackgroundWorkers/appsettings.Development.json` |
| Create | `.env` (repo root) |
| Create | `.env.local` (repo root, git-ignored) |
| Modify | `README.md` |

---

## Task 1: Register DotNetEnv in centralized package management

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/services/AuctionKings.API/AuctionKings.API.csproj`
- Modify: `src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj`

- [ ] **Step 1: Add version to `Directory.Packages.props`**

Add a new `Shared` group after the `API` group (line 13):

```xml
  <ItemGroup Label="Shared">
    <PackageVersion Include="DotNetEnv" Version="3.1.1" />
  </ItemGroup>
```

- [ ] **Step 2: Reference package in API `.csproj`**

Add to the first `<ItemGroup>` in `src/services/AuctionKings.API/AuctionKings.API.csproj`:

```xml
<PackageReference Include="DotNetEnv" />
```

- [ ] **Step 3: Reference package in Workers `.csproj`**

Add a new `<ItemGroup>` in `src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj` before the closing `</Project>`:

```xml
  <ItemGroup>
    <PackageReference Include="DotNetEnv" />
  </ItemGroup>
```

- [ ] **Step 4: Restore packages**

```bash
dotnet restore
```

Expected: `Restore succeeded.` with no errors. `DotNetEnv 3.1.1` should appear in the restore output.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props \
  src/services/AuctionKings.API/AuctionKings.API.csproj \
  src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
git commit -m "chore: add DotNetEnv package for .env.local loading"
```

---

## Task 2: Create `.env` template and `.env.local`

**Files:**
- Create: `.env` (repo root)
- Create: `.env.local` (repo root)

- [ ] **Step 1: Create `.env` template**

Create `/.env` at the repo root:

```dotenv
# Copy this file to .env.local and fill in the values.
# .env.local is git-ignored and never committed.
# Actual passwords are in docker/secrets/

# PostgreSQL
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=

# Redis
ConnectionStrings__Redis=localhost:6379

# JWT (API only)
Jwt__Secret=
Jwt__Issuer=
Jwt__Audience=
Jwt__ExpiresInMinutes=60

# AWS local endpoints (LocalStack / Moto)
AWS__SQS__ServiceURL=http://localhost:4566
AWS__SQS__BidQueueUrl=http://localhost:4566/000000000000/auction-bids-queue
AWS__Cognito__ServiceURL=http://localhost:5555
```

- [ ] **Step 2: Create `.env.local` with real dev values**

Create `/.env.local` at the repo root (passwords from `docker/secrets/`):

```dotenv
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=Tr1c0lor0!@#
ConnectionStrings__Redis=localhost:6379,password=Tr1c0lor0#@!!@#
Jwt__Secret=dev-only-secret-key-at-least-32-chars!!
Jwt__Issuer=auction-kings-dev
Jwt__Audience=auction-kings-dev
Jwt__ExpiresInMinutes=60
AWS__SQS__ServiceURL=http://localhost:4566
AWS__SQS__BidQueueUrl=http://localhost:4566/000000000000/auction-bids-queue
AWS__Cognito__ServiceURL=http://localhost:5555
```

- [ ] **Step 3: Verify `.env.local` is git-ignored**

```bash
git check-ignore -v .env.local
```

Expected output: `.gitignore:479:.env.local	.env.local`

- [ ] **Step 4: Commit `.env` only**

```bash
git add .env
git commit -m "chore: add .env template for local secrets"
```

---

## Task 3: Load `.env.local` in API `Program.cs`

**Files:**
- Modify: `src/services/AuctionKings.API/Program.cs`

- [ ] **Step 1: Add the load call at the very top of `Program.cs`**

Insert before `var builder = WebApplication.CreateBuilder(args);` (currently line 20):

```csharp
DotNetEnv.Env.TraversePath().Load(".env.local");
```

The top of the file should now read:

```csharp
using System.Text;
using System.Text.Json;
using AuctionKings.API.Auth;
using AuctionKings.API.Filters;
using AuctionKings.API.Hubs;
using AuctionKings.API.Middleware;
using AuctionKings.API.Realtime;
using AuctionKings.Application;
using AuctionKings.Application.Abstractions;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;

DotNetEnv.Env.TraversePath().Load(".env.local");

var builder = WebApplication.CreateBuilder(args);
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build src/services/AuctionKings.API/
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/services/AuctionKings.API/Program.cs
git commit -m "feat: load .env.local at startup in API"
```

---

## Task 4: Load `.env.local` in Workers `Program.cs`

**Files:**
- Modify: `src/workers/AuctionKings.BackgroundWorkers/Program.cs`

- [ ] **Step 1: Add the load call at the very top of `Program.cs`**

Insert before `var builder = Host.CreateApplicationBuilder(args);` (currently line 8). The full file should read:

```csharp
using AuctionKings.BackgroundWorkers;
using AuctionKings.BackgroundWorkers.Workers;
using AuctionKings.Infrastructure;
using AuctionKings.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

DotNetEnv.Env.TraversePath().Load(".env.local");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<OutboxOptions>(
    builder.Configuration.GetSection(OutboxOptions.SectionName)
);

builder.Services.Configure<AuctionCloseWorkerOptions>(
    builder.Configuration.GetSection(AuctionCloseWorkerOptions.SectionName));
builder.Services.Configure<CleanupWorkerOptions>(
    builder.Configuration.GetSection(CleanupWorkerOptions.SectionName));

builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<AuctionCloseWorker>();
builder.Services.AddHostedService<CleanupExpiredDataWorker>();

var host = builder.Build();
await host.RunAsync();
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build src/workers/AuctionKings.BackgroundWorkers/
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/workers/AuctionKings.BackgroundWorkers/Program.cs
git commit -m "feat: load .env.local at startup in background workers"
```

---

## Task 5: Strip credentials from appsettings files

**Files:**
- Modify: `src/services/AuctionKings.API/appsettings.Development.json`
- Modify: `src/services/AuctionKings.API/appsettings.LocalStack.json`
- Modify: `src/workers/AuctionKings.BackgroundWorkers/appsettings.Development.json`

- [ ] **Step 1: Remove `ConnectionStrings` block from API `appsettings.Development.json`**

The file should become:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Information",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "Jwt": {
    "Secret": "dev-only-secret-key-at-least-32-chars!!",
    "Issuer": "auction-kings-dev",
    "Audience": "auction-kings-dev",
    "ExpiresInMinutes": 60
  }
}
```

- [ ] **Step 2: Remove passwords from API `appsettings.LocalStack.json`**

Only the `ConnectionStrings` values change — passwords revert to empty, all AWS endpoint config stays:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=",
    "Redis": "localhost:6379"
  },
  "AWS": {
    "Region": "us-east-1",
    "Cognito": {
      "Region": "us-east-1",
      "ServiceURL": "http://localhost:5555"
    },
    "SQS": {
      "BidQueueUrl": "http://localhost:4566/000000000000/auction-bids-queue",
      "ServiceURL": "http://localhost:4566"
    },
    "DynamoDB": {
      "ServiceURL": "http://localhost:4566"
    },
    "S3": {
      "BucketName": "auctionkings-assets",
      "ServiceURL": "http://localhost:4566"
    },
    "SNS": {
      "ServiceURL": "http://localhost:4566"
    },
    "EventBridge": {
      "ServiceURL": "http://localhost:5555"
    },
    "AppConfig": {
      "ServiceURL": "http://localhost:5555"
    },
    "XRay": {
      "ServiceURL": "http://localhost:5555"
    },
    "SecretsManager": {
      "ServiceURL": "http://localhost:4566"
    },
    "CloudWatch": {
      "ServiceURL": "http://localhost:4566"
    }
  },
  "Cache": {
    "ListingsTtl": "00:00:30",
    "DetailsTtl": "00:01:00",
    "CountersTtl": "00:00:05"
  },
  "RateLimit": {
    "MaxRequests": 10,
    "Window": "00:00:10"
  }
}
```

- [ ] **Step 3: Remove `ConnectionStrings` block from Workers `appsettings.Development.json`**

The file should become:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AWS": {
    "Region": "us-east-1",
    "SQS": {
      "BidQueueUrl": "http://localhost:4566/000000000000/auction-bids-queue",
      "ServiceURL": "http://localhost:4566"
    }
  }
}
```

- [ ] **Step 4: Build entire solution to confirm nothing broke**

```bash
dotnet build
```

Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add \
  src/services/AuctionKings.API/appsettings.Development.json \
  src/services/AuctionKings.API/appsettings.LocalStack.json \
  src/workers/AuctionKings.BackgroundWorkers/appsettings.Development.json
git commit -m "chore: remove credentials from appsettings — secrets now in .env.local"
```

---

## Task 6: Update README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the Configuration section**

Replace the existing "Configuration" section (lines ~112–125) with:

```markdown
## Configuration

Copy the env template and fill in your secrets before running locally:

```bash
cp .env .env.local
# Edit .env.local — passwords are in docker/secrets/
```

`.env` is committed and documents every required variable. `.env.local` is git-ignored and holds real values. It is loaded automatically at startup; no extra step is needed when using `dotnet run`.

### Database

- Host: `localhost:5432`, DB: `AuctionKingsDB`, user: `dev`
- Password: `docker/secrets/postgres_password.txt`

### Redis

- Host: `localhost:6379`
- Password: `docker/secrets/redis_password.txt`
```

- [ ] **Step 2: Update Option B instructions**

In the "Option B — Local Development" section, add step 0 before the existing steps:

```markdown
### Option B — Local Development (dotnet run)

```bash
# 0. Copy env template and fill in secrets
cp .env .env.local
# edit .env.local with passwords from docker/secrets/

# 1. Start infrastructure (PostgreSQL, Redis, LocalStack, Prometheus, Grafana)
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..
...
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: update README with .env.local setup instructions"
```

---

## Task 7: Full verification

- [ ] **Step 1: Verify `.env.local` is not tracked**

```bash
git status
```

Expected: `.env.local` does NOT appear in the output. Only tracked files show.

- [ ] **Step 2: Build the full solution**

```bash
dotnet build
```

Expected: `Build succeeded.` 0 errors.

- [ ] **Step 3: Verify `.env` is committed and `.env.local` is not**

```bash
git ls-files .env .env.local
```

Expected output:
```
.env
```

Only `.env` appears — `.env.local` is correctly ignored.
