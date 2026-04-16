# AuctionKings Codebase Audit Report

---

## Executive Summary

### Severity Counts

| Severity | Count |
| -------- | ----- |
| CRITICAL | 1     |
| HIGH     | 7     |
| MEDIUM   | 9     |
| LOW      | 6     |
| INFO     | 5     |

---

## Findings by Severity

### 🔴 CRITICAL

- **[AUD-01] Missing Auction Activation Path**
  _Files:_ `Auction.cs:59`, `CreateAuctionCommandHandler.cs:34`

---

### 🟠 HIGH — Security

- **[AUD-02] Missing `[Authorize]` on SignalR Hubs**
  _Files:_ `AuctionHub.cs:8`, `NotificationHub.cs:6`

- **[AUD-03] CreateAuction endpoint lacks authorization**
  _File:_ `AuctionsController.cs:31`

- **[AUD-04] User bid history exposed without auth**
  _File:_ `UsersController.cs:17`

- **[AUD-05] GetBids endpoint lacks auth**
  _File:_ `BidsController.cs:64`

- **[AUD-06] Rate limiter fails open when Redis is down**
  _File:_ `RedisRateLimiter.cs:70-78`

- **[AUD-07] `[RateLimit]` attribute defined but never applied to any endpoint**

- **[AUD-08] No SignalR connection/method throttling**
  _File:_ `AuctionHub.cs`

---

### 🟡 MEDIUM — Security & Consistency

- **[AUD-09] No CORS configuration**
  _File:_ `Program.cs`

- **[AUD-10] Idempotency key accepts arbitrary strings**
  _File:_ `IdempotencyFilter.cs:35`

- **[AUD-11] Inconsistent error handling in Auction aggregate**
  _(Create throws, PlaceBid/Close return Result)_

- **[AUD-12] Inconsistent validator access modifiers**
  _(CreateAuctionCommandValidator public vs others internal)_

- **[AUD-13] Duplicated `MapError()` in AuctionsController and BidsController**

- **[AUD-14] UsersController error handling differs from other controllers**

- **[AUD-15] Missing AuctionCancelledEvent**
  _(Cancel() modifies state without emitting domain event)_

- **[AUD-16] IConnectionMultiplexer not disposed on shutdown**
  _File:_ `DependencyInjection.cs:64`

- **[AUD-17] TreatWarningsAsErrors commented out**
  _File:_ `Directory.Build.props:8`

---

### 🟢 LOW — Dead Code & Infrastructure

- **[AUD-18] Orphaned file `UsersControllers.cs`**
  _(plural typo, 1-line comment)_

- **[AUD-19] Unused RequestLoggingMiddleware**
  _(never registered)_

- **[AUD-20] Unused `Money.Zero()` factory method**
  _File:_ `Money.cs:24`

- **[AUD-21] `Auction.Cancel()` never invoked in production code**

- **[AUD-22] `Auction.Activate()` has no production caller**
  _(only tests)_

- **[AUD-23] ConnectionTracker Redis ops not atomic**
  _(SetAdd + KeyExpire separate calls)_

---

### 🔵 INFO — Configuration & Testing Gaps

- **[AUD-24] Missing config validation in BackgroundWorkers**
  _(no ValidateOnStart)_

- **[AUD-25] SQS publish failures silently swallowed**
  _File:_ `SqsMessagePublisher.cs:47`

- **[AUD-26] Hardcoded JWT dev secret in source control**
  _File:_ `appsettings.Development.json:12`

- **[AUD-27] No authorization enforcement tests**

- **[AUD-28] No SignalR hub authentication tests**

---
