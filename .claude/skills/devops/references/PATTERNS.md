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
# NOTE: Application .csproj is misspelled on disk as Applciation (missing 'i')
COPY src/services/AuctionKings.Application/AuctionKings.Applciation.csproj src/services/AuctionKings.Application/
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
- `adduser` before `USER`: the user must exist before the `USER appuser` instruction. Files copied with `COPY` are owned by `root` (read-only access for `appuser` is sufficient). If the app needs to write to its working directory, use `COPY --from=build --chown=appuser:appgroup /app/publish .`
- `EXPOSE 8080`: documents the port; actual binding is in compose or ECS task definition

---

## P2: Multi-Stage Dockerfile — Background Worker

Use for projects that run background processing (no HTTP endpoints). Key difference: uses `runtime` base image instead of `aspnet`.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj src/workers/AuctionKings.BackgroundWorkers/
# NOTE: Application .csproj is misspelled on disk as Applciation (missing 'i')
COPY src/services/AuctionKings.Application/AuctionKings.Applciation.csproj src/services/AuctionKings.Application/
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
# Requires Docker Compose v2.20+ (Compose Spec). Not compatible with docker-compose v1.
# docker-compose.infra.yml defines the top-level `secrets:` block and the `backend` network.
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
      # Non-sensitive connection parts only. Password read from /run/secrets/postgres_password
      # via AddKeyPerFile (see P7). App composes the full connection string at runtime.
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER}
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
