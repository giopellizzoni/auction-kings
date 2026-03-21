---
name: principal-architect
description: Defines the architectural competencies, engineering standards, and system design mindset expected from a Principal-level C# / ASP.NET Core engineer building mission-critical, high-concurrency auction systems on AWS.
---

# AuctionKings — Principal Architect Checklist

This checklist covers AuctionKings-specific architectural decisions and invariants. General .NET best practices, CQRS/event sourcing rules, and distributed systems principles are defined in the global CLAUDE.md.

---

## Domain Invariants (Non-Negotiable)

- Bid placement is atomic — optimistic concurrency via PostgreSQL `xmin` shadow property
- Anti-sniping: bids placed within 5 minutes of auction end extend it by 5 minutes
- Highest bid integrity must never be violated — no direct DB writes to bid state
- Auction closing is deterministic — only the `Auction` aggregate triggers close logic
- `Money` is a ComplexProperty (NOT `OwnsOne`) — EF Core 10 global owned-type registration causes conflicts
- All state changes flow through domain events → Outbox → consumers (never bypass)

## Layer Rules (Strict)

```
API → Application → Domain ← Infrastructure
```

- Domain has zero references to Application, Infrastructure, or API
- Application has zero references to Infrastructure or API
- Outbox pattern is mandatory for all domain event publishing — no direct dispatch
- Aggregates expose behavior via methods only — no public setters

## Concurrency Strategy

- Write model: PostgreSQL `xmin` as concurrency token (shadow property, `xid` column type)
- `UseXminAsConcurrencyToken()` was removed in Npgsql 10 — use explicit shadow property
- No distributed locks in the hot bid path — optimistic concurrency only

## Key Infrastructure Facts

- PostgreSQL 16 for write model, Redis 7 for cache + SignalR backplane
- Two SignalR hubs: `AuctionHub` (bidding) and `NotificationHub` (user alerts)
- Background worker project: `AuctionKings.BackgroundWorkers` — Outbox processor lives here
- `Auction.Bids` uses field-access mode: `Navigation(x => x.Bids).UsePropertyAccessMode(PropertyAccessMode.Field)`
- Application `.csproj` filename has a typo: `AuctionKings.Applciation.csproj`

## Definition of Done

A feature is complete when:

- [ ] Business invariants enforced at domain level
- [ ] Concurrency scenario considered
- [ ] Outbox used for event publishing (no direct dispatch)
- [ ] Idempotency guaranteed for command handlers
- [ ] No warnings suppressed — fix root cause
- [ ] Build passes: `dotnet build --configuration Release`
- [ ] Tests pass: `dotnet test test/AuctionKings.UnitTests/`
