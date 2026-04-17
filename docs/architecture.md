# AuctionKings — Architecture Overview

---

## Table of Contents

1. [System Summary](#1-system-summary)
2. [Layer Diagram](#2-layer-diagram)
3. [Domain Model (DDD)](#3-domain-model-ddd)
4. [CQRS with MediatR](#4-cqrs-with-mediatr)
5. [Outbox Pattern](#5-outbox-pattern)
6. [SignalR Real-Time Layer](#6-signalr-real-time-layer)
7. [Concurrency, Idempotency, and Auth](#7-concurrency-idempotency-and-auth)
8. [Rate Limiting](#8-rate-limiting)
9. [Observability](#9-observability)
10. [Infrastructure — Local Docker Stack](#10-infrastructure--local-docker-stack)
11. [AWS Staging Architecture](#11-aws-staging-architecture)
12. [AWS Production Roadmap — v2.0](#12-aws-production-roadmap--v20)
13. [Key Design Decisions](#13-key-design-decisions)

---

## 1. System Summary

AuctionKings is a real-time auction platform built with .NET 10 / ASP.NET Core 10. It supports thousands of concurrent bidders, guarantees data consistency under high concurrency, and delivers sub-second bid notifications over WebSockets.

| Concern | Technology |
|---|---|
| API / Hubs | ASP.NET Core 10 — REST + SignalR |
| Application logic | MediatR (CQRS) + FluentValidation |
| Persistence | PostgreSQL 16 via EF Core 10 |
| Cache / Rate limiting | Redis 7 |
| Real-time backplane | Redis (SignalR) |
| Auth (current) | JWT HS256 — dev only |
| Auth (roadmap) | AWS Cognito (RS256) |
| Logging | Serilog — structured JSON |
| Background workers | .NET Worker Service |
| Local infra | Docker (PostgreSQL, Redis, LocalStack, Moto) |
| Cloud target | AWS ECS Fargate, RDS, ElastiCache, SQS/SNS |

---

## 2. Layer Diagram

```mermaid
graph TD
    Client([Browser / Mobile Client])

    subgraph API["API Layer (AuctionKings.API)"]
        Controllers["REST Controllers\n(Auctions, Bids, Users)"]
        Hubs["SignalR Hubs\n(AuctionHub, NotificationHub)"]
        Middleware["Middleware & Filters\n(Auth, CorrelationId, RateLimit,\nGlobalExceptionFilter, SecurityHeaders)"]
    end

    subgraph Application["Application Layer (AuctionKings.Application)"]
        Commands["Commands\n(CreateAuction, PlaceBid, CloseAuction)"]
        Queries["Queries\n(GetAuctionDetails, GetActiveAuctions,\nGetAuctionBids)"]
        Behaviors["Pipeline Behaviors\n(Validation, Logging, Idempotency,\nPerformance)"]
    end

    subgraph Domain["Domain Layer (AuctionKings.Domain)"]
        Aggregates["Aggregates\n(Auction, Bid)"]
        ValueObjects["Value Objects\n(Money, AuctionDuration, BidIncrement)"]
        DomainEvents["Domain Events\n(BidPlaced, AuctionClosed, ...)"]
        DomainErrors["Domain Errors / Result"]
    end

    subgraph Infrastructure["Infrastructure Layer (AuctionKings.Infrastructure)"]
        EF["EF Core + PostgreSQL\n(Repositories, UoW, Migrations)"]
        Outbox["Outbox Table\n(transactional event buffer)"]
        Redis["Redis\n(Cache, Rate Limit, SignalR backplane)"]
        SignalRNotif["SignalRNotificationService\n(IHubContext — lives in API)"]
    end

    subgraph Workers["Background Workers"]
        OutboxProc["OutboxProcessor\n(1 s poll, SKIP LOCKED)"]
        CloseWorker["AuctionCloseWorker\n(10 s poll)"]
        CleanupWorker["CleanupExpiredDataWorker\n(hourly)"]
    end

    Client -->|HTTP / WebSocket| API
    Controllers --> Application
    Hubs --> Application
    Middleware --> Application
    Application --> Domain
    Application --> Infrastructure
    Infrastructure --> Domain
    Workers --> Infrastructure
    Domain -->|domain events| Outbox
    Outbox --> OutboxProc
    OutboxProc -->|MediatR Publish| Application
    Application --> SignalRNotif
    SignalRNotif -->|push| Client
```

---

## 3. Domain Model (DDD)

### Aggregates

| Aggregate | Aggregate Root | Key entities / VOs |
|---|---|---|
| Auction | `Auction` | `Bid` (entity), `Money`, `AuctionDuration`, `BidIncrement` |
| User | `User` | `UserProfile` |

### Key domain rules (enforced in `Auction.PlaceBid`)

- Auction must be `Active` and not past `EndTime`.
- Bid amount must exceed `CurrentPrice`.
- The current winner cannot outbid themselves.
- **Anti-sniping:** if a bid arrives within the last 30 seconds, `EndTime` extends by 2 minutes and an `AuctionExtendedEvent` is raised.

### Domain Events

All events implement `IDomainEvent` and are collected on `AggregateRoot`. They are never dispatched in-process — they are serialised into the Outbox table within the same database transaction and dispatched asynchronously.

| Event | Trigger |
|---|---|
| `AuctionCreatedEvent` | New auction persisted |
| `BidPlacedEvent` | Successful bid |
| `AuctionExtendedEvent` | Anti-sniping extension |
| `AuctionClosedEvent` | Auction finalised |
| `WinnerDeterminedEvent` | Winner resolved after close |

---

## 4. CQRS with MediatR

All use-cases flow through MediatR. Commands mutate state; queries are read-only and may hit the Redis cache before touching PostgreSQL.

### Commands

| Command | Description |
|---|---|
| `CreateAuctionCommand` | Create and persist a new auction |
| `PlaceBidCommand` | Validate and record a bid; raises `BidPlacedEvent` |
| `CloseAuctionCommand` | Transition auction to `Closed`; raises `AuctionClosedEvent` |

### Queries

| Query | Description |
|---|---|
| `GetAuctionDetailsQuery` | Single auction with bids (cache-first) |
| `GetActiveAuctionsQuery` | Paginated list of active auctions |
| `GetAuctionBidsQuery` | Bid history for a given auction |

### Pipeline Behaviors (ordered)

1. **LoggingBehavior** — structured entry/exit logs with elapsed ms.
2. **ValidationBehavior** — FluentValidation; returns `Result.Failure` on error.
3. **IdempotencyBehavior** — Redis cache + DB store keyed on `X-Request-Id` (24 h TTL).
4. **PerformanceBehavior** — warning log when handler exceeds 500 ms.

---

## 5. Outbox Pattern

**Problem:** Domain events must be delivered even if the process crashes between saving state and publishing events.

**Solution:** Events are serialised as `OutboxMessage` rows and written in the same `SaveChangesAsync` transaction as the aggregate state. A background processor polls and dispatches them.

```
PlaceBid command
    └─► Auction.PlaceBid()              (domain)
            └─► AddDomainEvent(BidPlacedEvent)
    └─► UnitOfWork.SaveChangesAsync()   (infrastructure)
            ├─► INSERT INTO auctions ... (state)
            └─► INSERT INTO outbox_messages ... (event)
                    ── single PostgreSQL transaction ──

OutboxProcessor (every 1 s)
    └─► SELECT ... FOR UPDATE SKIP LOCKED  (100 rows, no double-processing)
    └─► Deserialise + MediatR.Publish()
    └─► SET processed_at = NOW()
```

**Retry policy:** up to 5 retries; error message stored on the row. SKIP LOCKED ensures safe concurrent processing across multiple worker replicas.

---

## 6. SignalR Real-Time Layer

### Two hubs

| Hub | Purpose |
|---|---|
| `AuctionHub` | Per-auction groups — bid updates, price changes, auction close |
| `NotificationHub` | Per-user channel — winner/loser notifications |

### Scaling with Redis backplane

All hub instances share the Redis backplane (`StackExchangeRedis` provider). Group broadcasts (`auction:{id}`) fan out across all API replicas transparently.

### State sync on reconnect

When a client calls `JoinAuction`, the hub sends the full current `AuctionStateDto` (price, winner, bid count, last 10 bids, server time, version). If the client version is behind after a reconnect, `RequestStateSync` triggers a full refresh.

### SignalR placement note

`SignalRNotificationService` (which uses `IHubContext<AuctionHub>`) lives in the **API project**, not Infrastructure. This avoids a circular project dependency (Infrastructure → API). It is registered as `IRealtimeNotificationService` and consumed by Application-layer event handlers.

### Graceful shutdown

`GracefulShutdownService` sends a `ServerShuttingDown` message to all connected clients before the process exits, instructing them to reconnect with a 1-second delay.

---

## 7. Concurrency, Idempotency, and Auth

### Optimistic concurrency via PostgreSQL `xmin`

EF Core maps the `xmin` system column (type `xid`) as the concurrency token on the `Auction` entity. On every `UPDATE`, PostgreSQL automatically rejects writes where `xmin` changed since the row was read, and EF Core raises `DbUpdateConcurrencyException`. The handler returns `DomainErrors.Bid.ConcurrentModification` (HTTP 409) and the client retries.

No application-managed version counter is needed; the database itself is the authority.

### Idempotency

The `X-Request-Id` header carries a client-generated UUID per request. `IdempotencyBehavior<TRequest, TResponse>` checks:

1. Redis cache (fast path, 24 h TTL).
2. Persistent `IdempotencyStore` in PostgreSQL (survives cache eviction).

If found, the stored response is returned immediately with no side effects. `PlaceBidCommand` derives its idempotency key as `{UserId}:{AuctionId}:{RequestId}`.

### JWT Authentication (current — dev)

Local HS256 tokens. The `sub` claim carries the user ID. SignalR hubs prefer the `X-SignalR-Token` header or `signalr_token` cookie over the query-string access token (query-string usage is logged as a warning).

### JWT Authentication (roadmap — AWS Cognito)

RS256 tokens issued by Cognito User Pools. See [cognito-integration.md](cognito-integration.md) for the full migration guide.

The Cognito auth layer adds:
- **Token revocation:** `RedisTokenBlacklistService` checks the `jti` claim against a Redis key per revoked token. `RevokeAllUserTokensAsync` stores a `revoke_before` timestamp per user for global sign-out.
- **Claim enrichment:** On token validation, the user profile is fetched and `reputation`, `is_verified`, and `total_bids` claims are added to the principal.
- **Authorization policies:** `CanBid` (email verified + minimum reputation score), `CanCreateAuction`, and `HighValueBid` (MFA or KYC required for bids above $10,000) — enforced via `IAuthorizationHandler`.

---

## 8. Rate Limiting

### Current implementation

`[RateLimitAttribute]` is defined and wired to a Redis-backed `IDistributedRateLimiter`. The attribute is configurable per endpoint: `Limit` (default 10 requests) and `WindowSeconds` (default 60 seconds).

**Current limit:** 10 bids per user per 10 seconds (configured in appsettings).

**Known issue (audit H-6):** The attribute exists but is not yet applied to any controller action — rate limiting is currently inactive until `[RateLimit]` is added to `PlaceBid` and `CreateAuction`.

### Algorithm — distributed sliding window (Redis Lua)

The rate limiter uses an atomic Lua script executed on Redis to avoid race conditions across multiple API instances:

```
1. Remove expired entries from the sorted set: ZREMRANGEBYSCORE key 0 (now - window_ms)
2. Count current entries: ZCARD key
3. If count < limit:
       Add new entry: ZADD key now <unique-member>
       Set TTL: EXPIRE key (window_seconds + 1)
       Return: allowed, remaining = limit - count - 1
   Else:
       Get oldest entry score: ZRANGE key 0 0 WITHSCORES
       Return: denied, retry_after = oldest_score + window_ms - now
```

Headers returned on every response: `X-RateLimit-Limit`, `X-RateLimit-Remaining`. On 429: `Retry-After`.

**Fail-open behaviour:** if Redis is unavailable, requests are allowed through. Audit finding H-4 flags this — write operations should fail closed when Redis is down.

---

## 9. Observability

### Serilog structured JSON logging

- All logs are emitted as structured JSON (enriched with `CorrelationId`, `UserId`, request path, elapsed ms).
- `RequestLoggingMiddleware` records every HTTP request/response.
- `LoggingBehavior` records every MediatR command/query with timing.
- In production the sink targets AWS CloudWatch Logs.

### Health endpoints

| Endpoint | Checks |
|---|---|
| `GET /health/live` | Process liveness only |
| `GET /health/ready` | PostgreSQL + Redis + Outbox backlog |
| `GET /health/db` | Database connectivity only |

### Prometheus metrics

`GET /metrics` exposes a Prometheus-format endpoint via `prometheus-net.AspNetCore`. Three custom metrics are tracked:

| Metric | Type | Description |
|---|---|---|
| `http_request_duration_seconds` | Histogram | Per-route HTTP latency (auto via `UseHttpMetrics`) |
| `auction_kings_bids_placed_total` | Counter | Successful bids since startup |
| `auction_kings_outbox_pending_messages_count` | Gauge | Pending outbox messages per batch |

### Grafana dashboard

An "AuctionKings Overview" dashboard is auto-provisioned in Grafana (`docker/grafana/provisioning/`). It includes panels for request rate, p50/p99 latency, status-code breakdown, bids-placed rate, and outbox lag. Grafana is available at `http://localhost:3000` (admin / admin) when running the infra compose stack.

### Distributed tracing (roadmap)

AWS X-Ray distributed tracing for ECS Fargate deployments is planned for the production environment.

---

## 10. Infrastructure — Local Docker Stack

```
docker/docker-compose.infra.yml
    ├── PostgreSQL 16      :5432  (DB: AuctionKingsDB, user: dev)
    ├── Redis 7            :6379
    ├── LocalStack 4.3     :4566  (S3, SQS, SNS, DynamoDB emulation)
    ├── Moto 5.1.4         :5555  (Cognito emulation)
    ├── Prometheus         :9090  (scrapes /metrics every 15 s)
    └── Grafana            :3000  (auto-provisioned dashboard)
```

---

## 11. AWS Staging Architecture

The staging environment is provisioned with Terraform (`infra/terraform/staging/`) and deployed via GitHub Actions OIDC. It consists of:

```
ALB (public, HTTP :80)
    └─► ECS Fargate (auction-kings-staging cluster)
            │  Task: 0.25 vCPU / 512 MB — API container
            │  Logs → CloudWatch /ecs/auction-kings-staging-api
            │
            ├─► RDS PostgreSQL 16 (db.t4g.micro, encrypted, private subnet)
            ├─► ElastiCache Redis 7 (cache.t4g.micro, TLS + AUTH, private subnet)
            ├─► SQS bid-events queue + DLQ
            └─► Secrets Manager (DB password, Redis AUTH, JWT secret)
```

GitHub OIDC → `AssumeRoleWithWebIdentity` is used for the deploy workflow — no long-lived AWS access keys in CI.

---

## 12. AWS Production Roadmap — v2.0

### Target infrastructure

```
CloudFront (CDN)
    └─► ALB
            ├─► API Service          (ECS Fargate, multi-AZ)
            ├─► SignalR Service      (ECS Fargate, Redis backplane)
            └─► Worker Service       (ECS Fargate)
                    │
                    ├─► RDS PostgreSQL 16 Multi-AZ
                    ├─► ElastiCache Redis 7 cluster mode
                    ├─► DynamoDB          (event store)
                    ├─► SNS / EventBridge (fan-out messaging)
                    ├─► AWS Cognito       (RS256 auth)
                    ├─► Secrets Manager  (rotation enabled)
                    └─► CloudWatch + X-Ray (logs, metrics, traces)
```

### Event Sourcing with DynamoDB snapshots

Domain events will be stored in DynamoDB as an immutable event stream alongside the PostgreSQL write model. Two tables: `AuctionEvents` (stream ID + version as composite key) and `AuctionSnapshots`.

Snapshots are created every 50 events to avoid full stream replay on aggregate hydration. Load sequence: fetch latest snapshot → load events after snapshot version → replay incrementally onto the aggregate.

Optimistic concurrency at the DynamoDB level: writes use a `ConditionExpression` that rejects the put if the version already exists, preventing concurrent event appends.

### Saga pattern — auction closing

Auction close triggers a persisted `AuctionClosingSaga` that coordinates across services with compensating transactions:

| Step | Action | Compensation |
|---|---|---|
| 1 | Determine winner | Reopen auction |
| 2 | Reserve payment | Release payment reservation |
| 3 | Reserve item in inventory | Release item |
| 4 | Notify winner, losers, and seller | — |

Saga state (`AuctionClosingSagaState`) is persisted after every step. On failure, compensation runs in reverse order. Status values: `Started → DeterminingWinner → ReservingPayment → ReservingItem → NotifyingParticipants → Completed`. Failure paths: `Compensating → Failed`.

### Frontend — Blazor WebAssembly

Planned frontend: Blazor WebAssembly served via CloudFront. Connects to `AuctionHub` via SignalR for real-time bid updates.

### Distributed tracing

AWS X-Ray tracing for ECS Fargate deployments (planned). Correlates HTTP requests, MediatR commands, and database queries across service boundaries.

---

## 13. Key Design Decisions

Full context, consequences, and trade-offs for each ADR are in [decisions.md](decisions.md).

| ADR | Decision | Why |
|---|---|---|
| ADR-01 | PostgreSQL `xmin` as EF Core concurrency token | Zero schema overhead; DB-managed; no risk of app forgetting to increment |
| ADR-02 | Outbox Pattern for domain event delivery | At-least-once delivery guarantee even on process crash between DB commit and publish |
| ADR-03 | `X-Request-Id` + Redis cache + DB store for idempotency | Duplicate bids prevented at zero DB cost on hot path (Redis hit); DB store survives cache eviction |
| ADR-04 | `SignalRNotificationService` lives in API project | Avoids circular dependency: Infrastructure → API; interface in Application keeps inner layers clean |
