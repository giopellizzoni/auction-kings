# AuctionKings — Security & Code Audit

**Date:** 2026-04-16
**Sources:** Three independent audit passes (security-auditor, codex, claude-code agents)
**Scope:** Full codebase — API, Application, Domain, Infrastructure, Workers, Docker, Build configuration

---

## Executive Summary

| Severity | Count |
| -------- | ----- |
| CRITICAL | 4     |
| HIGH     | 7     |
| MEDIUM   | 21    |
| LOW      | 8     |
| INFO     | 4     |

Top priorities: **unauthenticated SignalR hubs**, **missing authorization on `CreateAuction`**, **IDOR via request-body identity fields**, **auctions stuck in Pending state forever**, and **no CORS policy**.

---

## CRITICAL

| ID  | Finding                                                                                                                                             | Location                                             |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| C-1 | SignalR hubs have no `[Authorize]` — anonymous WebSocket clients can connect, join groups, enumerate auction state, flood connections               | `AuctionHub.cs:8`, `NotificationHub.cs:6`            |
| C-2 | `CreateAuction` endpoint has no `[Authorize]` — anonymous callers can create arbitrary auctions                                                     | `AuctionsController.cs:34`                           |
| C-3 | IDOR — `SellerId` and `BidderId` accepted from request body; server never validates against the JWT `sub` claim, enabling identity spoofing         | `AuctionsController.cs:43`, `BidsController.cs:47`   |
| C-4 | `Auction.Activate()` is never called — auctions are created as `Pending` and never transition to `Active`; `GetActiveAuctions` always returns empty | `CreateAuctionCommandHandler.cs:34`, `Auction.cs:81` |

**C-1 remediation:** Add `[Authorize]` to both hub classes. Configure JWT bearer token transport for WebSocket connections in `Program.cs` (the `access_token` query-string approach for SignalR).

**C-2 remediation:** Add `[Authorize(Roles = "Seller")]` to `CreateAuction`.

**C-3 remediation:** Remove `SellerId`/`BidderId` from request DTOs. Extract the user ID from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` in the controller and inject it into the command.

**C-4 remediation:** Call `auction.Activate(dateTimeProvider.UtcNow)` in `CreateAuctionCommandHandler` after persisting, or introduce an explicit activation step.

---

## HIGH

| ID  | Finding                                                                                                                                                | Location                                        |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------- |
| H-1 | No CORS configuration — no `AddCors()`/`UseCors()` in pipeline; cross-origin requests may work unrestricted                                            | `Program.cs`                                    |
| H-2 | `DevController` token endpoint insufficient production guard — relies solely on `ASPNETCORE_ENVIRONMENT`; misconfiguration exposes admin token minting | `DevController.cs:27`                           |
| H-3 | Unauthenticated read endpoints — `GetUserBidHistory` exposes any user's complete bid history to anonymous callers; GUIDs are enumerable                | `UsersController.cs:19`, `BidsController.cs:66` |
| H-4 | Rate limiter fails open — Redis unavailability silently disables all rate limiting                                                                     | `RedisRateLimiter.cs:72`                        |
| H-5 | JWT dev secret committed to source control — if used in staging/production, attackers can forge valid tokens                                           | `appsettings.Development.json:12`               |
| H-6 | `[RateLimitAttribute]` defined but never applied to any controller or action — rate limiting is entirely inactive                                      | `Filters/RateLimitAttribute.cs`                 |
| H-7 | No SignalR connection/method throttling — hub methods accept unlimited concurrent calls, enabling resource exhaustion                                  | `AuctionHub.cs`                                 |

**H-1 remediation:** Add explicit CORS policy restricting allowed origins, methods, and headers.

**H-2 remediation:** Conditionally register `DevController` only in Development via `#if DEBUG` or conditional route registration. Consider removing it and using a separate dev-token tool.

**H-3 remediation:** Add `[Authorize]` to `GetUserBidHistory` and validate the authenticated user can only access their own history (or has Admin role).

**H-4 remediation:** Fail closed (deny) for write operations when Redis is unavailable. Fail open only for reads. Add alerting on rate limiter failures.

**H-5 remediation:** Move JWT secrets to environment variables or a secrets manager. Use `dotnet user-secrets` locally. Add a startup check that throws if `Jwt:Secret` matches the dev placeholder in non-Development environments.

**H-6 remediation:** Apply `[RateLimit]` to `PlaceBid` and `CreateAuction`, or remove the attribute and use ASP.NET Core's built-in rate limiting middleware instead.

**H-7 remediation:** Add per-connection and per-method throttling in hub methods (e.g. a semaphore or rate-limit check at the start of each hub method).

---

## MEDIUM

| ID   | Finding                                                                                                                                              | Location                                                            |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| M-1  | `AuctionCloseWorker` uses `DateTime.UtcNow` directly instead of `IDateTimeProvider` — untestable and inconsistent with Application layer             | `AuctionCloseWorker.cs:50`                                          |
| M-2  | `OutboxProcessor` uses `DateTime.UtcNow` directly for `MarkAsProcessed`                                                                              | `OutboxProcessor.cs:104`                                            |
| M-3  | `Auction.Activate()` has no end-time validation — an already-expired auction can be activated; `now` parameter is accepted but unused                | `Auction.cs:81`                                                     |
| M-4  | `Auction.Cancel()` is dead code — no command, controller, or worker calls it; `now` parameter accepted but unused                                    | `Auction.cs:160`                                                    |
| M-5  | Missing `Content-Security-Policy` header                                                                                                             | `SecurityHeadersMiddleware.cs`                                      |
| M-6  | `RequestLoggingMiddleware` exists but is never registered in the pipeline; Serilog is used instead, making this class dead code                      | `RequestLoggingMiddleware.cs`, `Program.cs`                         |
| M-7  | Idempotency race condition — `ExistsAsync` check and `StoreAsync` are not atomic; concurrent requests with the same key can both pass through        | `IdempotencyBehavior.cs:30-38`                                      |
| M-8  | `RemoveByPrefixAsync` uses SCAN — not cluster-safe; targets only one node in Redis Cluster                                                           | `RedisCacheService.cs:88`                                           |
| M-9  | Identity claim extraction inconsistent — JWT emits `sub`, hub reads `ClaimTypes.NameIdentifier`, rate limiter reads `User.Identity?.Name`            | `JwtTokenGenerator.cs`, `HubExtensions.cs`, `RateLimitAttribute.cs` |
| M-10 | Inconsistent error handling in `Auction` aggregate — `Create` throws exceptions, `PlaceBid`/`Close` return `Result`                                  | `Auction.cs`                                                        |
| M-11 | Inconsistent validator access modifiers — `CreateAuctionCommandValidator` is `public`, others are `internal`                                         | Application validators                                              |
| M-12 | `MapError()` helper duplicated in `AuctionsController` and `BidsController` — should be extracted to a shared base or extension                      | `AuctionsController.cs`, `BidsController.cs`                        |
| M-13 | `UsersController` error handling pattern differs from `AuctionsController` and `BidsController`                                                      | `UsersController.cs`                                                |
| M-14 | `Auction.Cancel()` emits no domain event — state change occurs with no `AuctionCancelledEvent` raised                                                | `Auction.cs:160`                                                    |
| M-15 | `IConnectionMultiplexer` is not disposed on shutdown                                                                                                 | `DependencyInjection.cs:64`                                         |
| M-16 | `TreatWarningsAsErrors` is commented out — analyzer warnings (nullable, code style) silently pass CI                                                 | `Directory.Build.props:8`                                           |
| M-17 | Idempotency key accepts arbitrary strings — no length or format validation; oversized keys could cause DB issues                                     | `IdempotencyFilter.cs:35`                                           |
| M-18 | `AddApplication()` not called in `BackgroundWorkers` — MediatR, FluentValidation, Mapster unregistered; `ISender` resolution likely fails at runtime | `BackgroundWorkers/Program.cs`                                      |
| M-19 | `PlaceBid` currency mismatch not validated at the API layer — domain `Money` throws on mismatch, surfacing as an unhandled 500                       | `PlaceBidCommandHandler.cs`                                         |
| M-20 | `Auction.Version` (manually incremented) + EF `xmin` concurrency token — redundant double concurrency protection with different semantics            | `Auction.cs:27`, `AuctionConfiguration.cs`                          |
| M-21 | `ConnectionTracker` Redis ops not atomic — `SetAdd` + `KeyExpire` are separate calls; a crash between them leaves keys without TTL                   | `ConnectionTracker`                                                 |

---

## LOW

| ID  | Finding                                                                                                                           | Location                          |
| --- | --------------------------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| L-1 | `UsersControllers.cs` (plural typo) — empty stub containing only a comment; misleading during audits and searches                 | `Controllers/UsersControllers.cs` |
| L-2 | Application `.csproj` filename typo — `Applciation` instead of `Application`                                                      | `AuctionKings.Applciation.csproj` |
| L-3 | Design-time DB factory has hardcoded `Password=dev` in connection string                                                          | `AuctionDbContextFactory.cs:15`   |
| L-4 | Grafana default `admin/admin` credentials in docker-compose — risky if used in staging                                            | `docker-compose.infra.yml:129`    |
| L-5 | `X-XSS-Protection` header is deprecated — can introduce XSS vulnerabilities in older IE; modern browsers have removed XSS Auditor | `SecurityHeadersMiddleware.cs:9`  |
| L-6 | 11 unused AWS package version declarations in central package management — inflates manifest, creates confusion                   | `Directory.Packages.props:39-49`  |
| L-7 | `Money.Zero()` factory method is unused across the codebase                                                                       | `Money.cs:24`                     |
| L-8 | `NuGet.Packaging` / `NuGet.Protocol` 6.12.1 NU1901 low-severity vulnerability (`GHSA-g4vj-cjjj-v7hg`)                             | `Directory.Packages.props`        |

---

## INFO

| ID  | Finding                                                                                                                            | Location                       |
| --- | ---------------------------------------------------------------------------------------------------------------------------------- | ------------------------------ |
| I-1 | No config validation on startup in `BackgroundWorkers` — missing `ValidateOnStart` means misconfiguration only surfaces at runtime | `BackgroundWorkers/Program.cs` |
| I-2 | SQS publish failures silently swallowed — no re-throw or dead-letter path                                                          | `SqsMessagePublisher.cs:47`    |
| I-3 | No authorization enforcement tests — missing test coverage for protected endpoints returning 401/403                               | `test/`                        |
| I-4 | No SignalR hub authentication tests — missing test coverage for anonymous vs authenticated hub access                              | `test/`                        |
