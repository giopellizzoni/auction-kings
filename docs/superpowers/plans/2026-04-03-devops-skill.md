# DevOps Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a Claude Code skill that guides containerization, Docker Compose orchestration, CI/CD Docker integration, and secrets management for AuctionKings.

**Architecture:** Two-file skill — `SKILL.md` (workflow, principles, checklists) + `references/PATTERNS.md` (annotated canonical examples). The skill is invoked when working with Dockerfiles, compose files, or CI/CD pipelines.

**Tech Stack:** Docker, Docker Compose, .NET 10, GitHub Actions, AWS ECR, AWS ECS Fargate, Trivy

**Spec:** `docs/superpowers/specs/2026-04-03-devops-skill-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `.claude/skills/devops/SKILL.md` | Workflow, principles, checklists |
| Create | `.claude/skills/devops/references/PATTERNS.md` | Annotated canonical examples for Dockerfiles, compose, CI/CD, secrets |

---

### Task 1: Create SKILL.md

**Files:**
- Create: `.claude/skills/devops/SKILL.md`

- [ ] **Step 1: Create the skill directory**

```bash
mkdir -p .claude/skills/devops/references
```

- [ ] **Step 2: Write SKILL.md**

Create `.claude/skills/devops/SKILL.md` with the following content:

```markdown
---
name: devops
description: Guides containerization, Docker Compose orchestration, CI/CD pipeline Docker integration, and secrets management for AuctionKings .NET 10 services. Use when creating or modifying Dockerfiles, docker-compose files, .dockerignore, CI/CD pipelines for Docker builds, or preparing services for container deployment.
---

# AuctionKings DevOps

This skill guides the creation and maintenance of Docker infrastructure for the AuctionKings platform. It covers multi-stage Dockerfiles, Docker Compose for local and production-like environments, CI/CD pipeline integration, and secrets management.

## When to Use This Skill

- Creating or modifying Dockerfiles for .NET services
- Writing or updating docker-compose files
- Adding Docker build/push/deploy steps to CI/CD pipelines
- Setting up `.dockerignore`
- Configuring secrets for containerized services
- Preparing a new service for container deployment

## Workflow

When invoked, follow these five phases:

1. **Assess** — Read the target project's `.csproj`, `Program.cs`, and any existing Docker files to understand what's being containerized (runtime type: web API vs worker, project references, ports, config sources)
2. **Generate** — Produce Dockerfile, compose files, `.dockerignore` using patterns from [references/PATTERNS.md](references/PATTERNS.md)
3. **Secure** — Apply secrets strategy: Docker secrets for local compose, AWS Secrets Manager guidance for ECS Fargate. Never bake secrets into image layers.
4. **Pipeline** — Add or update CI/CD steps in `.github/workflows/ci.yml` for Docker build, vulnerability scan, ECR push, and ECS deploy
5. **Validate** — Run `docker build` to verify the image builds. Check image size with `docker images`. Confirm compose services start with `docker compose up`

## Core Principles

- **Multi-stage builds always** — builder stage (`sdk`) + runtime stage (`aspnet` or `runtime`), never ship the SDK image
- **Non-root user** — always create and switch to `appuser` before `ENTRYPOINT`
- **Minimal layers** — combine `RUN` commands, order by cache-friendliness (restore -> build -> publish)
- **Restore before copy** — copy `.csproj` + `Directory.*.props` first, then `dotnet restore`, then copy source to maximize layer cache hits
- **Alpine base images** — use `*-alpine` variants for smaller footprint unless a dependency requires glibc
- **Healthchecks in compose, not Dockerfile** — keeps images reusable; healthcheck config varies by environment
- **No secrets in images** — no `ENV`, `ARG`, or `COPY` of credential files into the image. Secrets injected at runtime only
- **One process per container** — each deployable project gets its own Dockerfile and compose service
- **Port 8080 convention** — ASP.NET Core 10 defaults to port 8080 for non-root containers

## File Naming Conventions

- `docker/Dockerfile.api` — API service Dockerfile
- `docker/Dockerfile.worker` — BackgroundWorkers Dockerfile
- `docker/Dockerfile.<name>` — pattern for future services
- `docker/docker-compose.yml` — local dev compose (extends infra)
- `docker/docker-compose.prod.yml` — production-like override
- `.dockerignore` — at repo root (Docker build context is repo root)

## Dockerfile Checklist

- [ ] Multi-stage build (sdk -> aspnet for web, sdk -> runtime for workers)
- [ ] Alpine base images
- [ ] `Directory.Build.props` + `Directory.Packages.props` copied before source
- [ ] Each `.csproj` in the dependency graph copied individually to preserve restore cache
- [ ] `dotnet restore` targets the entry project `.csproj`
- [ ] `dotnet publish -c Release --no-restore` (restore already done)
- [ ] Non-root user (`appuser`) created and set before ENTRYPOINT
- [ ] No secrets baked into image layers (no `ENV` with passwords, no `COPY` of secret files)
- [ ] `.dockerignore` exists at repo root

## Compose Checklist

- [ ] Local dev compose uses `include` to extend `docker-compose.infra.yml`
- [ ] Services on the `backend` network (shared with infra)
- [ ] `depends_on` with `condition: service_healthy` on postgres and redis
- [ ] Secrets via Docker secrets (files in `docker/secrets/`, not env vars with passwords)
- [ ] Volumes for persistent data only (not app binaries)
- [ ] Port bindings use `127.0.0.1:` for infra services
- [ ] Production-like compose uses pre-built images, resource limits, no volume mounts

## CI/CD Checklist

- [ ] Docker layer caching enabled (GitHub Actions cache)
- [ ] Image tagged with git SHA + `latest`
- [ ] ECR login step before push
- [ ] Vulnerability scan step (Trivy)
- [ ] ECS service update with new task definition

## Secrets Strategy

| Environment | Mechanism | Details |
|---|---|---|
| Local dev (compose) | Docker secrets | Files in `docker/secrets/`, mounted at `/run/secrets/` |
| Production-like (compose.prod) | Docker secrets | Same mechanism, different secret values |
| ECS Fargate | AWS Secrets Manager | Injected via task definition `secrets` block |

App reads secrets via `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)` in `Program.cs`, with fallback to environment variables for ECS.
```

- [ ] **Step 3: Verify the file was created correctly**

```bash
cat .claude/skills/devops/SKILL.md | head -5
```

Expected: the frontmatter lines (`---`, `name: devops`, `description: ...`).

- [ ] **Step 4: Commit**

```bash
git add .claude/skills/devops/SKILL.md
git commit -m "feat(devops): add DevOps skill SKILL.md with workflow, principles, and checklists"
```

---

### Task 2: Create references/PATTERNS.md

**Files:**
- Create: `.claude/skills/devops/references/PATTERNS.md`

- [ ] **Step 1: Write PATTERNS.md**

Create `.claude/skills/devops/references/PATTERNS.md` with the following content:

````markdown
# DevOps Patterns — AuctionKings

Annotated canonical examples for Dockerfiles, Docker Compose, CI/CD, and secrets. Adapt these patterns to the specific service being containerized.

---

## P1: Multi-Stage Dockerfile — ASP.NET Core API

Use for any project that hosts HTTP endpoints (API controllers, SignalR hubs, health endpoints).

```dockerfile
# ============================================================
# Build stage — full SDK, produces published output
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# 1. Copy files that affect restore FIRST (maximizes layer cache)
#    If any .csproj or package props change, restore re-runs.
#    If only source code changes, this layer is cached.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/services/AuctionKings.API/AuctionKings.API.csproj src/services/AuctionKings.API/
COPY src/services/AuctionKings.Application/AuctionKings.Application.csproj src/services/AuctionKings.Application/
COPY src/services/AuctionKings.Domain/AuctionKings.Domain.csproj src/services/AuctionKings.Domain/
COPY src/services/AuctionKings.Infrastructure/AuctionKings.Infrastructure.csproj src/services/AuctionKings.Infrastructure/
COPY src/common/AuctionKings.Shared/AuctionKings.Shared.csproj src/common/AuctionKings.Shared/

# 2. Restore — only re-runs when .csproj or props files change
RUN dotnet restore src/services/AuctionKings.API/AuctionKings.API.csproj

# 3. Copy all source and publish
COPY src/ src/
RUN dotnet publish src/services/AuctionKings.API/AuctionKings.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================================
# Runtime stage — minimal image, no SDK, no source code
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Non-root user — container never runs as root
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/publish .

USER appuser

# ASP.NET Core 10 defaults to port 8080 for non-root
EXPOSE 8080

ENTRYPOINT ["dotnet", "AuctionKings.API.dll"]
```

**Why each decision matters:**
- `sdk:10.0-alpine` as builder: Alpine keeps the build layer smaller; SDK is only in this stage, never shipped
- Copy `.csproj` files individually: preserves the project reference graph so `dotnet restore` resolves correctly
- `--no-restore` on publish: restore already happened in a cached layer; avoids redundant work
- `aspnet:10.0-alpine` as runtime: ~100MB vs ~210MB for Debian. Only includes ASP.NET Core runtime, not SDK
- `adduser` before `COPY`: the user exists before app files land, so ownership is correct
- `EXPOSE 8080`: documents the port; actual binding is in compose or ECS task definition

---

## P2: Multi-Stage Dockerfile — Background Worker

Use for projects that run background processing (no HTTP endpoints). Key difference: uses `runtime` base image instead of `aspnet`.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj src/workers/AuctionKings.BackgroundWorkers/
COPY src/services/AuctionKings.Application/AuctionKings.Application.csproj src/services/AuctionKings.Application/
COPY src/services/AuctionKings.Domain/AuctionKings.Domain.csproj src/services/AuctionKings.Domain/
COPY src/services/AuctionKings.Infrastructure/AuctionKings.Infrastructure.csproj src/services/AuctionKings.Infrastructure/
COPY src/common/AuctionKings.Shared/AuctionKings.Shared.csproj src/common/AuctionKings.Shared/

RUN dotnet restore src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj

COPY src/ src/
RUN dotnet publish src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# runtime (not aspnet) — no HTTP stack, smaller image
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/publish .

USER appuser

# No EXPOSE — workers don't serve HTTP
ENTRYPOINT ["dotnet", "AuctionKings.BackgroundWorkers.dll"]
```

**Key difference from API pattern:**
- `runtime:10.0-alpine` instead of `aspnet:10.0-alpine` — saves ~30MB by excluding ASP.NET Core HTTP libraries
- No `EXPOSE` directive — workers connect outbound to databases/queues, they don't listen for inbound connections

---

## P3: .dockerignore

Place at the repo root. The Docker build context is the repo root (because Dockerfiles need access to `Directory.Build.props` and multiple project directories).

```
**/.git
**/.vs
**/.idea
**/.vscode
**/bin
**/obj
**/node_modules
**/test
**/docs
**/docker/secrets
**/*.user
**/*.DotSettings
**/*.md
!Directory.Build.props
!Directory.Packages.props
```

**Why:**
- `test/` and `docs/` excluded — not needed in any Docker image, saves context transfer time
- `docker/secrets/` excluded — NEVER include secret files in the build context
- `*.md` excluded but `Directory.Build.props` and `Directory.Packages.props` explicitly included with `!` — these are needed for restore
- Smaller build context = faster `docker build` (context is sent to daemon before build starts)

---

## P4: docker-compose.yml — Local Development

Builds app services from local Dockerfiles. Includes the infra compose for PostgreSQL and Redis. Designed for `docker compose up` from the `docker/` directory.

```yaml
name: auctionkings

include:
  - docker-compose.infra.yml

services:
  api:
    build:
      context: ..
      dockerfile: docker/Dockerfile.api
    container_name: ak-api
    ports:
      - "5081:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=REPLACED_AT_RUNTIME
    secrets:
      - postgres_password
      - redis_password
    volumes:
      - ../src/services/AuctionKings.API/appsettings.Development.json:/app/appsettings.Development.json:ro
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - backend

  worker:
    build:
      context: ..
      dockerfile: docker/Dockerfile.worker
    container_name: ak-worker
    environment:
      - DOTNET_ENVIRONMENT=Development
    secrets:
      - postgres_password
      - redis_password
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - backend
```

**Why:**
- `include` pulls in infra services (postgres, redis) with their healthchecks, secrets, and network
- `context: ..` — build context is repo root so Dockerfiles can access all projects
- `appsettings.Development.json` mounted read-only — allows config changes without rebuilding
- `depends_on` with `condition: service_healthy` — app waits for DB and cache to be ready
- Connection string uses `Host=postgres` — Docker DNS resolves the service name within the `backend` network
- Secrets are inherited from infra compose's secret definitions

---

## P5: docker-compose.prod.yml — Production-Like Override

No build context. Uses pre-built images. Resource limits enforced. Use for local smoke-testing of production configuration.

```yaml
name: auctionkings-prod

include:
  - docker-compose.infra.yml

services:
  api:
    image: ${ECR_REGISTRY:-local}/auctionkings-api:${TAG:-latest}
    container_name: ak-api
    restart: unless-stopped
    ports:
      - "5081:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    secrets:
      - postgres_password
      - redis_password
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - backend
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 15s
    deploy:
      resources:
        limits:
          cpus: "1.0"
          memory: 512M

  worker:
    image: ${ECR_REGISTRY:-local}/auctionkings-worker:${TAG:-latest}
    container_name: ak-worker
    restart: unless-stopped
    environment:
      - DOTNET_ENVIRONMENT=Production
    secrets:
      - postgres_password
      - redis_password
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - backend
    deploy:
      resources:
        limits:
          cpus: "0.5"
          memory: 256M
```

**Why:**
- `image:` instead of `build:` — uses pre-built images; `ECR_REGISTRY` defaults to `local` for local testing with `docker tag`
- `restart: unless-stopped` — auto-recovery on crashes
- `healthcheck` with `wget` — Alpine doesn't have `curl`; checks the `/health` endpoint
- `deploy.resources.limits` — prevents runaway containers from starving the host; mirrors ECS Fargate task resource allocation
- No volume mounts — production images are self-contained

---

## P6: CI/CD Pipeline — Docker Build, Scan, Push, Deploy

Add these jobs to `.github/workflows/ci.yml` after the existing `build-and-test` job. They only run on pushes to `main` (not on PRs).

```yaml
  docker-build-and-push:
    name: Docker Build, Scan & Push
    runs-on: ubuntu-latest
    needs: build-and-test
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    permissions:
      id-token: write
      contents: read

    strategy:
      matrix:
        include:
          - service: api
            dockerfile: docker/Dockerfile.api
            image-name: auctionkings-api
          - service: worker
            dockerfile: docker/Dockerfile.worker
            image-name: auctionkings-worker

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_ROLE_ARN }}
          aws-region: ${{ vars.AWS_REGION }}

      - name: Login to Amazon ECR
        id: ecr-login
        uses: aws-actions/amazon-ecr-login@v2

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build Docker image
        uses: docker/build-push-action@v6
        with:
          context: .
          file: ${{ matrix.dockerfile }}
          push: false
          load: true
          tags: ${{ matrix.image-name }}:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Scan for vulnerabilities
        uses: aquasecurity/trivy-action@v0.28.0
        with:
          image-ref: ${{ matrix.image-name }}:${{ github.sha }}
          format: table
          exit-code: 1
          severity: CRITICAL,HIGH

      - name: Tag and push to ECR
        env:
          ECR_REGISTRY: ${{ steps.ecr-login.outputs.registry }}
          IMAGE_NAME: ${{ matrix.image-name }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker tag $IMAGE_NAME:$IMAGE_TAG $ECR_REGISTRY/$IMAGE_NAME:$IMAGE_TAG
          docker tag $IMAGE_NAME:$IMAGE_TAG $ECR_REGISTRY/$IMAGE_NAME:latest
          docker push $ECR_REGISTRY/$IMAGE_NAME:$IMAGE_TAG
          docker push $ECR_REGISTRY/$IMAGE_NAME:latest

  deploy-to-ecs:
    name: Deploy to ECS
    runs-on: ubuntu-latest
    needs: docker-build-and-push
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    permissions:
      id-token: write
      contents: read

    strategy:
      matrix:
        include:
          - service: auctionkings-api
            task-def: ecs/api-task-def.json
          - service: auctionkings-worker
            task-def: ecs/worker-task-def.json

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: ${{ secrets.AWS_ROLE_ARN }}
          aws-region: ${{ vars.AWS_REGION }}

      - name: Login to Amazon ECR
        id: ecr-login
        uses: aws-actions/amazon-ecr-login@v2

      - name: Update ECS task definition image
        id: task-def
        uses: aws-actions/amazon-ecs-render-task-definition@v1
        with:
          task-definition: ${{ matrix.task-def }}
          container-name: ${{ matrix.service }}
          image: ${{ steps.ecr-login.outputs.registry }}/${{ matrix.service }}:${{ github.sha }}

      - name: Deploy to ECS
        uses: aws-actions/amazon-ecs-deploy-task-definition@v2
        with:
          task-definition: ${{ steps.task-def.outputs.task-definition }}
          service: ${{ matrix.service }}
          cluster: auctionkings
          wait-for-service-stability: true
```

**Why:**
- `needs: build-and-test` — only build images after tests pass
- `if: github.ref == 'refs/heads/main'` — Docker build/push only on main, not on PRs (PRs only run build-and-test)
- Matrix strategy builds both services in parallel
- `docker/build-push-action` with `cache-from/cache-to: type=gha` — GitHub Actions cache for Docker layers
- Trivy scan with `exit-code: 1` — pipeline fails on CRITICAL/HIGH vulnerabilities
- `wait-for-service-stability: true` — deploy step waits until ECS reports the new task is healthy

---

## P7: Secrets Strategy — Integration Pattern

To read Docker secrets (mounted as files at `/run/secrets/`) with fallback to environment variables (for ECS Fargate), add this to `Program.cs`:

```csharp
// Read Docker secrets if present (local dev + compose environments)
// Falls back to environment variables automatically (ECS Fargate)
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
```

**How it works:**
- `AddKeyPerFile` reads each file in `/run/secrets/` as a configuration key-value pair where the filename is the key and the file content is the value
- In Docker Compose: secret files are mounted at `/run/secrets/postgres_password`, `/run/secrets/redis_password`
- In ECS Fargate: secrets are injected as environment variables via the task definition `secrets` block — no files exist at `/run/secrets/`, so `optional: true` skips gracefully
- `IConfiguration` resolves from the highest-priority source: secrets file > environment variable > appsettings.json

**ECS task definition secrets block example:**

```json
{
  "containerDefinitions": [
    {
      "name": "auctionkings-api",
      "secrets": [
        {
          "name": "postgres_password",
          "valueFrom": "arn:aws:secretsmanager:REGION:ACCOUNT:secret:auctionkings/postgres-password"
        },
        {
          "name": "redis_password",
          "valueFrom": "arn:aws:secretsmanager:REGION:ACCOUNT:secret:auctionkings/redis-password"
        }
      ]
    }
  ]
}
```
````

- [ ] **Step 2: Verify the file was created correctly**

```bash
cat .claude/skills/devops/references/PATTERNS.md | head -5
```

Expected: `# DevOps Patterns — AuctionKings` header.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/devops/references/PATTERNS.md
git commit -m "feat(devops): add references/PATTERNS.md with annotated canonical examples"
```

---

### Task 3: Verify Skill Registration

**Files:**
- None (verification only)

- [ ] **Step 1: Verify the skill appears in Claude Code's skill list**

The skill should auto-register based on the `.claude/skills/devops/SKILL.md` file. Verify by checking the directory structure:

```bash
find .claude/skills/devops -type f
```

Expected output:
```
.claude/skills/devops/SKILL.md
.claude/skills/devops/references/PATTERNS.md
```

- [ ] **Step 2: Verify SKILL.md frontmatter is valid**

```bash
head -4 .claude/skills/devops/SKILL.md
```

Expected:
```
---
name: devops
description: Guides containerization, Docker Compose orchestration, CI/CD pipeline Docker integration, and secrets management for AuctionKings .NET 10 services. Use when creating or modifying Dockerfiles, docker-compose files, .dockerignore, CI/CD pipelines for Docker builds, or preparing services for container deployment.
---
```

- [ ] **Step 3: Verify PATTERNS.md has all 7 sections**

```bash
grep "^## P[0-9]" .claude/skills/devops/references/PATTERNS.md
```

Expected:
```
## P1: Multi-Stage Dockerfile — ASP.NET Core API
## P2: Multi-Stage Dockerfile — Background Worker
## P3: .dockerignore
## P4: docker-compose.yml — Local Development
## P5: docker-compose.prod.yml — Production-Like Override
## P6: CI/CD Pipeline — Docker Build, Scan, Push, Deploy
## P7: Secrets Strategy — Integration Pattern
```

- [ ] **Step 4: Final commit with both files**

If not already committed individually:

```bash
git add .claude/skills/devops/
git commit -m "feat(devops): add DevOps skill for containerization, compose, CI/CD, and secrets management"
```
