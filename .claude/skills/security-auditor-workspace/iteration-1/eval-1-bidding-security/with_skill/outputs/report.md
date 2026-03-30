# Security Audit Report — AuctionKings
Date: 2026-03-29

## Executive Summary

The auction bidding flow has one Critical vulnerability — `PlaceBidCommand` and `CreateAuctionCommand` accept a caller-supplied `BidderId`/`SellerId` in the request body with no verification against the authenticated principal, enabling any authenticated user to place bids or create auctions as any other user. This is compounded by the complete absence of `AddAuthentication()` and `[Authorize]` attributes across all controllers and hubs, meaning in the current state every endpoint is publicly accessible without a token. The domain layer itself is well-structured: self-bid protection, minimum-bid enforcement, optimistic concurrency, and anti-sniping are all correctly implemented in the aggregate. The infrastructure layer has a hardcoded development credential in the design-time factory and `AllowAdmin = true` on Redis, but the Docker composition correctly externalises secrets via files. The application is not production-ready — the two Critical issues must be resolved before any public deployment.

---

## Findings

### CRITICAL

#### C-1: Bidder Identity Never Verified Against Authenticated Principal (IDOR / Horizontal Privilege Escalation)
- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs:8` and `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs:46`
- **Description**: `PlaceBidCommand` includes a `BidderId: Guid` field that the caller supplies in the request body. `PlaceBidCommandHandler.Handle` passes `request.BidderId` directly to `auction.PlaceBid(request.BidderId, amount, ...)` without ever comparing it to the authenticated user's identity (e.g., a `sub` or `nameidentifier` claim from the JWT). There is no `ICurrentUserService` or equivalent abstraction anywhere in the handler or command.
- **Impact**: Any authenticated user can forge a bid on behalf of any other user by supplying a different `BidderId` GUID. This lets an attacker drain a competitor's bid budget, manipulate auction outcomes, and falsely attribute winning bids. Because `BidderId == SellerId` is the only self-bid guard, the attacker could also circumvent the seller-exclusion rule by using the seller's own GUID as `BidderId` while sending the request as a different user.
- **Remediation**: Inject an `ICurrentUserService` that resolves the `Guid` from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)`. Remove `BidderId` from the command record and derive it exclusively server-side in the handler: `var bidderId = _currentUserService.UserId;`. Apply the same pattern to `CreateAuctionCommand.SellerId`.

---

#### C-2: Auction Ownership Never Verified for CloseAuction (Unauthorized Closure)
- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandHandler.cs:30-59`
- **Description**: `CloseAuctionCommandHandler.Handle` loads the auction, calls `auction.Close(now)`, and commits — without checking that the authenticated caller is the auction's `SellerId`. `CloseAuctionCommand` carries only `AuctionId`; there is no ownership assertion anywhere in the handler or the domain method.
- **Impact**: Any authenticated user who knows (or guesses) an `AuctionId` can close any auction prematurely, robbing the legitimate seller of further bids and potentially awarding the item to the current leader at a below-market price. The `WinnerDeterminedEvent` and `AuctionClosedEvent` are both raised, making the closure indistinguishable from a legitimate one.
- **Remediation**: After loading the auction, assert `auction.SellerId == _currentUserService.UserId` and return `Result.Failure(DomainErrors.Auction.Forbidden)` (add this error) if the check fails. Similarly protect any future `CancelAuction` command.

---

#### C-3: No Authentication Scheme Registered — All Endpoints Are Publicly Accessible
- **Location**: `src/services/AuctionKings.API/Program.cs:35`
- **Description**: `Program.cs` calls `app.UseAuthorization()` but never calls `builder.Services.AddAuthentication(...)`. No JWT Bearer scheme, no cookie scheme, nothing is configured. This means `[Authorize]` attributes — if added — would silently return 401 for all requests, and without those attributes every endpoint is fully anonymous. Currently no controller or hub carries `[Authorize]`, making every route publicly reachable.
- **Impact**: An unauthenticated attacker can call `POST /api/auctions`, `POST /api/bids`, and `DELETE /api/auctions/{id}` (once implemented) without a token. Combined with C-1 and C-2, the entire bidding flow is unauthenticated and identity-spoofable.
- **Remediation**: Add `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => { ... })` in `Program.cs`, call `app.UseAuthentication()` before `app.UseAuthorization()`, and decorate all controllers and hubs with `[Authorize]`.

---

### HIGH

#### H-1: PlaceBidCommand Does Not Implement IIdempotentCommand — Duplicate Bids Possible
- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs:6-10` and `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs:23`
- **Description**: `IdempotencyBehavior` checks `if (request is not IIdempotentCommand)` and skips deduplication for any command that does not implement the interface. `PlaceBidCommand` does not implement `IIdempotentCommand`. A network retry or duplicate POST will produce two separate bids, each with a new `Bid.Id`, both committed to the database.
- **Impact**: A client experiencing a network timeout will retry and the user may be debited twice, or the auction's `CurrentPrice` may jump by two increments instead of one, confusing other bidders. More dangerously, an automated script can spam bids to lock out other participants before the concurrency token catches up.
- **Remediation**: Implement `IIdempotentCommand` on `PlaceBidCommand` by adding `string IdempotencyKey { get; }` and requiring the caller to supply an idempotency key (e.g., from the `Idempotency-Key` request header, extracted by the controller). The `IdempotencyBehavior` pipeline will then block replay automatically.

---

#### H-2: CreateAuctionCommand.SellerId Accepted From Client — Seller Identity Spoofable
- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs:6-12` and `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommandHandler.cs:34-40`
- **Description**: `CreateAuctionCommand` includes `SellerId: Guid` as a client-supplied field. `CreateAuctionCommandHandler` passes it directly to `Auction.Create(..., request.SellerId)`. There is no validation that `request.SellerId` matches the authenticated user.
- **Impact**: Any user can create auctions attributed to any other user's ID. This means a malicious user can list items on behalf of another seller, and that seller will be unable to close or cancel the auction (since ownership checks — once added — will compare against the forged `SellerId`). It also undermines any future seller reputation or review system.
- **Remediation**: Same pattern as C-1: remove `SellerId` from the command record and resolve it exclusively from `ICurrentUserService.UserId` inside the handler.

---

#### H-3: AuctionHub and NotificationHub Lack [Authorize] — Anonymous WebSocket Connections Accepted
- **Location**: `src/services/AuctionKings.API/Hubs/AuctionHub.cs:7` and `src/services/AuctionKings.API/Hubs/NotificationHub.cs:7`
- **Description**: Neither hub class carries an `[Authorize]` attribute. Anonymous clients can connect to `/hubs/auction` and join any auction group (via `JoinAuctionAsync`), receiving all `BidPlaced` and `AuctionClosed` events including `BidderId` and exact bid amounts in real-time.
- **Impact**: Any anonymous visitor receives the full live bid stream for every auction they subscribe to. This exposes bidder identity (`BidderId` is a stable GUID) and precise bid timing — information that can be used to snipe or strategically outbid.
- **Remediation**: Add `[Authorize]` to both hub classes. Configure JWT token extraction for WebSocket in `Program.cs` via `options.Events.OnMessageReceived = context => { var token = context.Request.Query["access_token"]; if (!string.IsNullOrEmpty(token)) context.Token = token; return Task.CompletedTask; };`.

---

#### H-4: GlobalExceptionFilter Is an Empty Stub — Unhandled Exceptions Leak Stack Traces
- **Location**: `src/services/AuctionKings.API/Filters/GlobalExceptionFilter.cs:1-6`
- **Description**: `GlobalExceptionFilter` is an empty class. It does not implement `IExceptionFilter` or `IAsyncExceptionFilter`. It is not registered in `Program.cs`. Any unhandled exception — including `InvalidOperationException` from `Money.EnsureSameCurrency`, `ArgumentException` from `Auction.Create`, or EF Core errors — will propagate to ASP.NET Core's default exception handler, which may return a 500 with a full stack trace in development mode and a generic 500 in production.
- **Impact**: In development (or if `ASPNETCORE_ENVIRONMENT` is misconfigured in production), an attacker can trigger validation paths that throw unhandled exceptions to enumerate internal class names, method signatures, connection string fragments from EF exception messages, and file paths.
- **Remediation**: Implement `GlobalExceptionFilter` as `IAsyncExceptionFilter`, map known exception types (`ValidationException`, `DuplicateCommandException`, `ConcurrencyException`) to appropriate HTTP status codes, and return a structured `ProblemDetails` response. Register it with `builder.Services.AddControllers(options => options.Filters.Add<GlobalExceptionFilter>())`.

---

### MEDIUM

#### M-1: Bid Currency Not Validated Against Auction Currency — Cross-Currency Bid Accepted
- **Location**: `src/services/AuctionKings.Domain/Aggregates/Auction.cs:97-101` and `src/services/AuctionKings.Domain/ValueObjects/Money.cs:63-70`
- **Description**: `Auction.PlaceBid` calls `BidIncrement.GetMinimumBid(CurrentPrice)` which returns a `Money` in the auction's currency, then compares `amount < minimumBid` using the `<` operator. That operator calls `EnsureSameCurrency`, which throws `InvalidOperationException` if currencies differ — it does not return a `Result` failure. This exception is unhandled in `PlaceBidCommandHandler` (only `ConcurrencyException` is caught), so a cross-currency bid causes a 500 response instead of a clean `400 Bad Request`.
- **Impact**: A client submitting a bid with `Currency = "JPY"` on a `USD` auction receives a 500 error and the exception bubbles through the pipeline, potentially leaking internal details (see H-4). More importantly, there is no domain-level `Result` guard that explicitly rejects cross-currency bids — a future refactor of `EnsureSameCurrency` to be non-throwing would silently allow currency confusion.
- **Remediation**: Add an explicit currency check at the start of `PlaceBid`: `if (!amount.Currency.Equals(CurrentPrice.Currency, StringComparison.OrdinalIgnoreCase)) return Result.Failure<Bid>(DomainErrors.Auction.CurrencyMismatch);`. Add `CurrencyMismatch` to `DomainErrors.Auction`.

---

#### M-2: Auction.Close() Allows Direct Pending→Closed Transition (State Machine Gap)
- **Location**: `src/services/AuctionKings.Domain/Aggregates/Auction.cs:127-158`
- **Description**: `Close()` only guards against `Closed` and `Cancelled` states. An auction in `Pending` status (never activated) can be closed directly. The state machine path `Pending → Closed` is unintended — an auction that was never active has no bids, so `WinnerId` will be null and a `AuctionClosedEvent` is raised for an auction that never ran.
- **Impact**: An attacker (or a bug in the background activation job) can close a `Pending` auction, permanently preventing it from running. The event is published to the outbox and downstream consumers will receive a spurious closure. Any refund or notification logic triggered by `AuctionClosedEvent` will fire for an auction with zero bids.
- **Remediation**: Add `if (Status != AuctionStatus.Active) return Result.Failure(DomainErrors.Auction.NotActive);` at the top of `Close()`, or more precisely `if (Status is not AuctionStatus.Active and not AuctionStatus.Pending) ...` depending on business rules. At minimum, document the intended valid transitions.

---

#### M-3: RateLimitAttribute Not Applied to Any Endpoint — Rate Limiting Dead Code
- **Location**: `src/services/AuctionKings.API/Filters/RateLimitAttribute.cs:8` and all controller files
- **Description**: `RateLimitAttribute` is correctly implemented and `IRateLimiter` is registered in DI. However, no controller or action method carries `[RateLimit]`. The bid placement path — the highest-value target for abuse — has no rate limiting applied.
- **Impact**: An attacker with a valid account can submit thousands of bids per second, overwhelming the concurrency retry loop (3 attempts), exhausting PostgreSQL connections, and performing a low-cost denial-of-service against the bidding infrastructure.
- **Remediation**: Apply `[RateLimit(policy: "bid", retryAfterSeconds: 10)]` to the `PlaceBid` action (once implemented), and a stricter policy to `CreateAuction`. Consider also applying `RateLimitAttribute` at the hub level by creating an equivalent filter for SignalR `OnConnectedAsync`.

---

#### M-4: AllowAdmin = true on Redis ConnectionMultiplexer
- **Location**: `src/services/AuctionKings.Infrastructure/DependencyInjection.cs:53`
- **Description**: The Redis `ConnectionMultiplexer` is configured with `redisConfig.AllowAdmin = true`. This grants access to dangerous server-level commands including `FLUSHDB`, `CONFIG SET`, `DEBUG SLEEP`, `DEBUG OBJECT`, and `MONITOR`.
- **Impact**: If an attacker compromises the application process (e.g., via a deserialization vulnerability), they can flush the entire Redis database (destroying all idempotency keys and cached data), reconfigure Redis in-process, or use `DEBUG SLEEP` to stall the server. `AllowAdmin` is also required for the SignalR Redis backplane's `SCAN` operation, so removing it blindly would break backplane cleanup.
- **Remediation**: Audit which specific `AllowAdmin`-gated operations are actually used. If only `SCAN` is needed for cache prefix invalidation in `RedisCacheService`, consider using Lua scripting instead and removing `AllowAdmin`. At minimum, document the requirement and ensure the Redis instance uses a strong password (already done in Docker compose) and network isolation.

---

#### M-5: Connection Strings with Credentials Committed to Source Control
- **Location**: `src/services/AuctionKings.API/appsettings.json:10-11` and `src/services/AuctionKings.Infrastructure/Persistence/AuctionDbContextFactory.cs:15`
- **Description**: `appsettings.json` contains a PostgreSQL connection string with `Username=dev;Password=` (empty password). `AuctionDbContextFactory.cs` hardcodes `Password=dev` in a design-time connection string. Both files are committed to source control.
- **Impact**: Even with an empty password in `appsettings.json`, the username, host, port, and database name are disclosed. The design-time factory's `Password=dev` is a real credential. If the dev database is reachable (e.g., in a misconfigured CI environment or if port 5432 is accidentally exposed), these credentials provide direct database access.
- **Remediation**: Replace `appsettings.json` connection strings with placeholder values (e.g., `"Host=localhost;..."` with no credentials) and use `dotnet user-secrets` for local development. For `AuctionDbContextFactory`, read from environment variables or user secrets rather than hardcoding. In production, inject via environment variables or a secrets manager.

---

#### M-6: PostgreSQL and Redis Ports Bound to All Interfaces in Docker Compose
- **Location**: `docker/docker-compose.infra.yml:23` and `docker/docker-compose.infra.yml:49`
- **Description**: Both port mappings use `"${POSTGRES_HOST_PORT}:${POSTGRES_CONTAINER_PORT}"` and `"${REDIS_HOST_PORT}:${REDIS_CONTAINER_PORT}"` without binding to `127.0.0.1`. Docker's default port binding maps to `0.0.0.0`, exposing the services on all network interfaces of the host machine.
- **Impact**: On a developer machine connected to a corporate or public WiFi network, PostgreSQL and Redis are reachable from the local subnet. Redis is password-protected (good), but PostgreSQL's protection depends entirely on pg_hba.conf defaults. An attacker on the same network can attempt direct database connections.
- **Remediation**: Change port bindings to `"127.0.0.1:${POSTGRES_HOST_PORT}:${POSTGRES_CONTAINER_PORT}"` for both services, limiting exposure to the loopback interface only.

---

### LOW

#### L-1: AntiSniping Threshold and Extension Are Hardcoded Constants
- **Location**: `src/services/AuctionKings.Domain/Aggregates/Auction.cs:11-12`
- **Description**: `AntiSnipingThreshold` (5 min) and `AntiSnipingExtension` (5 min) are `static readonly` fields on the aggregate. They cannot be changed without redeployment, and there is no per-auction override.
- **Impact**: Not directly exploitable, but hardcoded business rules reduce operational flexibility. If the anti-sniping window is too small, it fails to prevent sniping; if too large, it can extend auctions indefinitely for a determined bidder. An attacker who understands the constant can plan bids to trigger the maximum number of extensions.
- **Remediation**: Extract these into a configuration class (`AuctionDomainOptions`) registered via `IOptions<AuctionDomainOptions>` and injected into the `PlaceBid` method or passed via `Auction.Create`. This allows per-environment tuning without code changes.

---

#### L-2: TreatWarningsAsErrors Is Commented Out
- **Location**: `Directory.Build.props:8`
- **Description**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is commented out. The `.NET analyzers` and `EnforceCodeStyleInBuild` are enabled, but their warnings do not fail the build.
- **Impact**: Security-relevant warnings (nullable dereference warnings that could mask null checks, deprecated API usage, CA2100 SQL injection warnings) will silently pass CI. A developer may introduce a null-dereference on `Context.User` in a hub method and never see a build failure.
- **Remediation**: Re-enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Fix any existing warnings before enabling to avoid build breakage.

---

#### L-3: IdempotencyFilter Is an Empty Stub
- **Location**: `src/services/AuctionKings.API/Filters/IdempotencyFilter.cs:3`
- **Description**: `IdempotencyFilter` is declared as an empty class. The `IdempotencyBehavior` in the MediatR pipeline handles idempotency at the application layer, but the HTTP-layer filter that should extract the `Idempotency-Key` header and route it into the command is a no-op stub.
- **Impact**: Without the HTTP filter, there is no mechanism to extract a client-supplied idempotency key from the `Idempotency-Key` request header and populate `IIdempotentCommand.IdempotencyKey` on the command. Even if `PlaceBidCommand` were to implement `IIdempotentCommand` (see H-1), duplicate detection would not work end-to-end until this filter is implemented.
- **Remediation**: Implement `IdempotencyFilter` as `IAsyncActionFilter`: read the `Idempotency-Key` header, store it in `HttpContext.Items`, and have the controller pass it into the command constructor.

---

#### L-4: AllowedHosts Set to Wildcard
- **Location**: `src/services/AuctionKings.API/appsettings.json:8`
- **Description**: `"AllowedHosts": "*"` accepts requests with any `Host` header value. This does not restrict host header injection attacks in the current setup, but it is a hardening gap.
- **Impact**: Host header injection can affect password reset links, canonical URL generation, or cache poisoning if any middleware or component uses `Request.Host` to construct URLs.
- **Remediation**: Set `AllowedHosts` to the known production hostname(s) (e.g., `"api.auctionkings.com"`) in the production `appsettings` or environment-specific override.

---

#### L-5: No HSTS, No Security Headers
- **Location**: `src/services/AuctionKings.API/Program.cs`
- **Description**: `UseHsts()` is absent. No middleware sets `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, or `Referrer-Policy` headers.
- **Impact**: Browsers will not enforce HTTPS for return visits (missing HSTS), and responses can be rendered in iframes or with MIME-sniffing. For a pure JSON API the risk is lower, but the absence of `X-Content-Type-Options: nosniff` is a baseline gap.
- **Remediation**: Add `app.UseHsts()` inside an `if (!app.Environment.IsDevelopment())` block. Add a minimal security headers middleware that sets `X-Content-Type-Options: nosniff` and `X-Frame-Options: DENY` on every response.

---

## Positive Findings

- **Self-bid protection** is correctly implemented at the domain level: `Auction.PlaceBid` returns `DomainErrors.Auction.CannotBidOnOwnAuction` when `bidderId == SellerId` (`Auction.cs:92-95`). This is domain-enforced, not just validator-enforced.
- **Minimum bid increment** is enforced in the domain aggregate (`Auction.cs:97-101`), not only in the FluentValidation validator. A validator bypass cannot skip this check.
- **Optimistic concurrency** uses two independent tokens: an application-level `Version` integer (`AuctionConfiguration.cs:69`) and the PostgreSQL `xmin` shadow property (`AuctionConfiguration.cs:73-78`). Both are `IsConcurrencyToken()`, so a concurrent update will raise `DbUpdateConcurrencyException` regardless of which layer catches it.
- **Concurrency retry loop** in `PlaceBidCommandHandler` retries 3 times on `ConcurrencyException` before returning a failure (`PlaceBidCommandHandler.cs:36-68`). This correctly handles transient collisions without surfacing them to the user.
- **Anti-sniping** is implemented in the domain aggregate (`Auction.cs:116-122`): bids placed within 5 minutes of `EndTime` extend it by 5 minutes, and an `AuctionExtendedEvent` is raised for downstream notification.
- **MediatR pipeline order** is correct: `Logging → ValidationBehavior → IdempotencyBehavior → PerformanceBehavior → Handler` (`DependencyInjection.cs:20-23`). Validation fires before idempotency deduplication.
- **FluentValidation is registered for internal types** (`includeInternalTypes: true`) ensuring internal validators (e.g., `PlaceBidCommandValidator`) are discovered automatically.
- **Redis authentication** is configured in Docker compose via `--requirepass` backed by a Docker secret file, not a hardcoded env var (`docker-compose.infra.yml:29-32`).
- **SignalR notifications use group scoping**, not `Clients.All`: `SignalRNotificationService` sends to `Groups.Group($"auction-{auctionId}")` (`SignalRNotificationService.cs:16-18`), limiting event delivery to subscribed clients.
- **Bid entity mutation methods are internal**: `MarkAsOutbid`, `MarkAsWinning`, `MarkAsWon`, `Cancel` on `Bid` are all `internal` (`Bid.cs:29-32`), preventing external code from manipulating bid status outside the aggregate.
- **BidConfiguration uses dual index** on `(AuctionId, BidderId)` to support efficient per-bidder queries on an auction (`BidConfiguration.cs:40-43`).
- **Cascade delete behavior** on bids is explicit (`AuctionConfiguration.cs:83`) — a deliberate design choice whose audit compliance implications are noted in M-X (the team has opted for cascade).

---

## Production Readiness Checklist

- [ ] **C-1**: Remove `BidderId` from `PlaceBidCommand`; resolve bidder identity from the authenticated JWT claim server-side.
- [ ] **C-2**: Add ownership assertion in `CloseAuctionCommandHandler` — verify caller is `auction.SellerId` before closing.
- [ ] **C-3**: Register `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` in `Program.cs`; add `UseAuthentication()` before `UseAuthorization()`; decorate all controllers and hubs with `[Authorize]`.
- [ ] **H-1**: Implement `IIdempotentCommand` on `PlaceBidCommand` and wire idempotency key from the `Idempotency-Key` HTTP header.
- [ ] **H-2**: Remove `SellerId` from `CreateAuctionCommand`; resolve from authenticated principal server-side.
- [ ] **H-3**: Add `[Authorize]` to `AuctionHub` and `NotificationHub`; configure JWT query-string extraction for WebSocket connections.
- [ ] **H-4**: Implement `GlobalExceptionFilter` to catch known exception types and return structured `ProblemDetails`; register it in `AddControllers(options => ...)`.
- [ ] **M-1**: Add explicit currency-mismatch guard at the top of `Auction.PlaceBid` returning a `Result` failure instead of throwing.
- [ ] **M-2**: Add state guard to `Auction.Close()` to reject `Pending` and `Cancelled` auctions with a domain error.
- [ ] **M-3**: Apply `[RateLimit]` attribute to the `PlaceBid` and `CreateAuction` controller actions.
- [ ] **M-4**: Audit Redis `AllowAdmin` usage; scope or remove if possible.
- [ ] **M-5**: Remove credentials from `appsettings.json` and `AuctionDbContextFactory.cs`; use `dotnet user-secrets` for local dev and environment variables / secrets manager for production.
- [ ] **M-6**: Bind Docker port mappings to `127.0.0.1` for PostgreSQL and Redis in `docker-compose.infra.yml`.
- [ ] **L-1**: Extract anti-sniping constants to `IOptions<AuctionDomainOptions>`.
- [ ] **L-2**: Re-enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`.
- [ ] **L-3**: Implement `IdempotencyFilter` to extract the `Idempotency-Key` header and pass it to commands.
- [ ] **L-4**: Set `AllowedHosts` to the production hostname in production configuration.
- [ ] **L-5**: Add `UseHsts()` for production and minimal security response headers middleware.
