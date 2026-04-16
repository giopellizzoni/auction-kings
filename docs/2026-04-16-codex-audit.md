# Codex Audit Notes

Date: 2026-04-16
Branch: `codex/audit`

## Scope

Read-only audit of the repository focused on:

- unused code or files
- vulnerabilities and security-sensitive behavior
- inconsistencies across API/auth/configuration

## Findings

### 1. Bid impersonation risk

Severity: High

Files:

- `src/services/AuctionKings.API/Controllers/BidsController.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs`

Issue:

`BidsController.PlaceBid` accepts `BidderId` from the request body and forwards it through the command into the domain flow. The authenticated principal is not used to determine the bidder identity.

Impact:

Any user with the `Bidder` role can place a bid on behalf of another user if they know or can guess that user's GUID.

Suggested fix:

- Remove `BidderId` from the request contract.
- Resolve bidder identity from the authenticated user claims.
- Add tests that verify impersonation is rejected.

### 2. Auction creation allows seller impersonation

Severity: High

Files:

- `src/services/AuctionKings.API/Controllers/AuctionsController.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs`

Issue:

`AuctionsController.CreateAuction` has no `[Authorize]` attribute and accepts `SellerId` from the request body.

Impact:

- Anonymous users can create auctions unless this is intentionally public.
- Callers can create auctions on behalf of another seller.

Suggested fix:

- Decide whether auction creation is public or authenticated.
- If authenticated, require authorization and derive `SellerId` from claims instead of the request body.

### 3. Dev token endpoint is available outside production

Severity: High

File:

- `src/services/AuctionKings.API/Controllers/DevController.cs`

Issue:

The dev token endpoint returns tokens in every non-production environment. It only blocks when `IHostEnvironment.IsProduction()` is true.

Impact:

If a staging or preview deployment exposes this route, anyone who can reach it may mint arbitrary JWTs with arbitrary roles.

Suggested fix:

- Restrict the endpoint to local development only.
- Prefer disabling it entirely outside explicit developer mode.
- Consider compile-time exclusion or a feature flag instead of environment-name checks alone.

### 4. SignalR hubs are unauthenticated while JWT auth is configured

Severity: Medium

Files:

- `src/services/AuctionKings.API/Program.cs`
- `src/services/AuctionKings.API/Hubs/AuctionHub.cs`
- `src/services/AuctionKings.API/Hubs/NotificationHub.cs`

Issue:

JWT authentication and authorization are configured for the API, but both SignalR hubs are still marked with TODO comments and are not protected with `[Authorize]`.

Impact:

- Anonymous clients can connect to hubs.
- `AuctionHub` exposes state synchronization methods to unauthenticated sockets.
- The hub auth posture is inconsistent with the controller auth posture.

Suggested fix:

- Decide whether auction hub access is public or authenticated.
- Protect hubs explicitly according to that decision.
- Add tests for anonymous and authenticated hub access.

### 5. Idempotency check is race-prone

Severity: Medium

Files:

- `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs`
- `src/services/AuctionKings.Infrastructure/Services/IdempotencyService.cs`
- `src/services/AuctionKings.Infrastructure/Persistence/Configurations/IdempotencyKeyConfiguration.cs`

Issue:

The pipeline checks `ExistsAsync`, then executes the command handler, then stores the idempotency key afterward. Concurrent requests with the same key can both pass the existence check before either one stores the key.

Impact:

Duplicate business operations may still execute under race conditions.

Suggested fix:

- Reserve the key atomically before executing the handler.
- Handle unique-key conflicts as duplicate requests.
- Add a concurrent integration test rather than only a sequential duplicate test.

## Unused or noisy items

### 6. `RateLimitAttribute` appears unused

Severity: Low

File:

- `src/services/AuctionKings.API/Filters/RateLimitAttribute.cs`

Issue:

The attribute exists, but the audit did not find it applied to any controller or action.

Notes:

- If rate limiting is intended, wire it in and test it.
- If not intended, remove it to reduce dead surface area.

### 7. `UsersControllers.cs` is a placeholder file

Severity: Low

File:

- `src/services/AuctionKings.API/Controllers/UsersControllers.cs`

Issue:

The file only contains a comment saying the class moved to `UsersController.cs`.

Notes:

- It is harmless.
- It also looks like a duplicate or typo during audits and searches.

## Inconsistencies worth revisiting

### 8. Identity extraction does not line up cleanly across components

Files:

- `src/services/AuctionKings.API/Auth/JwtTokenGenerator.cs`
- `src/services/AuctionKings.API/Hubs/HubExtensions.cs`
- `src/services/AuctionKings.API/Filters/RateLimitAttribute.cs`

Issue:

- JWT generation emits `sub` and `role`.
- Hub code reads `ClaimTypes.NameIdentifier`.
- Rate limiting reads `User.Identity?.Name`.

Impact:

Different components rely on different identity fields. That makes the auth story harder to reason about and easier to break accidentally.

Suggested fix:

- Standardize which claim carries the application user ID.
- Use the same identity source in controllers, hubs, filters, and tests.

## Dependency warnings seen during verification

Observed during `dotnet build` / package audit attempts:

- `NuGet.Packaging` `6.12.1` reported `NU1901` low severity vulnerability
- `NuGet.Protocol` `6.12.1` reported `NU1901` low severity vulnerability
- Advisory: `GHSA-g4vj-cjjj-v7hg`

## Notes

- No code changes were made as part of this audit summary.
- The repository had an untracked `.claude/worktrees/` directory at the time of the audit.
