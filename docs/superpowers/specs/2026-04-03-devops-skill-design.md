# DevOps Skill Design — AuctionKings

**Date:** 2026-04-03
**Status:** Approved

---

## Summary

A new Claude Code skill (`devops`) that guides containerization, Docker Compose orchestration, CI/CD pipeline Docker integration, and secrets management for the AuctionKings platform. Follows a hybrid approach: workflow + principles in `SKILL.md`, annotated canonical examples in `references/PATTERNS.md`.

## Requirements

- Multi-stage Dockerfiles for .NET 10 services to minimize image size
- Docker Compose for local dev (with hot-reload config) and production-like environments
- CI/CD pipeline steps for Docker build, push to ECR, vulnerability scanning, and ECS Fargate deployment
- Layered secrets strategy: Docker secrets locally, AWS Secrets Manager for ECS
- Template/pattern support for adding future services (e.g., Blazor WASM frontend)

## Scope

### In scope

- Multi-stage Dockerfiles for `AuctionKings.API` and `AuctionKings.BackgroundWorkers`
- `.dockerignore` for minimal build context
- `docker-compose.yml` — local dev (builds from source, extends infra, volume-mounts config)
- `docker-compose.prod.yml` — production-like override (pre-built images, resource limits, no mounts)
- CI/CD additions to `.github/workflows/ci.yml` (Docker build, Trivy scan, ECR push, ECS deploy)
- Secrets strategy documentation and `Program.cs` integration pattern
- Reusable patterns for containerizing future .NET services

### Out of scope

- Application code quality (handled by `dotnet-core-expert`)
- Deep security audit of Docker config (handled by `security-auditor`)
- Git workflow for PRs (handled by `git-workflow`)

## Skill Structure

```
.claude/skills/devops/
├── SKILL.md                    # Workflow, principles, checklists
└── references/
    └── PATTERNS.md             # Annotated canonical examples
```

**Trigger description:** Use when creating or modifying Dockerfiles, docker-compose files, `.dockerignore`, CI/CD pipelines for Docker builds, or preparing services for container deployment.

## SKILL.md — Workflow

Five-phase workflow when the skill is invoked:

1. **Assess** — Read the target project's `.csproj`, `Program.cs`, and existing Docker files to understand what's being containerized
2. **Generate** — Produce Dockerfile, compose files, `.dockerignore` using patterns from `references/PATTERNS.md`
3. **Secure** — Apply secrets strategy (Docker secrets for local, AWS Secrets Manager guidance for ECS)
4. **Pipeline** — Add/update CI/CD steps for Docker build, push, deploy
5. **Validate** — Run `docker build` to verify the image builds, check image size, confirm healthchecks work

## Core Principles

- **Multi-stage builds always** — builder stage (`sdk`) + runtime stage (`aspnet`), never ship the SDK image
- **Non-root user** — always create and switch to `appuser` before `ENTRYPOINT`
- **Minimal layers** — combine `RUN` commands, order by cache-friendliness (restore -> build -> publish)
- **Restore before copy** — copy `.csproj` + `Directory.*.props` first, then `dotnet restore`, then copy source to maximize layer cache hits
- **Alpine base images** — use `*-alpine` variants for smaller footprint unless a dependency requires glibc
- **Healthchecks in compose, not Dockerfile** — keeps images reusable, healthcheck config varies by environment
- **No secrets in images** — no `ENV`, `ARG`, or `COPY` of credential files into the image. Secrets injected at runtime only
- **One process per container** — API and BackgroundWorkers are separate images/services
- **Port 8080 convention** — ASP.NET Core 10 defaults to 8080 for non-root containers

## Checklists

### Dockerfile checklist

- [ ] Multi-stage build (sdk -> aspnet or sdk -> runtime)
- [ ] Alpine base images
- [ ] `.csproj` + `Directory.*.props` copied before source for restore caching
- [ ] `dotnet publish` with `-c Release`, `--no-restore`
- [ ] Non-root user (`appuser`) before ENTRYPOINT
- [ ] No secrets baked into image layers
- [ ] `.dockerignore` exists and excludes `bin/`, `obj/`, `.git/`, `docker/secrets/`

### Compose checklist

- [ ] Services on named networks (not default bridge)
- [ ] `depends_on` with `condition: service_healthy`
- [ ] Secrets via Docker secrets (not env vars with passwords)
- [ ] Volumes for persistent data only (not app binaries)
- [ ] Port bindings use `127.0.0.1:` for infra services

### CI/CD checklist

- [ ] Docker layer caching enabled
- [ ] Image tagged with git SHA + `latest`
- [ ] ECR login step before push
- [ ] Vulnerability scan step (Trivy or Docker Scout)
- [ ] ECS service update with new task definition

## File Naming Conventions

- `docker/Dockerfile.api` — API service Dockerfile
- `docker/Dockerfile.worker` — BackgroundWorkers Dockerfile
- `docker/docker-compose.yml` — local dev (replaces current empty file)
- `docker/docker-compose.prod.yml` — production-like override
- `.dockerignore` — at repo root (Docker build context is repo root)

## references/PATTERNS.md — Content Plan

### P1: Multi-stage Dockerfile (ASP.NET Core API)

- Build stage: `sdk:10.0-alpine`, copy `Directory.Build.props` + `Directory.Packages.props` + all `.csproj` files preserving project reference graph, `dotnet restore`, copy source, `dotnet publish`
- Runtime stage: `aspnet:10.0-alpine`, create `appuser`, copy published output, `USER appuser`, `EXPOSE 8080`, `ENTRYPOINT`

### P2: Multi-stage Dockerfile (Background Worker)

- Same build stage pattern targeting `AuctionKings.BackgroundWorkers.csproj`
- Runtime stage uses `runtime:10.0-alpine` instead of `aspnet` (no HTTP stack)
- No `EXPOSE` directive

### P3: .dockerignore

Excludes: `.git`, `bin/`, `obj/`, `node_modules/`, `.vs/`, `.idea/`, `docker/secrets/`, `*.user`, `*.DotSettings`, `test/`, `docs/`

### P4: docker-compose.yml (local dev)

- Uses `include` to extend `docker-compose.infra.yml`
- Builds API + Worker from local Dockerfiles
- Volume-mounts `appsettings.Development.json` for config overrides
- `depends_on` with `condition: service_healthy` on postgres and redis
- All services join the `backend` network from infra

### P5: docker-compose.prod.yml (production-like override)

- No build context — references pre-built images (`${ECR_REGISTRY}/auctionkings-api:${TAG}`)
- Resource limits via `deploy.resources.limits`
- Restart policy `unless-stopped`
- Healthcheck on API (`/health` endpoint)
- No volume mounts for app code

### P6: CI/CD pipeline steps

- Docker build step with build context at repo root, Dockerfile path at `docker/Dockerfile.api`
- Trivy vulnerability scan action
- AWS ECR login action
- Tag with `github.sha` + `latest`, push to ECR
- ECS deploy action with task definition update

### P7: Secrets strategy

| Environment                    | Mechanism           | Details                                                  |
| ------------------------------ | ------------------- | -------------------------------------------------------- |
| Local dev (compose)            | Docker secrets      | Files in `docker/secrets/`, mounted at `/run/secrets/`   |
| Production-like (compose.prod) | Docker secrets      | Same mechanism, different secret values                  |
| ECS Fargate                    | AWS Secrets Manager | Injected as env vars via task definition `secrets` block |

Integration pattern using `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)` in `Program.cs` to read Docker secrets with fallback to environment variables.

## Relationship to Other Skills

| Skill                | Boundary                                                                                                 |
| -------------------- | -------------------------------------------------------------------------------------------------------- |
| `dotnet-core-expert` | Owns application code standards; `devops` owns container packaging of that code                          |
| `security-auditor`   | Audits Docker config as part of Domain 4 (DevOps); `devops` skill produces config that passes that audit |
| `code-review`        | Reviews application code; `devops` artifacts (Dockerfiles, compose) reviewed against devops checklists   |
| `git-workflow`       | Governs branching and PRs; `devops` follows those conventions when committing Docker files               |
