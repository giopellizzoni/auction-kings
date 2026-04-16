# AuctionKings — Security, Consistency & Code Hygiene Audit

**Date:** 2026-04-16
**Scope:** Full codebase — API, Application, Domain, Infrastructure, Workers, Docker, Build configuration

---

## Executive Summary

AuctionKings is well-architected with Clean Architecture, CQRS, optimistic concurrency, idempotency controls, and a robust outbox pattern. However, the audit identified **3 critical**, **5 high**, **8 medium**, and **7 low** findings across security vulnerabilities, architectural inconsistencies, and unused/dead code.

The most impactful issues are: **unauthenticated SignalR hubs**, **missing authorization on mutation endpoints**, **missing CORS configuration**, and **several unused AWS packages inflating the dependency surface**.

---

## CRITICAL

### C-1. SignalR Hubs Have No Authorization

|                 |                                                                                                                                                                                                                                                                                            |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Location**    | `src/services/AuctionKings.API/Hubs/AuctionHub.cs:8`, `NotificationHub.cs:6`                                                                                                                                                                                                               |
| **Description** | Both hubs have a TODO comment `// TODO: Add [Authorize]` but no `[Authorize]` attribute. Any anonymous WebSocket client can connect, join auction groups, receive real-time bid data, and trigger `JoinAuctionAsync` / `RequestStateSyncAsync` which execute database queries via MediatR. |
| **Impact**      | Anonymous users can: (1) observe all bid activity in real time, (2) flood the server with connections and group joins causing resource exhaustion, (3) invoke `RequestStateSyncAsync` to enumerate auction state without authentication.                                                   |
| **Remediation** | Add `[Authorize]` to both hub classes. Configure JWT bearer token transport for WebSocket connections in `Program.cs` (the `access_token` query string approach for SignalR).                                                                                                              |

### C-2. CreateAuction Endpoint Has No Authorization

|                 |                                                                                                                                                                                                                         |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Controllers/AuctionsController.cs:34-36` (`[HttpPost]`)                                                                                                                                  |
| **Description** | The `POST /api/v1/auctions` endpoint has no `[Authorize]` attribute. Any anonymous caller can create auctions. Only `CloseAuction` requires Admin and `PlaceBid` requires Bidder — but auction creation is unprotected. |
| **Impact**      | An attacker can flood the system with bogus auctions, polluting the database and active listings cache. The `SellerId` is accepted from the request body rather than extracted from the JWT claims, enabling spoofing.  |
| **Remediation** | Add `[Authorize]` to `CreateAuction`. Extract `SellerId` from `User.Claims` instead of trusting the request body.                                                                                                       |

### C-3. IDOR — SellerId and BidderId Accepted from Request Body

|                 |                                                                                                                                                                                                                          |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Location**    | `AuctionsController.cs:43` (`request.SellerId`), `BidsController.cs:47` (`request.BidderId`)                                                                                                                             |
| **Description** | Both `CreateAuctionRequest.SellerId` and `PlaceBidRequest.BidderId` are supplied by the caller in the JSON body. The server never validates that these match the authenticated user's identity from the JWT `sub` claim. |
| **Impact**      | An authenticated attacker can impersonate any user — creating auctions on behalf of others or placing bids as another user. This undermines the entire trust model (ownership checks, self-bidding prevention).          |
| **Remediation** | Remove `SellerId`/`BidderId` from request DTOs. Extract the user ID from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` in the controller and pass it to the command.                                      |

---

## HIGH

### H-1. No CORS Configuration

|                 |                                                                                                                                                                                                                                                                                                                                     |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Program.cs` (entire file)                                                                                                                                                                                                                                                                            |
| **Description** | There is no `AddCors()` / `UseCors()` call in the pipeline. In a browser-facing application with SignalR and REST APIs, the absence of an explicit CORS policy means the default behavior depends on the hosting model. If fronted by a reverse proxy that doesn't strip CORS headers, cross-origin requests may work unrestricted. |
| **Impact**      | Potential cross-origin attacks where a malicious website can invoke API endpoints and SignalR hubs from the user's browser session.                                                                                                                                                                                                 |
| **Remediation** | Add explicit CORS policy restricting allowed origins, methods, and headers.                                                                                                                                                                                                                                                         |

### H-2. DevController Token Endpoint — Insufficient Production Guard

|                 |                                                                                                                                                                                                                                                                                                                                                          |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Controllers/DevController.cs:27`                                                                                                                                                                                                                                                                                          |
| **Description** | The `POST /dev/token` endpoint returns 404 in production based on `_env.IsProduction()`. However: (1) It relies on `ASPNETCORE_ENVIRONMENT` being set correctly — misconfiguration exposes it. (2) There is no `#if DEBUG` compilation guard or conditional registration at DI level. (3) It accepts any `Role` string, enabling admin token generation. |
| **Impact**      | If deployed with `ASPNETCORE_ENVIRONMENT` missing or set to anything other than `Production`, anyone can mint arbitrary JWT tokens including Admin role tokens, gaining full API access.                                                                                                                                                                 |
| **Remediation** | Conditionally register the controller only in Development using `#if DEBUG` or `app.Environment.IsDevelopment()` checks at route registration. Consider removing it entirely and using a separate tool for dev tokens.                                                                                                                                   |

### H-3. GetAuctions and GetBids — No Authorization on Read Endpoints

|                 |                                                                                                                                                                                                                                      |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Location**    | `AuctionsController.cs:63` (`[HttpGet]`), `BidsController.cs:66` (`[HttpGet]`), `UsersController.cs:19` (`[HttpGet("{id:guid}/bids")]`)                                                                                              |
| **Description** | All GET endpoints are publicly accessible without authentication. While read endpoints are often public, the `GetUserBidHistory` endpoint at `/api/v1/users/{id}/bids` exposes any user's complete bid history to anonymous callers. |
| **Impact**      | Information disclosure — attackers can enumerate all users' bid histories by guessing/iterating GUIDs. This reveals bidding strategies.                                                                                              |
| **Remediation** | Add `[Authorize]` to `GetUserBidHistory` and validate that the authenticated user can only access their own bid history (or has Admin role).                                                                                         |

### H-4. Rate Limiter Fails Open

|                 |                                                                                                                                                                   |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.Infrastructure/RateLimiting/RedisRateLimiter.cs:72`                                                                                    |
| **Description** | When Redis is unavailable, the rate limiter catches all exceptions and returns `true` (allowed). This is explicitly logged as "Failing open."                     |
| **Impact**      | If Redis goes down, all rate limiting is disabled. Attackers who can force a Redis outage (or during any Redis maintenance window) gain unlimited API access.     |
| **Remediation** | Consider failing closed (deny) for critical write operations (bid placement), and failing open only for reads. Add monitoring/alerting for rate limiter failures. |

### H-5. JWT Secret in appsettings.Development.json Committed to Git

|                 |                                                                                                                                                                                                                                                    |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/appsettings.Development.json:12`                                                                                                                                                                                    |
| **Description** | The dev JWT secret `"dev-only-secret-key-at-least-32-chars!!"` is committed to source control. While marked as dev-only, it sets a bad precedent and could inadvertently be used in staging/production if environment configuration is mismanaged. |
| **Impact**      | If this secret leaks to production, any attacker can forge valid JWT tokens.                                                                                                                                                                       |
| **Remediation** | Move JWT secrets to environment variables or a secrets manager. Use `dotnet user-secrets` for local development. Add a startup check that throws if the Jwt:Secret matches the dev placeholder in non-Development environments.                    |

---

## MEDIUM

### M-1. AuctionCloseWorker Uses `DateTime.UtcNow` Instead of `IDateTimeProvider`

|                 |                                                                                                                                                                                                                            |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/workers/AuctionKings.BackgroundWorkers/Workers/AuctionCloseWorker.cs:50`                                                                                                                                              |
| **Description** | The `AuctionCloseWorker` calls `DateTime.UtcNow` directly instead of using the injected `IDateTimeProvider`. The Application layer correctly uses `IDateTimeProvider` in all command handlers, but the worker bypasses it. |
| **Impact**      | Inconsistency: (1) `GetExpiredAuctionsAsync` uses a different time source than `CloseAuctionCommandHandler`, causing edge-case race conditions. (2) The worker cannot be unit-tested with a deterministic clock.           |
| **Remediation** | Inject `IDateTimeProvider` into `AuctionCloseWorker` and use it instead of `DateTime.UtcNow`.                                                                                                                              |

### M-2. OutboxProcessor Uses `DateTime.UtcNow` Instead of `IDateTimeProvider`

|                 |                                                                            |
| --------------- | -------------------------------------------------------------------------- |
| **Location**    | `src/workers/AuctionKings.BackgroundWorkers/OutboxProcessor.cs:104`        |
| **Description** | `message.MarkAsProcessed(DateTime.UtcNow)` uses the system clock directly. |
| **Impact**      | Same testability and consistency issues as M-1.                            |
| **Remediation** | Inject and use `IDateTimeProvider`.                                        |

### M-3. `Auction.Activate()` Has No End-Time Validation

|                 |                                                                                                                                                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Location**    | `src/services/AuctionKings.Domain/Aggregates/Auction.cs:81-87`                                                                                                                                         |
| **Description** | `Activate(DateTime now)` only checks `Status != Pending` but doesn't validate that `now < EndTime`. An auction whose end time has already passed can be activated. The `now` parameter is also unused. |
| **Impact**      | An auction can be activated after its scheduled end time, creating an inconsistent state until the `AuctionCloseWorker` closes it in the next polling cycle.                                           |
| **Remediation** | Validate `now < EndTime` in `Activate()` and return a `Result` instead of silently returning.                                                                                                          |

### M-4. `Auction.Cancel()` Is Unreachable Dead Code

|                 |                                                                                                                                                                                                                  |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.Domain/Aggregates/Auction.cs:160-176`                                                                                                                                                 |
| **Description** | The `Cancel(DateTime now)` method exists on the `Auction` aggregate but is never called anywhere in the codebase — no command, no controller endpoint, no worker invokes it. There is no `CancelAuctionCommand`. |
| **Impact**      | Dead code increases maintenance burden. The `now` parameter is also unused. The `Cancelled` enum value exists but is effectively unreachable.                                                                    |
| **Remediation** | Either implement the cancel flow (command + endpoint) or remove the dead code.                                                                                                                                   |

### M-5. Missing `Content-Security-Policy` Header

|                 |                                                                                                                                                                                  |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Middleware/SecurityHeadersMiddleware.cs`                                                                                                          |
| **Description** | The middleware sets several security headers but omits `Content-Security-Policy`. While the API primarily serves JSON, any error page or Swagger UI rendering benefits from CSP. |
| **Impact**      | Reduced defense-in-depth against XSS if the API ever serves HTML content.                                                                                                        |
| **Remediation** | Add `Content-Security-Policy: default-src 'self'` header.                                                                                                                        |

### M-6. `RequestLoggingMiddleware` Is Registered but Never Used

|                 |                                                                                                                                                                                                                         |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Middleware/RequestLoggingMiddleware.cs` (entire file), `Program.cs`                                                                                                                      |
| **Description** | The middleware exists but is never registered in `Program.cs` via `app.UseMiddleware<RequestLoggingMiddleware>()`. The application uses `app.UseSerilogRequestLogging()` instead, making this class entirely dead code. |
| **Impact**      | Unused code; no functional issue. Maintenance burden.                                                                                                                                                                   |
| **Remediation** | Delete `RequestLoggingMiddleware.cs` or add a comment explaining why it's kept.                                                                                                                                         |

### M-7. Idempotency Race Condition — Check-Then-Store Gap

|                 |                                                                                                                                                                                                                                                               |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs:30-38`                                                                                                                                                                                |
| **Description** | The `ExistsAsync` check and `StoreAsync` are not atomic. Between the check and the store, a concurrent request with the same key can pass through, resulting in duplicate processing. The store only happens after the handler succeeds, widening the window. |
| **Impact**      | Under high concurrency, duplicate bids could be placed with the same idempotency key before the key is stored. The optimistic concurrency on `Auction.Version` mitigates this for bids, but any future non-versioned commands would be vulnerable.            |
| **Remediation** | Use an atomic "insert-if-not-exists" operation (e.g., `INSERT ... ON CONFLICT DO NOTHING` returning affected rows) instead of separate check + store.                                                                                                         |

### M-8. `RemoveByPrefixAsync` Uses `KEYS`/`SCAN` Pattern — Not Production-Safe at Scale

|                 |                                                                                                                                                                                                                                |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Location**    | `src/services/AuctionKings.Infrastructure/Cache/RedisCacheService.cs:88-103`                                                                                                                                                   |
| **Description** | `RemoveByPrefixAsync` uses `server.KeysAsync(pattern: ...)` which internally uses `SCAN`. While better than `KEYS`, it still iterates all keys on the selected server. In a clustered Redis setup, this only targets one node. |
| **Impact**      | Performance degradation at scale if many keys exist. Incomplete invalidation in Redis Cluster mode.                                                                                                                            |
| **Remediation** | Use key-set tracking (maintain a Redis set of related keys per prefix) or tag-based invalidation instead of SCAN.                                                                                                              |

---

## LOW

### L-1. `UsersControllers.cs` — Empty Stub File

|                 |                                                                                                                                                                                                       |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Controllers/UsersControllers.cs`                                                                                                                                       |
| **Description** | Contains a single comment: `// Class moved to UsersController.cs — file retained to preserve git history.` This is dead code. Git history is preserved in the commit log regardless of file presence. |
| **Remediation** | Delete the file.                                                                                                                                                                                      |

### L-2. Application `.csproj` File Has Typo: `AuctionKings.Applciation.csproj`

|                 |                                                                                                                                                                                                          |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.Application/AuctionKings.Applciation.csproj` (filename on disk)                                                                                                               |
| **Description** | The project file is misspelled (`Applciation` instead of `Application`). This is a known issue documented in `CLAUDE.md` and other docs. The build works because the solution references the file as-is. |
| **Impact**      | Confusing for new developers. May cause issues with tooling that expects conventional naming.                                                                                                            |
| **Remediation** | Rename to `AuctionKings.Application.csproj` and update all references (`AuctionKings.sln`, Dockerfile, CI pipeline).                                                                                     |

### L-3. `TreatWarningsAsErrors` Is Commented Out

|                 |                                                                                                                         |
| --------------- | ----------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `Directory.Build.props:8`                                                                                               |
| **Description** | The `TreatWarningsAsErrors` setting is commented out. Analyzer warnings (nullable, code style) will not fail the build. |
| **Impact**      | Warnings accumulate silently. Potential nullable reference violations or code quality issues slip through CI.           |
| **Remediation** | Uncomment `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Fix existing warnings first.                           |

### L-4. Unused AWS Packages in `Directory.Packages.props`

|                 |                                                                                                                                                                                                                                                                                                                                                                                                                            |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `Directory.Packages.props:39-49`                                                                                                                                                                                                                                                                                                                                                                                           |
| **Description** | The following AWS packages are declared in central package management but only `AWSSDK.SQS` is referenced in any `.csproj`: `AWSSDK.DynamoDBv2`, `AWSSDK.SimpleNotificationService`, `AWSSDK.S3`, `AWSSDK.CognitoIdentityProvider`, `AWSSDK.SecretsManager`, `AWSSDK.AppConfig`, `AWSSDK.CloudWatch`, `AWSSDK.CloudWatchLogs`, `AWSSDK.XRay`, `AWSSDK.EventBridge`, `Kralizek.Extensions.Configuration.AWSSecretsManager`. |
| **Impact**      | No runtime impact (unused packages aren't restored unless referenced), but it inflates the package manifest and creates confusion about actual dependencies.                                                                                                                                                                                                                                                               |
| **Remediation** | Remove unused package version declarations from `Directory.Packages.props`, or annotate them as planned-but-not-yet-used.                                                                                                                                                                                                                                                                                                  |

### L-5. Design-Time DB Factory Has Hardcoded Credentials

|                 |                                                                                                                                                                                                                     |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.Infrastructure/Persistence/AuctionDbContextFactory.cs:15`                                                                                                                                |
| **Description** | The EF Core design-time factory uses `Password=dev` hardcoded in the connection string. This only runs during `dotnet ef` migrations and is a common pattern, but credentials in source code are a hygiene concern. |
| **Remediation** | Use environment variables in the design-time factory: `Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "dev"`.                                                                                                 |

### L-6. Grafana Uses Default `admin/admin` Credentials

|                 |                                                                                                                                                                              |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `docker/docker-compose.infra.yml:129-130`                                                                                                                                    |
| **Description** | `GF_SECURITY_ADMIN_USER: admin` and `GF_SECURITY_ADMIN_PASSWORD: admin` are hardcoded. While acceptable for local dev, this is risky if the compose file is used in staging. |
| **Remediation** | Use Docker secrets or environment variables for Grafana credentials.                                                                                                         |

### L-7. `X-XSS-Protection` Header Is Deprecated

|                 |                                                                                                                                                                                                |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Location**    | `src/services/AuctionKings.API/Middleware/SecurityHeadersMiddleware.cs:9`                                                                                                                      |
| **Description** | The `X-XSS-Protection: 1; mode=block` header is deprecated in modern browsers and can actually _introduce_ XSS vulnerabilities in older IE versions. Modern browsers have removed XSS Auditor. |
| **Remediation** | Remove the header or set it to `X-XSS-Protection: 0`. Rely on `Content-Security-Policy` instead.                                                                                               |

---

## Inconsistencies

| #   | Issue                                      | Location                                   | Details                                                                                                                                                                                                                                                                                 |
| --- | ------------------------------------------ | ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| I-1 | `Activate()` never called                  | `Auction.cs:81`                            | Auctions are created as `Pending` but no code transitions them to `Active`. The `CreateAuctionCommandHandler` doesn't call `Activate()`. Queries filter by `AuctionStatus.Active`, meaning newly created auctions are invisible until... nothing activates them.                        |
| I-2 | `now` param unused in `Activate()`         | `Auction.cs:81`                            | The `DateTime now` parameter is accepted but never used.                                                                                                                                                                                                                                |
| I-3 | `now` param unused in `Cancel()`           | `Auction.cs:160`                           | Same issue — `DateTime now` is accepted but never used.                                                                                                                                                                                                                                 |
| I-4 | Missing `AddApplication()` in Workers      | `BackgroundWorkers/Program.cs`             | Workers call `AddInfrastructure()` but not `AddApplication()`. MediatR, FluentValidation, and Mapster are not registered. Yet `AuctionCloseWorker` resolves `ISender` (MediatR). This likely fails at runtime if MediatR handlers aren't registered.                                    |
| I-5 | `PlaceBid` currency mismatch not validated | `PlaceBidCommandHandler.cs`                | The bid's `Currency` from the request is used to construct `Money`, but there's no check that it matches the auction's `Currency`. The domain `Money` operations throw on currency mismatch, but this surfaces as an unhandled exception rather than a proper validation error.         |
| I-6 | `Auction.Version` vs EF `xmin`             | `Auction.cs:27`, `AuctionConfiguration.cs` | The `Auction` entity has a `Version` property that's manually incremented, AND the EF config uses PostgreSQL `xmin` as a concurrency token. These are redundant — the domain `Version` increment + the `xmin` check provide double concurrency protection but with different semantics. |

---

## Unused/Dead Code Summary

| File/Symbol                                   | Type             | Reason                                                                    |
| --------------------------------------------- | ---------------- | ------------------------------------------------------------------------- |
| `UsersControllers.cs`                         | File             | Empty stub — comment only                                                 |
| `RequestLoggingMiddleware.cs`                 | Class            | Never registered in middleware pipeline                                   |
| `Auction.Cancel()`                            | Method           | No command, controller, or worker invokes it                              |
| `Auction.Activate()`                          | Method           | Never called from any handler or worker                                   |
| `AuctionStatus.Cancelled`                     | Enum value       | Unreachable via any code path                                             |
| `AuctionStatus.Pending`                       | Enum value       | Auctions created as Pending are never transitioned — effectively orphaned |
| 11 AWS packages in `Directory.Packages.props` | Package versions | Declared but never referenced in any `.csproj`                            |

---

## Positive Findings

1. **Clean Architecture** — Layer dependencies are correctly enforced (Domain has no outward deps, Application depends only on Domain/Shared).
2. **Optimistic Concurrency** — Bid placement uses retry loop with `Version` tracking and `xmin` concurrency token.
3. **Outbox Pattern** — Domain events are reliably persisted and processed with `SKIP LOCKED`, retry counts, and failure tracking.
4. **Idempotency** — `X-Request-Id` header enforcement on POST/PUT with `IdempotencyBehavior` pipeline.
5. **Security Headers** — HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy are all set.
6. **Docker Best Practices** — Multi-stage build, non-root user, minimal runtime image, healthcheck.
7. **Secrets Management** — Docker Compose uses file-based secrets for Postgres and Redis passwords.
8. **Structured Logging** — Serilog with JSON formatting, correlation IDs, and request enrichment.
9. **Health Checks** — Three tiers: liveness, readiness (DB + Redis + Outbox), and DB-only.
10. **Domain Validation** — Value objects enforce invariants (`Money` rejects negatives, `AuctionDuration` enforces min/max).
11. **FluentValidation Pipeline** — All commands have validators running before handlers.
12. **Prometheus Metrics** — HTTP metrics and custom bid counter exposed at `/metrics`.

---

## Production Readiness Checklist

| Check                                | Status                                                              |
| ------------------------------------ | ------------------------------------------------------------------- |
| All endpoints require authentication | ❌ Hubs, CreateAuction, all GETs are anonymous                      |
| CORS policy configured               | ❌ Missing entirely                                                 |
| No hardcoded secrets in source       | ⚠️ Dev JWT secret in appsettings.Development.json                   |
| Rate limiting on all write endpoints | ⚠️ `RateLimitAttribute` exists but is not applied to any controller |
| TLS enforced                         | ✅ HSTS header + `UseHttpsRedirection()`                            |
| Security headers                     | ⚠️ CSP missing, X-XSS-Protection deprecated                         |
| Concurrency protection               | ✅ Optimistic concurrency with retries                              |
| Idempotency on writes                | ✅ Via `X-Request-Id` + `IdempotencyBehavior`                       |
| Health checks                        | ✅ Liveness, readiness, DB                                          |
| Structured logging                   | ✅ Serilog JSON                                                     |
| Docker non-root                      | ✅ `appuser`                                                        |
| Warnings as errors                   | ❌ Commented out                                                    |
| All domain methods reachable         | ❌ `Activate()`, `Cancel()` are dead code                           |
| Worker services functional           | ⚠️ Missing `AddApplication()` registration                          |
