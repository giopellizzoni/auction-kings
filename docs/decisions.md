# Design Decisions

This document records the three architectural choices that most affect correctness and scalability in AuctionKings.

---

## ADR-001: Optimistic Concurrency via PostgreSQL `xmin`

### Context

Multiple bidders can submit bids on the same auction simultaneously. Without concurrency control, two concurrent bids could both read the same "current highest bid", pass validation, and both be persisted — meaning the auction ends with an incorrect winner.

### Decision

Use PostgreSQL's built-in `xmin` system column as an optimistic concurrency token via EF Core's `UseXminAsConcurrencyToken()`. When a bid is placed:

1. The `Auction` aggregate is loaded with its current `xmin` value.
2. The bid is validated against the current state.
3. On `SaveChanges`, EF Core generates `WHERE xmin = <original_value>`. If another transaction has modified the row since the read, `xmin` will have changed and the update will affect 0 rows — EF Core throws `DbUpdateConcurrencyException`.
4. The handler retries up to a configured number of times.

### Why `xmin` over application-managed row versions

- Zero migration overhead: `xmin` is always present on every PostgreSQL row; no extra column or trigger.
- Updated by PostgreSQL on every write — no risk of application forgetting to increment.
- Works correctly under SKIP LOCKED (the Outbox query pattern), unlike advisory locks.

### Limitations

- PostgreSQL-only: migrating to another database requires replacing `xmin` with a standard `rowversion`/`timestamp` column.
- `xmin` wraps around after ~4 billion transactions (extremely unlikely in practice, but theoretically possible on very high-write tables).
- Retry on conflict adds latency under high contention. Under sustained load (see load test results), p99 latency increases with concurrency above ~50 simultaneous bidders on a single auction.

---

## ADR-002: Outbox Pattern for Reliable Domain Event Delivery

### Context

After a bid is placed, domain events (`BidPlaced`, `AuctionClosed`) must be delivered to SignalR (real-time updates) and SQS (downstream services). Publishing directly inside the transaction risks:

- Events fired but transaction rolls back → phantom notifications.
- Transaction commits but publish fails → silent data loss.

### Decision

Use the Transactional Outbox Pattern:

1. Domain events are serialized and saved to the `outbox_messages` table **inside the same database transaction** as the aggregate change.
2. `OutboxProcessor` (background worker) polls the table with `SELECT ... FOR UPDATE SKIP LOCKED`, processes each message, and marks it `processed_at = now()`.
3. If processing fails, `retry_count` is incremented. After `MaxRetries` (default: 3), the message is marked `failed = true` and alerts via structured log.

### Why Outbox over direct publish

- **At-least-once delivery**: the message is durable from the moment the transaction commits.
- **SKIP LOCKED**: multiple instances of the worker can run concurrently without message duplication.
- **Decoupled retry**: failed deliveries don't block the request path.

### Limitations

- **Eventual consistency**: SignalR clients see bid events with a delay equal to the outbox polling interval (default: 5 seconds).
- **At-least-once, not exactly-once**: consumers must be idempotent. The `X-Request-Id` idempotency key on the bid endpoint prevents duplicate bids, but downstream SQS consumers must handle re-delivery.
- **Stuck message risk**: if the database is healthy but SQS/SignalR is down, messages accumulate. The `OutboxHealthCheck` reports `Degraded` when messages are stuck > 5 minutes.

---

## ADR-003: Current System Limits

| Limit | Value | Reason |
|-------|-------|--------|
| Max page size (bids/auctions) | 50 | Prevents unbounded query result sets |
| Rate limit (bids per user) | 10 req / 10 s | Redis sliding window; prevents bid flooding |
| Outbox batch size | 50 messages | Tunable via `Outbox:BatchSize` in `appsettings.json` |
| Outbox polling interval | 5 s | Tunable via `Outbox:PollingIntervalSeconds` |
| Outbox max retries | 3 | After 3 failures, message is marked `failed` |
| JWT expiry | 60 min | Configurable via `Jwt:ExpiresInMinutes` |
| SignalR backplane | Redis | Required for multi-instance deployments |

### Known gaps (not yet implemented)

- **No horizontal scale testing**: the Redis backplane and SKIP LOCKED have been designed for multi-instance, but load tests run against a single API instance.
- **No circuit breaker**: SQS publish failures cause outbox message retry accumulation; no automatic back-pressure or circuit breaker pattern.
- **No audit trail for failed bids**: bids rejected by domain validation are not persisted; post-mortem analysis relies on structured logs.
