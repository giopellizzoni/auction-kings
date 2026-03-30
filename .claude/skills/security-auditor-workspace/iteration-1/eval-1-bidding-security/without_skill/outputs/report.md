# Bidding Flow Security Analysis — AuctionKings

**Scope:** Domain layer (`AuctionKings.Domain`) and Application layer (`AuctionKings.Application`), focusing on the auction bidding flow.

**Date:** 2026-03-29

---

## Summary

The domain aggregate (`Auction.cs`) enforces several bidding invariants correctly. However, the Application layer has a critical authorization gap: `PlaceBidCommand` accepts a caller-supplied `BidderId` without verifying it against the authenticated user identity. Any authenticated user can impersonate any other user when placing a bid. Additional findings relate to missing temporal expiry checks, no authorization guard on `CloseAuctionCommand`, and the `Pending` → `Active` state transition lacking a time-based access control policy.

---

## Findings

### FINDING-1: Caller Identity Not Verified Against BidderId (CRITICAL)

**File:** `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs`
**Handler:** `PlaceBidCommandHandler.cs`

`PlaceBidCommand` is defined as:

```csharp
public sealed record PlaceBidCommand(
    Guid AuctionId,
    Guid BidderId,
    decimal Amount,
    string Currency) : IRequest<Result<Guid>>;
```

`BidderId` is a caller-supplied parameter — it is not derived from the authenticated user's claims. The handler passes this directly to the domain:

```csharp
var result = auction.PlaceBid(request.BidderId, amount, _dateTimeProvider.UtcNow);
```

There is no code anywhere in `PlaceBidCommandHandler`, the `PlaceBidCommandValidator`, or any pipeline behavior that checks `request.BidderId == currentUserId`. This is a classic **Insecure Direct Object Reference (IDOR)** / **Broken Object Level Authorization (BOLA)** vulnerability. An authenticated user can supply the GUID of any other user as `BidderId` and place bids on their behalf, bidding up items they don't intend to win, or manipulating the auction record to point to a different winner.

**Severity:** Critical

**Remediation:** The handler must resolve the authenticated user's identity from `IHttpContextAccessor` or pass it through a dedicated `ICurrentUserService`, then assert `request.BidderId == currentUser.Id` before proceeding. The command record should ideally not accept a `BidderId` from the caller at all — the handler should populate it from the security context.

---

### FINDING-2: CloseAuctionCommand Has No Authorization Check (HIGH)

**File:** `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandHandler.cs`

The handler closes an auction given only an `AuctionId`. There is no check that the caller is the `SellerId` of the auction, an administrator, or a background scheduler. Any authenticated user who can invoke this command can close any auction prematurely, depriving legitimate bidders of time and manipulating who wins.

```csharp
var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
// ... no identity check before:
var result = auction.Close(_dateTimeProvider.UtcNow);
```

The `CloseAuctionCommandValidator` only checks that `AuctionId` is non-empty.

**Severity:** High

**Remediation:** The handler must assert that the requesting user is either the auction's `SellerId` or holds an admin/system role. The command should carry the requestor's identity or the authorization check should happen at the API layer before dispatching.

---

### FINDING-3: CreateAuctionCommand Accepts an Arbitrary SellerId (HIGH)

**File:** `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs`

```csharp
public sealed record CreateAuctionCommand(
    string Title,
    string Description,
    decimal StartingPrice,
    string Currency,
    TimeSpan Duration,
    Guid SellerId) : IRequest<Result<Guid>>;
```

Like `BidderId` in `PlaceBidCommand`, `SellerId` is entirely caller-supplied. A user can create an auction and attribute it to another user's GUID as the seller. The seller-cannot-bid-on-own-auction protection in `Auction.cs` — `if (bidderId == SellerId)` — then becomes trivially bypassable: the attacker sets a victim's GUID as `SellerId`, preventing that victim from ever bidding on the item.

**Severity:** High

**Remediation:** `SellerId` must be populated from the authenticated user's claims in the handler or at the API layer, not accepted from the request body.

---

### FINDING-4: No Expiry Check in PlaceBid Domain Method (MEDIUM)

**File:** `src/services/AuctionKings.Domain/Aggregates/Auction.cs`

`PlaceBid` checks `Status != AuctionStatus.Active` but does not check whether `now >= EndTime`. An auction that has passed its end time but whose status has not yet been updated to `Closed` (i.e., the close command has not fired) will still accept bids from the domain's perspective:

```csharp
public Result<Bid> PlaceBid(Guid bidderId, Money amount, DateTime now)
{
    if (Status != AuctionStatus.Active)
    {
        return Result.Failure<Bid>(DomainErrors.Auction.NotActive);
    }
    // No check: if (now >= EndTime) return failure
```

This creates a race condition window where a user could place a bid after the stated end time, before the background scheduler fires `CloseAuctionCommand`.

**Severity:** Medium

**Remediation:** Add `if (now >= EndTime) return Result.Failure<Bid>(DomainErrors.Auction.NotActive);` (or a dedicated `AuctionExpired` error) at the top of `PlaceBid`.

---

### FINDING-5: Pending Auction Can Be Activated Without Time Verification (MEDIUM)

**File:** `src/services/AuctionKings.Domain/Aggregates/Auction.cs`

The `Activate(DateTime now)` method transitions from `Pending` to `Active` but ignores the `now` parameter entirely — it never checks `if (now < StartTime)`:

```csharp
public void Activate(DateTime now)
{
    if (Status != AuctionStatus.Pending)
    {
        return;
    }
    Status = AuctionStatus.Active;
}
```

If whatever system component calls `Activate` is exposed to user input or is invoked prematurely, an auction can become `Active` before its scheduled start time. Since `PlaceBid` only guards on status, bids could be placed before the auction is supposed to open.

**Severity:** Medium

**Remediation:** Enforce `if (now < StartTime) return;` (or throw a domain error) inside `Activate` to respect the scheduled start.

---

### FINDING-6: Currency Mismatch Not Blocked at Application Layer (LOW)

**File:** `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs`

The handler constructs `new Money(request.Amount, request.Currency)` from caller input and passes it directly to `PlaceBid`. The `Money` comparison operators call `EnsureSameCurrency`, which throws `InvalidOperationException` if currencies differ. If `CurrentPrice` is "USD" and the bid arrives with "EUR", this throws an unhandled exception rather than returning a clean domain error. While the exception is not a security bypass, it is an unhandled error path that could cause incorrect HTTP 500 responses and leaks stack traces if the exception filter is not masking them properly.

**Severity:** Low

**Remediation:** Validate that `request.Currency` matches the auction's currency in the handler or validator before constructing `Money`.

---

### FINDING-7: No Authentication Middleware Configured (Observation)

**File:** `src/services/AuctionKings.API/Program.cs`

```csharp
app.UseAuthorization();
```

`UseAuthentication()` is not present before `UseAuthorization()`. The controllers (`AuctionsController`, `BidsController`) are empty stubs with no `[Authorize]` attributes. The SignalR hubs have no `[Authorize]` attribute either. While the controllers are stubs awaiting implementation, the absence of authentication middleware and attributes means that at the API layer there is currently zero enforcement of identity — reinforcing the severity of FINDING-1 through FINDING-3.

**Severity:** Informational (blocker for all authorization fixes above)

---

## What the Domain Gets Right

These domain-level controls are correctly implemented and are genuine security strengths:

- **Seller self-bid prevention:** `Auction.cs` line 92 — `if (bidderId == SellerId)` returns `DomainErrors.Auction.CannotBidOnOwnAuction`. Enforced in the aggregate, not just the API layer.
- **Minimum bid increment enforcement:** `BidIncrement.GetMinimumBid` is called inside `PlaceBid`; the caller cannot skip it.
- **State machine gates:** `PlaceBid` and `Close` both guard on `Status`, preventing state-skipping.
- **Optimistic concurrency:** The `Version` field on `Auction.cs` is incremented on every mutating operation, and the Infrastructure layer uses xmin-based concurrency tokens. `PlaceBidCommandHandler` retries up to 3 times on `ConcurrencyException`, meaning two simultaneous bids will not silently corrupt the winning bid.
- **Negative amount rejection:** `Money` constructor rejects `amount < 0`; validator additionally requires `Amount > 0`.
- **Encapsulated mutation:** `Bid.Create` is `internal static`, and all `Bid` status-mutating methods are `internal`. External code cannot create or mutate a `Bid` directly — all paths go through the `Auction` aggregate.

---

## Risk Summary Table

| ID          | Description                                          | Severity    |
|-------------|------------------------------------------------------|-------------|
| FINDING-1   | BidderId not verified against authenticated identity | Critical    |
| FINDING-2   | CloseAuctionCommand lacks authorization check        | High        |
| FINDING-3   | SellerId is caller-supplied on CreateAuction         | High        |
| FINDING-4   | PlaceBid does not check EndTime at domain level      | Medium      |
| FINDING-5   | Activate ignores the now parameter (no time guard)   | Medium      |
| FINDING-6   | Currency mismatch throws unhandled exception         | Low         |
| FINDING-7   | No authentication middleware or [Authorize] anywhere | Observation |

---

## Files Reviewed

- `src/services/AuctionKings.Domain/Aggregates/Auction.cs`
- `src/services/AuctionKings.Domain/Aggregates/Bid.cs`
- `src/services/AuctionKings.Domain/ValueObjects/Money.cs`
- `src/services/AuctionKings.Domain/ValueObjects/BidIncrement.cs`
- `src/services/AuctionKings.Domain/ValueObjects/AuctionDuration.cs`
- `src/services/AuctionKings.Domain/Enums/AuctionStatus.cs`
- `src/services/AuctionKings.Domain/Errors/DomainErrors.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommand.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandHandler.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/PlaceBid/PlaceBidCommandValidator.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommand.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandHandler.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CloseAuction/CloseAuctionCommandValidator.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommand.cs`
- `src/services/AuctionKings.Application/Auctions/Commands/CreateAuction/CreateAuctionCommandValidator.cs`
- `src/services/AuctionKings.Application/Behaviors/ValidationBehavior.cs`
- `src/services/AuctionKings.Application/Behaviors/IdempotencyBehavior.cs`
- `src/services/AuctionKings.API/Controllers/AuctionsController.cs`
- `src/services/AuctionKings.API/Controllers/BidsController.cs`
- `src/services/AuctionKings.API/Hubs/AuctionHub.cs`
- `src/services/AuctionKings.API/Filters/RateLimitAttribute.cs`
- `src/services/AuctionKings.API/Filters/IdempotencyFilter.cs`
- `src/services/AuctionKings.API/Program.cs`
