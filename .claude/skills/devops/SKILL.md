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
