# Design Decisions

Full ADR records for AuctionKings. For a brief summary see [architecture.md — Key Design Decisions](architecture.md#13-key-design-decisions).

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

---

## ADR-004: Explicit CORS Origin Allowlist

### Context

The API had no CORS configuration (security audit finding H-1, issue #100). Without CORS headers browsers
already block cross-origin reads, and authentication is a bearer token (header, or `access_token` query
parameter for `/hubs/*`) rather than a cookie, so a foreign site cannot ride a user's session. The gap was
the absence of a deliberate, reviewable policy — which the planned Blazor WASM frontend (served from a
different origin via CloudFront) will need anyway.

### Decision

- A single default policy (`API/Cors/CorsExtensions.cs`) built from `Cors:AllowedOrigins`:
  - origins: exact allowlist only, with `AllowCredentials()` (required by the SignalR browser client);
  - methods: `GET`, `POST`, `PUT`, `DELETE`;
  - request headers: `Authorization`, `Content-Type`, `X-Request-Id`, `X-Correlation-Id`, `X-Requested-With`, `X-SignalR-User-Agent`;
  - exposed headers: `X-Correlation-Id`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `Retry-After`;
  - preflight cache: 10 minutes.
- `CorsSettingsValidator` fails startup on wildcards or anything that is not an exact `scheme://host[:port]` origin.
- The same list feeds `WebSocketOptions.AllowedOrigins`, because CORS does not apply to WebSocket upgrades.
- `UseApiCors()` runs before `UseHttpsRedirection()` (browsers reject redirected preflights) and before authentication.
- The default list is empty in every environment until a browser frontend exists.

### Limitations

- With an empty list the WebSocket middleware performs no origin check (framework behavior). Cross-origin
  `negotiate` is still blocked by CORS and hub access still requires a valid JWT.
- Once origins are configured, browser WebSocket connections from any unlisted origin — including the API's
  own origin — get `403`. The API serves no browser UI that uses the hubs today.
- Staging Terraform does not set origins yet; add a `cors.allowed_origins` input when the frontend lands.
