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

---

## ADR-005: Bidding Is Never Public

### Context

Audit finding H-3 (issue #102): `GET /auctions/{id}/bids` was anonymous and returned every bid with its `BidderId`, letting anyone map user activity and bidding strategy. Auditing the neighbouring surfaces showed the same identity leaking through the anonymous `GET /auctions/{id}` (`TopBids`), the hub's join payload, and the realtime `BidPlaced` event.

### Decision

- Bid data requires authentication; there is no anonymous bid view.
- A bidder never sees another bidder's identity, in any state of the auction (including after it closes).
- `GET /auctions/{id}/bids` requires login and returns `AuctionBidDto` — amount, currency, time, status, and `IsOwnBid` — with no `BidderId`. The caller's own bids are the only ones identified, via the flag.
- The query handler caches the full page (including `BidderId`) because the cache is shared across callers, and projects it per caller on every response. The cache entry is never returned directly.
- `AuctionDetailDto` no longer carries `TopBids`; the public auction view shows only the current price and `BidCount`. Bids are read from the authenticated endpoint.
- The realtime `BidPlaced` event no longer carries `bidderId`; clients get `auctionId`, `bidId`, `amount`, `currency`.
- `GET /users/{id}/bids` is unchanged: it returns `BidDto` (with `BidderId`) but only to that user or an Admin (#157).

### Limitations

- `AuctionClosed` still broadcasts `winnerId` to the auction group. Whether the winner is revealed at close is a product decision not covered here.
- Admins get no extra view of other bidders on the per-auction list; moderation tooling would need its own endpoint.
- Other participants can still see that bids exist (amounts and counts) once authenticated; only identities are hidden.

---

## ADR-006: No Signing Secrets in Source Control

### Context

Audit finding H-5 (issue #104): the Development JWT signing key (`dev-only-secret-key-at-least-32-chars!!`) was committed in `appsettings.Development.json`. If that value ever reached a deployed environment through a mis-set `ASPNETCORE_ENVIRONMENT` or configuration, anyone could forge valid tokens. Staging was not affected: it runs as `Staging` and gets a random secret from Secrets Manager.

### Decision

- No signing key is committed. `appsettings.Development.json` carries only `Issuer`, `Audience` and `ExpiresInMinutes`.
- Local development gets a per-checkout random secret in the git-ignored `.env.local`, generated by `scripts/setup-local-env.sh` (`openssl rand -hex 32`, 256 bits). The script also adds a secret to an existing `.env.local` that lacks one and replaces an empty `Jwt__Secret=` line, without touching anything else. This reuses the mechanism already used for the DB and Redis passwords (`DotNetEnv` loads `.env.local`) instead of introducing `dotnet user-secrets` as a second place for secrets.
- Docker keeps using `JWT_SECRET` from `docker/.env.local`; deployed environments use a secrets manager (`Jwt__Secret` from Secrets Manager in staging).
- `JwtOptionsValidator` runs in `Program.cs` before the signing key is built (and again through `ValidateOnStart`). It fails startup when the secret is missing or blank, under 32 bytes (the HS256 minimum), or equal to the previously committed placeholder, in every environment. It also requires `Issuer` and `Audience`.

### Why reject the placeholder everywhere, not just outside Development

The key was public the moment it was committed and remains in git history, so it must be treated as burned. It no longer exists in configuration, which makes "only outside Development" a rule with no legitimate use; rejecting it everywhere also catches a developer pasting it back in.

### Limitations

- The placeholder stays in git history. It was never a real secret, but any environment that ever ran with it must be treated as compromised and re-keyed.
- Developers must re-run `./scripts/setup-local-env.sh` once after pulling this change; until then the API refuses to start, with a message that says so.
- The validator checks length and the known placeholder, not entropy. A short guessable value that is 32+ bytes would pass.
- Staging still enables `Features:EnableDevTokenEndpoint`, which mints tokens signed with the real staging secret for anyone who calls it. That is a deliberate demo setting from #157, tracked separately.

---

## ADR-007: SignalR Throttling

### Context

Audit finding H-7 (issue #106): hub methods accepted unlimited calls, so an authenticated user could flood `JoinAuctionAsync` / `RequestStateSyncAsync` and open any number of connections. `JoinAuctionAsync` also added the connection to the group *before* checking the auction existed, so a single connection could collect an unbounded number of groups by joining random GUIDs, and every unknown GUID cost a database query because failed lookups are not cached.

### Decision

- One global `HubThrottlingFilter` (`IHubFilter`) registered through `HubOptions`, rather than checks at the top of each method, so every current and future hub method is covered and both hubs are covered.
- **Invocations** are limited per *user* (`rate:hub:{userId}`), not per connection, so extra connections do not buy extra calls. It uses the existing Redis sliding-window limiter through a new per-call overload `IsAllowedAsync(key, limit, window)`; the REST limiter and its global limit are unchanged. Over the limit the call throws `HubException` and the connection stays open.
- **Connections** are capped per user after the hub has registered the connection with `IConnectionTracker`; an over-cap connection is aborted and then untracked by `OnDisconnectedAsync`. `AuctionHub` and `NotificationHub` share the tracker's per-user count, so the default of 10 allows about five tabs with both hubs open.
- **Groups**: `JoinAuctionAsync` rejects once a connection is at its group cap, validates the auction exists, and only then joins the group. An unknown auction throws `HubException` and uses no slot. The joined set lives in `Context.Items` and is freed by `LeaveAuctionAsync`.
- `MaximumReceiveMessageSize` is 8 KB (hub arguments are a GUID) and `MaximumParallelInvocationsPerClient` is pinned to 1, which also keeps the per-connection group set safe without locking.
- All limits live in the validated `HubThrottling` section (every value must be greater than zero).
- **Fails open** when Redis is unreachable: hub methods only read cached data, and with Redis down the SignalR backplane is down as well. REST bid placement remains fail-closed.

### Limitations

- The connection cap is approximate: a connection is added by the hub and counted afterwards by the filter, as separate steps, so simultaneous connects can briefly be over-admitted or over-rejected. Making `ConnectionTracker` atomic (ADR-008) fixes the key-expiry leak but does not by itself make the cap exact; that needs an atomic "add and return the count".
- The invocation limiter costs one Redis round trip per hub call, and a rejected call still reaches the server. It bounds work done per user, not raw message volume; the message-size limit and the reverse proxy remain the first defence against raw floods.
- `RequestStateSyncAsync` is limited but not tied to joined auctions, so a user can still request state for any auction ID within their call budget (the REST auction endpoint is public anyway).
- Limits are per user. An attacker with many accounts is outside this control; account creation controls and WAF rate limiting apply.

---

## ADR-008: Atomic Connection Tracking

### Context

Audit finding M-21 (issue #127): `ConnectionTracker` created a user's connection set with `SADD` and then gave it a TTL with a separate `EXPIRE`. A process crash between the two commands left the key behind with no expiry, so it would leak memory indefinitely. `UntrackAsync` had the same shape (`SREM`, `SCARD`, `EXPIRE`).

### Decision

- `TrackAsync` and `UntrackAsync` are each a single Lua script, which Redis runs atomically: track is `SADD` + `PEXPIRE`; untrack is `SREM`, then `PEXPIRE` only while other connections remain (Redis deletes the set when its last member is removed). A key can no longer exist without its TTL.
- One key per script, in the same plain-`EVAL` style as `RedisRateLimiter`, so it is also safe on Redis Cluster. `IConnectionTracker` and `GetConnectionsAsync` are unchanged.
- The mock-based unit tests, which asserted that two separate commands were issued, are replaced by Testcontainers Redis tests that assert behaviour: TTL set and refreshed, last connection deletes the key, concurrent tracks, and a regression probe that takes atomic `EXISTS` + `PTTL` snapshots from a second connection while keys are created and fails if it ever sees a key without a TTL. The probe fails against the previous implementation.

### Limitations

- A connection that dies without `OnDisconnectedAsync` running (for example a crashed container) leaves its ID in the set. The TTL is refreshed on every `TrackAsync`, so for an active user those IDs do not expire on their own, and enough of them would eventually count against the per-user connection cap from ADR-007. Per-connection expiry (a sorted set scored by deadline, under a new key name) would fix this and is not part of this change.
- The cap check still adds and counts as separate steps (see ADR-007).

---

## ADR-009: Versioned Cache Scopes

### Context

Audit finding M-8 (issue #114): `RemoveByPrefixAsync` found keys with `SCAN` and deleted them with one multi-key `DEL`.

- `SCAN` walks the whole keyspace, which also holds rate-limit counters, idempotency keys and connection sets, and it ran three times for every bid (`PlaceBid` invalidates the auction, the active listings and the bidder's history), and again on close, create and activate.
- On Redis Cluster it scanned one arbitrary node, and a multi-key `DEL` across slots is rejected with `CROSSSLOT`, so invalidation would not have worked even with a correct scan.
- Read-through caching was racy: a reader could load old data, a bid could commit and invalidate, and the reader could then write its old data back, to be served for up to the entry's TTL (5 minutes for auction details).

### Decision

Replace prefix invalidation with **versioned scopes** in `ICacheService`:

- Entries belong to a scope (`CacheScopes.Auction(id)`, `CacheScopes.ActiveListings`, `CacheScopes.UserBids(userId)`). `GetOrSetAsync` / `GetOrSetIfFoundAsync` read and write scoped entries; `InvalidateScopeAsync` invalidates a scope. `RemoveByPrefixAsync` and the unscoped `GetOrSetAsync` are removed so scanning cannot come back. Plain `GetAsync` / `SetAsync` / `RemoveAsync` remain for data that is never invalidated by scope (stored idempotency responses).
- A scope's version lives in `cachever:{scope}` and is a random GUID; an absent marker reads as `0`. Entries are stored as `{version}|{json}`. A read fetches the marker and the entry back to back (one round trip) and is a hit only if the versions match. Invalidation is a single atomic `SET` of a new random version with a one-hour TTL.
- The version is read before the factory runs and the result is stored under that version. If the scope is invalidated while the factory loads, the result is stored under a dead version and is never served. Callers must therefore invalidate after the change commits, which every command handler already does.
- Entry lifetimes are clamped to the marker's one-hour lifetime. Because versions are random and never repeat, a marker that expires and is recreated cannot make an old entry valid again.
- Everything fails open: if Redis is unreachable a read is a miss and nothing is written; a failed invalidation is logged and entries expire normally.
- Unknown auctions are not cached (`GetOrSetIfFoundAsync`), as before.

### Why not tag sets

Tracking each scope's live keys in a Redis set would have kept the code change smaller, but it needs pruning to stay bounded (the public listing accepts any page number, so an attacker can create unlimited entries), costs work proportional to the number of live entries on every invalidation, adds a second write per cache write, and leaves the stale re-cache race in place.

### Limitations

- Entries from before an invalidation are not deleted; they hold memory until their TTL expires (at most 5 minutes).
- Each cache read is two `GET`s instead of one (sent together, so no extra round trip), and each scope that has been invalidated keeps a small marker key for an hour.
- Entries written by the previous code have no version and are treated as misses, so a rolling deploy simply re-warms the cache.
- Page numbers are still unbounded, so a flood of distinct pages can still fill the cache until TTLs reclaim it. That is unchanged by this decision; it is no longer amplified by invalidation, which does not depend on how many entries exist.
