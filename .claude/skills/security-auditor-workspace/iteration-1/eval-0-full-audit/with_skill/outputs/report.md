# Security Audit Report — AuctionKings
Date: 2026-03-29

## Executive Summary

AuctionKings has a well-structured Clean Architecture foundation with good domain-level safeguards (self-bid protection, anti-sniping, optimistic concurrency with dual tokens, idempotency pipeline). However, the project is **not production-ready for AWS deployment**. The most critical gap is a complete absence of authentication: `AddAuthentication()` is never called, `UseAuthentication()` is absent from the pipeline, and all three controllers and both SignalR hubs have no `[Authorize]` attributes — meaning every endpoint is publicly accessible without credentials. Secondary critical issues include hardcoded database credentials committed to source control, `AllowAdmin = true` on the Redis connection, and the rate limiter and exception filter both being empty stubs that provide no protection. The project must address the Critical and High findings before any public-facing deployment.

---

## Findings

### CRITICAL

#### C-1: No Authentication Configured — All Endpoints and Hubs Are Publicly Accessible

- **Location**: `src/services/AuctionKings.API/Program.cs:1-43`
- **Description**: `AddAuthentication()` is never called in `Program.cs`. `UseAuthentication()` is also absent from the middleware pipeline. Only `UseAuthorization()` is present (line 35), but with no registered authentication scheme, it has no effect. Any call to an endpoint with `[Authorize]` would silently return 401 — but no controllers carry `[Authorize]` either (see C-2). The net result is that every API surface is entirely open to anonymous callers.
- **Impact**: Any anonymous HTTP client can call every auction command and query without credentials. An attacker can create auctions, place bids as any user ID they choose, and close auctions they do not own — with no authentication barrier whatsoever.
- **Remediation**: Add JWT Bearer authentication before `AddControllers()`. At minimum: `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` and insert `app.UseAuthentication()` immediately before `app.UseAuthorization()` in the pipeline. Requires `Microsoft.AspNetCore.Authentication.JwtBearer` (already on the .NET 10 runtime meta-package). Configure JWT issuer, audience, and signing key from AWS Secrets Manager, not `appsettings.json`.

---

#### C-2: All Controllers Are Unauthenticated Stubs With No Authorization Attributes

- **Location**: `src/services/AuctionKings.API/Controllers/AuctionsController.cs:7`, `src/services/AuctionKings.API/Controllers/BidsController.cs:7`, `src/services/AuctionKings.API/Controllers/UsersControllers.cs:7`
- **Description**: All three controllers are empty stubs that carry only `[ApiController]` and `[Route]` — no `[Authorize]` attribute at class level or on any action. Because they are currently empty this is not yet exploitable, but when actions are implemented they will be open by default. There is no global authorization policy fallback registered in `Program.cs` either (`options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()` is absent).
- **Impact**: When the stubs are filled in, every action — including bid placement, auction creation, and auction closure — will be publicly accessible without a token. A developer filling in the stub today would not receive any compiler or runtime warning that protection is missing.
- **Remediation**: Add `[Authorize]` to all three controller classes now, before implementation begins. Additionally, register a fallback authorization policy in `Program.cs` via `builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. This ensures any future endpoint not explicitly annotated defaults to requiring authentication.

---

#### C-3: Horizontal Privilege Escalation — BidderId and SellerId Accepted Directly From Request Body Without Identity Verification

- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs:6-10`, `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs:6-12`, `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs:46`
- **Description**: Both `PlaceBidCommand` and `CreateAuctionCommand` accept `BidderId` and `SellerId` as plain record fields sourced from the request body. Neither handler verifies that the supplied `BidderId` / `SellerId` matches the authenticated principal (e.g., the JWT `sub` claim). An authenticated user can trivially supply any other user's GUID and act as that user.
- **Impact**: An attacker with a valid account (User A) can submit `PlaceBidCommand` with `BidderId = <User B's GUID>` and place bids attributed to User B, potentially manipulating auction outcomes, ruining another user's bidding history, or bypassing the self-bid protection check by supplying the seller's ID as their own.
- **Remediation**: Remove `SellerId` / `BidderId` from the command record. In the controller action, extract the user ID from `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` and pass it explicitly to the command. The handler should never trust a user-controlled identity field.

---

#### C-4: Credentials Committed to Source Control in Two Files

- **Location**: `src/services/AuctionKings.Infrastructure/Persistence/AuctionDbContextFactory.cs:15`, `src/services/AuctionKings.API/appsettings.json:10`
- **Description**: `AuctionDbContextFactory.cs` contains a hardcoded connection string `Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=dev` at line 15. `appsettings.json` contains `"DefaultConnection": "Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password="` at line 10, which exposes the username, host, database name, and port. Both files are committed to the git repository. Additionally, the Docker secret files `docker/secrets/postgres_password.txt` and `docker/secrets/redis_password.txt` (containing `Tr1c0lor0!@#` and `Tr1c0lor0#@!!@#`) exist in the repository. If the repository is or becomes public, or if CI/CD logs print environment state, these credentials are immediately usable.
- **Impact**: Full database compromise. An attacker with repository read access obtains valid credentials to connect directly to the PostgreSQL instance (if any network path exists). The Redis password is similarly exposed.
- **Remediation**: (1) Add `docker/secrets/` to `.gitignore` immediately and rotate the exposed passwords. (2) Replace the hardcoded string in `AuctionDbContextFactory.cs` with a placeholder or environment-variable lookup. (3) For production, provision credentials via AWS Secrets Manager and inject them via environment variables or `IConfiguration` backed by the AWS provider — never in `appsettings.json`.

---

### HIGH

#### H-1: Both SignalR Hubs Have No `[Authorize]` Attribute — Anonymous Connection Allowed

- **Location**: `src/services/AuctionKings.API/Hubs/AuctionHub.cs:7`, `src/services/AuctionKings.API/Hubs/NotificationHub.cs:7`
- **Description**: Neither `AuctionHub` nor `NotificationHub` carries an `[Authorize]` attribute. The hubs are mapped in `Program.cs` at lines 38-39 with no `.RequireAuthorization()` call either. Any anonymous WebSocket client can connect to `/hubs/auction` and `/hubs/notifications`. `AuctionHub.OnConnectedAsync` silently no-ops for unauthenticated connections (the `GetUserId()` check at line 17-21 returns null and skips tracking), which means tracking state diverges from actual connection state. Similarly, `NotificationHub` assigns group membership based on user ID (line 14) — an unauthenticated client skips group assignment but remains connected.
- **Impact**: Anonymous clients can connect, call `JoinAuctionAsync` for any `auctionId`, and receive all real-time bid events for that auction including bid amounts and bidder identifiers. In addition, when authentication is added, if JWT configuration for SignalR is omitted (see H-2), authenticated connections will fail silently.
- **Remediation**: Add `[Authorize]` to both hub classes. Also add `.RequireAuthorization()` to `MapHub<AuctionHub>` and `MapHub<NotificationHub>` in `Program.cs` as defense in depth. Reject the connection in `OnConnectedAsync` if `GetUserId()` returns null (`Context.Abort()`).

---

#### H-2: JWT Token Transport for SignalR Not Configured

- **Location**: `src/services/AuctionKings.API/Program.cs:1-43`
- **Description**: SignalR WebSocket connections cannot carry HTTP Authorization headers. The standard pattern requires configuring the JWT Bearer `OnMessageReceived` event to read the token from the `access_token` query parameter. This configuration is entirely absent from `Program.cs`. Once `[Authorize]` is applied to the hubs (H-1), all SignalR connections will fail with 401 even for valid authenticated clients.
- **Impact**: When authorization is added to the hubs, real-time functionality will be completely broken for all clients. This is likely to lead to a rushed fix that disables authorization to restore functionality.
- **Remediation**: Configure the JWT Bearer options to extract the token from the query string for hub endpoints:
  ```csharp
  .AddJwtBearer(options => {
      options.Events = new JwtBearerEvents {
          OnMessageReceived = ctx => {
              var token = ctx.Request.Query["access_token"];
              if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                  ctx.Token = token;
              return Task.CompletedTask;
          }
      };
  });
  ```

---

#### H-3: `CloseAuctionCommand` Has No Ownership Verification — Any Authenticated User Can Close Any Auction

- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandHandler.cs:30-59`, `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommand.cs:6`
- **Description**: `CloseAuctionCommand` accepts only `AuctionId`. The handler fetches the auction and calls `auction.Close()` with no check that the requesting user is the auction's `SellerId`. The `Auction.Close()` method itself contains no ownership guard — it only checks status (not `Closed`, not `Cancelled`).
- **Impact**: Once authentication is in place, any authenticated user (including a bidder) can call the close endpoint with any `AuctionId` and prematurely terminate an auction, determining the winner artificially. A malicious user could close an auction at a moment when their own bid is the highest.
- **Remediation**: Add the requesting user's identity to the command (extracted from the JWT claim in the controller, not the request body). In the handler, after fetching the auction, assert `auction.SellerId == requestingUserId` and return a `Forbidden` result if the check fails.

---

#### H-4: `GlobalExceptionFilter` Is an Empty Stub — Unhandled Exceptions Will Leak Stack Traces

- **Location**: `src/services/AuctionKings.API/Filters/GlobalExceptionFilter.cs:3-6`
- **Description**: `GlobalExceptionFilter` is declared as a plain class with no base class, no interface (`IExceptionFilter`, `IAsyncExceptionFilter`), and no logic. It is not registered in `Program.cs` at all. In development, ASP.NET Core's developer exception page will surface full stack traces. In production, without a configured exception handler, unhandled exceptions will propagate as 500 responses, and depending on server configuration may include stack traces in the response body or expose internal types and file paths.
- **Impact**: Stack traces reveal assembly names, file system paths, internal type names, and potentially database schema details. This information materially assists an attacker in crafting targeted exploits.
- **Remediation**: Implement `GlobalExceptionFilter : IAsyncExceptionFilter`, handle known exception types (`ValidationException`, `DuplicateCommandException`, `ConcurrencyException`) with appropriate HTTP status codes, and return a `ProblemDetails` response for all unhandled exceptions — never returning the raw exception message or stack trace. Register it via `builder.Services.AddControllers(opts => opts.Filters.Add<GlobalExceptionFilter>())`.

---

#### H-5: `IdempotencyFilter` Is an Empty Stub — No HTTP-Level Idempotency Protection

- **Location**: `src/services/AuctionKings.API/Filters/IdempotencyFilter.cs:3`
- **Description**: `IdempotencyFilter` is an empty class. The MediatR `IdempotencyBehavior` in the pipeline does provide command-level idempotency, but only for commands that implement `IIdempotentCommand`. Checking the command definitions: `PlaceBidCommand`, `CreateAuctionCommand`, and `CloseAuctionCommand` do not implement `IIdempotentCommand`, so the behavior silently passes through for all of them (line 23 of `IdempotencyBehavior.cs`). The HTTP filter stub that was intended to extract the idempotency key from the `Idempotency-Key` header is empty.
- **Impact**: A network retry or double-submit can result in duplicate bids being placed, duplicate auctions being created, or an auction being closed twice. In an auction system, duplicate bids can manipulate price discovery.
- **Remediation**: (1) Implement `IdempotencyFilter` to extract the `Idempotency-Key` header and check/store it via `IIdempotencyService`. (2) Make `PlaceBidCommand` and `CreateAuctionCommand` implement `IIdempotentCommand` so the pipeline behavior activates for them.

---

#### H-6: Redis `AllowAdmin = true` Enables Destructive Commands

- **Location**: `src/services/AuctionKings.Infrastructure/DependencyInjection.cs:53`
- **Description**: The `ConnectionMultiplexer` is created with `redisConfig.AllowAdmin = true`. This setting enables StackExchange.Redis to issue administrative commands including `FLUSHDB`, `FLUSHALL`, `CONFIG SET`, `DEBUG SLEEP`, `DEBUG SEGFAULT`, and `SLAVEOF`. The `AllowAdmin` flag is required by `GetServer().KeysAsync()` in `RedisCacheService.RemoveByPrefixAsync`, but the scope of the flag is the entire multiplexer — not just the `SCAN` operation.
- **Impact**: If any code path is exploited (e.g., a future injection vulnerability, a misconfigured Redis ACL, or a compromised service identity), an attacker can flush the entire Redis cache, disrupting the rate limiter, the idempotency store, the SignalR backplane, and all auction caches simultaneously. This is a potential denial-of-service vector.
- **Remediation**: The architectural pattern for prefix-based key removal without `AllowAdmin` is to maintain a Redis Set of keys per prefix and delete them explicitly, or use a dedicated Redis instance for `SCAN`-dependent operations. In production on AWS ElastiCache, `AllowAdmin` is not supported at all — this code will fail at runtime. Refactor `RemoveByPrefixAsync` to use a key registry pattern and remove `AllowAdmin = true`.

---

### MEDIUM

#### M-1: `AllowedHosts: "*"` Permits Host Header Injection

- **Location**: `src/services/AuctionKings.API/appsettings.json:8`
- **Description**: `"AllowedHosts": "*"` is the default development value and should be replaced with the production hostname before deployment. A wildcard allows any `Host` header value, which can be used in password reset poisoning, cache poisoning (if a CDN key includes the Host header), and SSRF if any code uses `Request.Host` to construct URLs.
- **Impact**: Moderate — exploitability depends on whether the application uses `Request.Host` for redirect URIs or email links. With AWS ALB in front, the actual host is typically validated at the load balancer level, reducing but not eliminating the risk.
- **Remediation**: Set `"AllowedHosts": "api.auctionkings.com"` (the actual production hostname) in the environment-specific configuration.

---

#### M-2: No CORS Policy Configured — Cross-Origin Behavior Is Undefined

- **Location**: `src/services/AuctionKings.API/Program.cs:1-43`
- **Description**: Neither `AddCors()` nor `UseCors()` appear anywhere in `Program.cs`. This means the browser's same-origin policy is the only protection. No explicit CORS headers are sent, so cross-origin requests from the front-end application will be blocked by browsers — but programmatic clients (e.g., `curl`, mobile apps) are unaffected. When a front-end client is added, the temptation will be to quickly add `AllowAnyOrigin()`, which if combined with credential headers becomes a misconfiguration.
- **Impact**: Functional gap now; security gap risk when CORS is eventually added without careful review.
- **Remediation**: Add an explicit, restrictive CORS policy targeting the known front-end origin(s): `builder.Services.AddCors(o => o.AddPolicy("FrontEnd", p => p.WithOrigins("https://app.auctionkings.com").AllowAnyHeader().AllowAnyMethod().AllowCredentials()))`. Apply it to controllers and hubs explicitly.

---

#### M-3: No HTTPS Strict Transport Security (HSTS) Configured

- **Location**: `src/services/AuctionKings.API/Program.cs:33`
- **Description**: `UseHttpsRedirection()` is present (line 33), but `UseHsts()` is not. HSTS instructs browsers to use HTTPS for all future requests for a configurable period, preventing SSL stripping attacks. Without it, the first HTTP request for a returning user can be downgraded.
- **Impact**: SSL stripping attacks remain possible on the first connection. In an auction context where bid amounts and user sessions are transmitted, this could lead to session hijacking.
- **Remediation**: Add `UseHsts()` gated on the non-development environment, with a suitable `MaxAge` (minimum 1 year for production): `if (!app.Environment.IsDevelopment()) { app.UseHsts(); }`. Configure `HstsOptions.MaxAge` and consider `IncludeSubdomains = true`.

---

#### M-4: Missing Security Response Headers

- **Location**: `src/services/AuctionKings.API/Program.cs:1-43`
- **Description**: No middleware adds `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, or `Referrer-Policy` headers to HTTP responses. These headers are required by common compliance frameworks (PCI DSS, SOC2) and OWASP.
- **Impact**: Without `X-Content-Type-Options: nosniff`, browsers may MIME-sniff response bodies, enabling content injection. Without `X-Frame-Options: DENY`, the API swagger UI (if exposed) can be framed. These are compliance failures.
- **Remediation**: Add a middleware that sets these headers on all responses:
  ```csharp
  app.Use(async (ctx, next) => {
      ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
      ctx.Response.Headers.Append("X-Frame-Options", "DENY");
      ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
      await next();
  });
  ```

---

#### M-5: Rate Limiter Fails Open — Redis Unavailability Disables Protection

- **Location**: `src/services/AuctionKings.Infrastructure/RateLimiting/RedisRateLimiter.cs:72-78`
- **Description**: The `catch` block in `IsAllowedAsync` logs a warning and returns `true` (allow), meaning any Redis connectivity failure — including one deliberately induced by an attacker (e.g., connection exhaustion, or exploiting H-6 above to `FLUSHALL`) — disables rate limiting entirely for all callers. The comment even documents this: `"Failing open."`.
- **Impact**: If Redis is unavailable (or deliberately disrupted), the bid placement and other endpoints become unrate-limited, enabling burst attacks. Additionally, since the rate limiter is defined but no endpoints currently apply `[RateLimitAttribute]`, the rate limiter provides zero protection at present regardless of Redis state.
- **Remediation**: Consider a fallback strategy: use an in-memory `SlidingWindowRateLimiter` from `System.Threading.RateLimiting` as a secondary check when Redis is unavailable. Separately, apply `[RateLimitAttribute]` to at minimum the `PlaceBid` endpoint.

---

#### M-6: No PostgreSQL SSL Mode Configured

- **Location**: `src/services/AuctionKings.API/appsettings.json:10`, `src/services/AuctionKings.Infrastructure/DependencyInjection.cs:29-33`
- **Description**: The connection string `"Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password="` does not include `SslMode=Require` or `TrustServerCertificate=false`. On AWS RDS for PostgreSQL, SSL can be required at the instance level, but the client connection string must also request SSL — otherwise Npgsql will negotiate an unencrypted connection if the server permits it.
- **Impact**: Database traffic (including bid amounts, auction states, user IDs) travels unencrypted between the application container and RDS if the server's `rds.force_ssl` parameter is not set to `1`.
- **Remediation**: Add `Ssl Mode=Require;Trust Server Certificate=false` to the production connection string. On AWS RDS, also set the `rds.force_ssl` DB parameter group setting to `1`.

---

#### M-7: Cascade Delete Destroys Bid History When an Auction Is Deleted

- **Location**: `src/services/AuctionKings.Infrastructure/Persistence/Configurations/AuctionConfiguration.cs:81-84`
- **Description**: The `Auction → Bid` relationship is configured with `OnDelete(DeleteBehavior.Cascade)`. Deleting an auction (even accidentally or by a future admin endpoint) permanently destroys all associated bids. In a real-money auction system, bid records are part of the financial audit trail.
- **Impact**: Audit trail loss; potential compliance failure under financial regulations requiring transaction records to be retained for a defined period (e.g., 7 years under most jurisdictions).
- **Remediation**: Change to `OnDelete(DeleteBehavior.Restrict)` or implement soft-delete. Add a `deleted_at` column to auctions rather than physical deletes.

---

#### M-8: Swagger / OpenAPI Exposed Without Authentication Guard

- **Location**: `src/services/AuctionKings.API/Program.cs:28-31`
- **Description**: `MapOpenApi()` is guarded only by `app.Environment.IsDevelopment()`. This is correct for local development. However, if the `ASPNETCORE_ENVIRONMENT` variable is left as `Development` in the AWS ECS task definition (a common mistake), Swagger will be publicly exposed in production, revealing the full API surface, endpoint paths, request/response schemas, and error codes.
- **Impact**: Information disclosure of the complete API contract to an attacker, significantly reducing reconnaissance effort.
- **Remediation**: Add an explicit check in the deployment pipeline that `ASPNETCORE_ENVIRONMENT` is set to `Production` in ECS task definitions. Consider also requiring an API key or IP allowlist for any internal documentation endpoint.

---

#### M-9: `PlaceBid` Does Not Validate Currency Match Against Auction Currency

- **Location**: `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs:45-46`, `src/services/AuctionKings.Domain/Aggregates/Auction.cs:97-100`
- **Description**: `PlaceBidCommand` accepts a `Currency` field from the caller. The handler constructs `new Money(request.Amount, request.Currency)` and passes it to `auction.PlaceBid()`. `BidIncrement.GetMinimumBid` returns a `Money` in the auction's `CurrentPrice.Currency`. The `<` operator on `Money` calls `EnsureSameCurrency`, which throws `InvalidOperationException` if currencies differ. This exception is not a domain `Result.Failure` — it is an unhandled exception that propagates to the controller, and with no exception filter (H-4) will result in a 500.
- **Impact**: A caller submitting a bid in a mismatched currency causes an unhandled 500. With the exception filter as a stub, this leaks the internal exception message. Additionally, if currency validation is ever relaxed, bids could be compared across currencies.
- **Remediation**: In `PlaceBidCommandHandler`, validate that `request.Currency` matches `auction.CurrentPrice.Currency` before constructing the `Money` object, and return `Result.Failure` with a domain error. Add a `DomainErrors.Auction.CurrencyMismatch` error.

---

### LOW

#### L-1: `TreatWarningsAsErrors` Is Commented Out

- **Location**: `Directory.Build.props:8`
- **Description**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is commented out. With `Nullable` enabled and analyzers active, nullable dereference warnings, unused variable warnings, and deprecated API warnings silently pass the build without failing. This means security-relevant nullable bugs (e.g., a null reference that skips an authorization check) are not caught at build time.
- **Impact**: Security-relevant warnings accumulate silently.
- **Remediation**: Uncomment `TreatWarningsAsErrors` and fix all warnings. For legacy suppressions use `#pragma warning disable` with a justification comment. This also satisfies the `EnforceCodeStyleInBuild` analyzer requirement.

---

#### L-2: No Dockerfile — Container Security Posture Cannot Be Assessed, and Build Is Non-Reproducible

- **Location**: `docker/` (absent)
- **Description**: No Dockerfile exists in the repository. The `docker-compose.yml` file is empty (1 line). For an AWS deployment targeting ECS Fargate, a Dockerfile is required. Without it, the image will be built ad hoc, likely without: a non-root user, a multi-stage build separating the SDK layer from the runtime layer, or a minimal base image (`mcr.microsoft.com/dotnet/aspnet` vs `sdk`).
- **Impact**: When a Dockerfile is eventually created under time pressure for deployment, it is likely to: run as root, include the full .NET SDK (which adds ~700 MB of build tooling and compiler attack surface), and potentially bake environment variables with credentials into image layers.
- **Remediation**: Create a multi-stage Dockerfile now: stage 1 uses `mcr.microsoft.com/dotnet/sdk:10.0` to build and publish; stage 2 uses `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` as the runtime image with a non-root `appuser` and `USER appuser` before `ENTRYPOINT`.

---

#### L-3: Port Binding in `docker-compose.infra.yml` Uses Variable-Controlled Ports Without Default Constraints

- **Location**: `docker/docker-compose.infra.yml:23`, `docker/docker-compose.infra.yml:49`
- **Description**: PostgreSQL and Redis ports are mapped as `"${POSTGRES_HOST_PORT}:${POSTGRES_CONTAINER_PORT}"` and `"${REDIS_HOST_PORT}:${REDIS_CONTAINER_PORT}"`. If the `.env` file sets host ports to `5432` and `6379` without binding to `127.0.0.1` (e.g., `"127.0.0.1:${POSTGRES_HOST_PORT}:${POSTGRES_CONTAINER_PORT}"`), Docker will bind to `0.0.0.0`, exposing both services on all network interfaces of the host machine.
- **Impact**: On a developer machine connected to a shared network, or on a bastion host, the database and cache are reachable from other hosts on the network, and in conjunction with the committed credentials (C-4), this is directly exploitable.
- **Remediation**: Prefix the port binding with `127.0.0.1:` to scope it to the loopback interface: `"127.0.0.1:${POSTGRES_HOST_PORT}:${POSTGRES_CONTAINER_PORT}"`. For production ECS, no host port mapping is needed at all — service discovery handles routing.

---

#### L-4: Anti-Sniping Extension Is Hardcoded and Unbounded

- **Location**: `src/services/AuctionKings.Domain/Aggregates/Auction.cs:11-12`
- **Description**: `AntiSnipingThreshold` (5 minutes) and `AntiSnipingExtension` (5 minutes) are compile-time constants. Because a bid placed within 5 minutes of close always extends by 5 minutes, the auction end time can be extended indefinitely by placing bids in the last-5-minute window repeatedly. There is no cap on the total number of extensions or the maximum total duration.
- **Impact**: A bad actor (or colluding bidders) can extend a high-value auction indefinitely, preventing it from closing and freezing the seller's asset in limbo.
- **Remediation**: Track the number of anti-snipe extensions and cap at a maximum (e.g., 3 extensions or `MaxAuctionDuration`). Make the threshold and extension configurable via `IOptions<AuctionSettings>` rather than static constants. Add a `MaxEndTime` guard in `PlaceBid`.

---

#### L-5: `IIdempotentCommand` Not Implemented by Any Mutation Command — Idempotency Pipeline Is Dead Code

- **Location**: `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs:23`, `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs`, `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs`
- **Description**: `IdempotencyBehavior.Handle` checks `if (request is not IIdempotentCommand)` and immediately calls `next()` when the check fails. None of the three command records (`PlaceBidCommand`, `CreateAuctionCommand`, `CloseAuctionCommand`) implement `IIdempotentCommand`. The idempotency infrastructure (service, database table, behavior) is fully built but never activated.
- **Impact**: Currently the pipeline provides zero idempotency protection despite having the full implementation. A network retry will create duplicate bids.
- **Remediation**: Have `PlaceBidCommand` and `CreateAuctionCommand` implement `IIdempotentCommand` with an `IdempotencyKey` property. In the controller, require callers to pass an `Idempotency-Key` header and thread it into the command.

---

#### L-6: No Secrets Manager Integration — Production Config Will Default to `appsettings.json`

- **Location**: `src/services/AuctionKings.Infrastructure/DependencyInjection.cs:23-27`, `src/services/AuctionKings.API/appsettings.json`
- **Description**: There is no AWS Secrets Manager, Parameter Store, or environment variable override configuration in `Program.cs` or the infrastructure layer. The only configuration source is `appsettings.json` and the default .NET config providers. In ECS, secrets must be injected via task definition secret references or environment variables — but without an explicit configuration provider wired to those sources, the application will fall back to the values in the committed `appsettings.json`.
- **Impact**: Credentials will be sourced from source control rather than the secrets vault in a default ECS deployment.
- **Remediation**: Add `builder.Configuration.AddEnvironmentVariables()` (already included by default in `WebApplication.CreateBuilder`, but verify) and ensure the ECS task definition injects `ConnectionStrings__DefaultConnection` and `ConnectionStrings__Redis` as secret environment variables from Secrets Manager. Add `AWSSDK.SecretsManager` and the AWS Systems Manager configuration provider if richer secret resolution is needed.

---

## Positive Findings

- Self-bid protection is correctly implemented at the domain level (`Auction.PlaceBid`: check `bidderId == SellerId` returns `DomainErrors.Auction.CannotBidOnOwnAuction`).
- Dual optimistic concurrency tokens are in place: application-level `Version` property configured as a concurrency token, plus the PostgreSQL `xmin` shadow property — both will throw `ConcurrencyException` on conflicting updates.
- The bid concurrency retry loop in `PlaceBidCommandHandler` is correctly implemented with 3 attempts and `ClearTracking()` between retries.
- Anti-sniping logic is implemented in the domain aggregate and correctly fires a domain event (`AuctionExtendedEvent`).
- Docker secrets pattern for PostgreSQL and Redis passwords is correctly implemented in `docker-compose.infra.yml` (using `POSTGRES_PASSWORD_FILE` and reading from `/run/secrets/`) — credentials are not inline in the compose file.
- Redis authentication (`--requirepass`) is configured in the compose command.
- The FluentValidation pipeline behavior is correctly ordered (Logging → Validation → Idempotency → Performance) and wired for all commands.
- No raw SQL (`FromSqlRaw`, `ExecuteSqlRaw`) was found in any repository — all queries use EF Core LINQ APIs, eliminating SQL injection risk in the current codebase.
- `BidIncrement` minimum bid enforcement is applied at the domain level inside `Auction.PlaceBid` (not only in the validator), which is the correct placement.
- Audit trail for outbox: `OutboxMessage` table and `OutboxRepository` exist, meaning domain events are durably stored before processing — supporting eventual-consistency patterns for financial records.
- `Auction.Close()` checks for `Cancelled` status and returns a domain error rather than silently succeeding.

---

## Production Readiness Checklist

- [ ] Implement `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` and add `app.UseAuthentication()` before `app.UseAuthorization()` in `Program.cs`
- [ ] Add `[Authorize]` to `AuctionsController`, `BidsController`, and `UsersControllers`; register a global fallback authorization policy
- [ ] Remove `SellerId` / `BidderId` from command records; extract caller identity from `HttpContext.User` in controllers
- [ ] Rotate the credentials in `docker/secrets/postgres_password.txt` and `docker/secrets/redis_password.txt`; add `docker/secrets/` to `.gitignore`
- [ ] Remove the hardcoded connection string from `AuctionDbContextFactory.cs`
- [ ] Add `[Authorize]` to `AuctionHub` and `NotificationHub`; add `.RequireAuthorization()` to `MapHub` calls
- [ ] Configure JWT Bearer `OnMessageReceived` to read `access_token` query parameter for SignalR connections
- [ ] Add requester ownership check in `CloseAuctionCommandHandler` (`requester == auction.SellerId`)
- [ ] Implement `GlobalExceptionFilter` as `IAsyncExceptionFilter`, return `ProblemDetails`, and register it in `Program.cs`
- [ ] Implement `IdempotencyFilter` and make `PlaceBidCommand` / `CreateAuctionCommand` implement `IIdempotentCommand`
- [ ] Remove `AllowAdmin = true` from the Redis `ConnectionMultiplexer`; refactor `RemoveByPrefixAsync` to use a key registry
- [ ] Set `"AllowedHosts"` to the production DNS hostname in the production configuration
- [ ] Add explicit, origin-restricted CORS policy for the front-end domain
- [ ] Add `UseHsts()` gated on non-development environment
- [ ] Add security response headers middleware (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`)
- [ ] Add `SslMode=Require` to the production PostgreSQL connection string; enable `rds.force_ssl` on the RDS instance
- [ ] Change `OnDelete(DeleteBehavior.Cascade)` on the Bid relationship to `Restrict` or implement soft-delete
- [ ] Confirm `ASPNETCORE_ENVIRONMENT=Production` is set in all ECS task definitions (prevents Swagger exposure)
- [ ] Add currency mismatch check in `PlaceBidCommandHandler` before constructing `Money`
- [ ] Uncomment `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props` and resolve all warnings
- [ ] Create a production Dockerfile (multi-stage, non-root user, `aspnet` runtime image, not `sdk`)
- [ ] Scope Docker host port bindings to `127.0.0.1` in `docker-compose.infra.yml`
- [ ] Add an extension cap and configurable limits for the anti-sniping logic
- [ ] Wire AWS Secrets Manager / Parameter Store as a configuration provider for ECS deployments
