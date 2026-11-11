# AuctionKings Runbook

Operational reference for running, monitoring, and troubleshooting AuctionKings in local or staging environments.

---

## 0. Local Setup From a Clean Checkout

Everything below works end-to-end on a fresh clone with no manual `psql`/`redis-cli` steps.

```bash
# 1. Generate docker/secrets/*.txt and .env.local (root + docker/) — idempotent, run once
./scripts/setup-local-env.sh

# 2. Start infra: PostgreSQL, Redis, LocalStack, Prometheus, Grafana
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# 3. Start the API — auto-applies EF Core migrations on startup in Development
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# 4. In a separate terminal, start the background workers (outbox processor, auction closer)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

Confirm it's healthy, then create an auction — it's **Active immediately** (auctions have no
scheduled-start concept yet, so `CreateAuctionCommandHandler` activates on creation):

```bash
curl -s http://localhost:5081/health/ready | jq .

curl -s -X POST http://localhost:5081/api/v1/auctions \
  -H "Content-Type: application/json" \
  -H "X-Request-Id: $(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen)" \
  -d '{
    "title": "Vintage Rolex Submariner",
    "description": "Classic 1960s timepiece.",
    "startingPrice": 5000.00,
    "currency": "USD",
    "durationInHours": 1,
    "sellerId": "550e8400-e29b-41d4-a716-446655440000"
  }'
# -> "status": "Active" in a follow-up GET /api/v1/auctions/{id}
```

Notes:

- `dotnet run` loads `.env.local` from the repo root automatically (`DotNetEnv` in `Program.cs`).
  `docker compose` does **not** auto-load it — pass `--env-file .env.local` explicitly, or the
  API/worker containers crash on startup (`JWT IDX10703`, Redis `NOAUTH`).
- Do **not** add `Jwt__Secret`/`Jwt__Issuer`/`Jwt__Audience` to the root `.env.local`.
  `appsettings.Development.json` already has a valid dev signing key; an empty `Jwt__Secret`
  env var overrides it via config precedence and crashes auth on the very first request
  (`IDX10703: key length is zero`) — including unauthenticated ones, since `JwtBearerOptions`
  are built lazily by the auth middleware. `scripts/setup-local-env.sh` already avoids this.
- `/dev/token` (see §8) only works when `ASPNETCORE_ENVIRONMENT=Development` — true for
  `dotnet run` (via `launchSettings.json`) and for `docker compose up` (the all-in-one compose
  file is local-only and is never used for staging/production deploys).

---

## 1. Starting the Stack

**First time on this checkout:** generate local secrets and env files once —

```bash
./scripts/setup-local-env.sh
```

This creates `docker/secrets/postgres_password.txt` and `redis_password.txt` (random,
git-ignored), plus `.env.local` (repo root) and `docker/.env.local`, wired to match. Safe to
re-run; it never overwrites files that already exist. See [Local Setup](#0-local-setup-from-a-clean-checkout)
below for details.

```bash
# 1. Start infrastructure (PostgreSQL, Redis, LocalStack)
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# 2. Start the API (port 5081) — applies EF Core migrations automatically in Development
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# 3. Start background workers (separate terminal)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

Or with Docker Compose (all-in-one — API, worker, and infra in containers):

```bash
cd docker && docker compose --env-file .env.local up -d
```

Every `POST`/`PUT` request requires an `X-Request-Id` header (idempotency), including
`/dev/token` and `POST /api/v1/auctions` — not just bid placement. Omitting it returns
`400 {"code":"Idempotency.MissingKey", ...}`. All examples below include it.

---

## 2. Health Checks

| Endpoint | Checks | Expected |
|----------|--------|----------|
| `GET /health/live` | Process is alive | `200 Healthy` |
| `GET /health/ready` | DB + Redis + Outbox | `200 Healthy` or `207 Degraded` |
| `GET /health/db` | PostgreSQL connectivity | `200 Healthy` |

**Quick check:**

```bash
curl -s http://localhost:5081/health/ready | jq .
```

A healthy response looks like:

```json
{
  "status": "Healthy",
  "totalDurationMs": 12.4,
  "checks": [
    { "name": "database", "status": "Healthy", "durationMs": 8.1 },
    { "name": "redis", "status": "Healthy", "durationMs": 2.3 },
    { "name": "outbox", "status": "Healthy", "durationMs": 1.9 }
  ]
}
```

---

## 3. Reading Logs

Logs are emitted as structured JSON (Serilog). Each log line includes `RequestId`, `CorrelationId`, `TraceId`.

**Filter logs by correlation ID:**

```bash
dotnet run ... 2>&1 | jq 'select(.CorrelationId == "abc-123")'
```

**Filter by log level:**

```bash
dotnet run ... 2>&1 | jq 'select(.Level == "Error" or .Level == "Fatal")'
```

**Key log events to watch:**

| Message | Meaning |
|---------|---------|
| `Outbox message {Id} processed successfully` | Normal outbox dispatch |
| `Failed to process outbox message {Id} (attempt {RetryCount})` | Transient failure; will retry |
| `Outbox message {Id} exceeded max retries` | Dead-lettered message; investigate manually |
| `Concurrency conflict on bid placement` | High contention on auction; normal under load |
| `Request completed in {Elapsed}ms` | Serilog HTTP request log |

---

## 4. Monitoring the Outbox

**Via health check:**

```bash
curl -s http://localhost:5081/health/ready | jq '.checks[] | select(.name == "outbox")'
```

- `Healthy` — no messages stuck > 5 minutes
- `Degraded` — some messages are stuck; check the background worker logs

**Via Prometheus gauge:**

```bash
curl -s http://localhost:5081/metrics | grep outbox
```

Example output:

```
# HELP auction_kings_outbox_pending_messages_count Count of outbox messages fetched in the last processing batch (approximation of pending queue depth; capped at batch size).
# TYPE auction_kings_outbox_pending_messages_count gauge
auction_kings_outbox_pending_messages_count 0
```

A non-zero value that does not decrease over multiple polls indicates the background worker is not running or is failing.

**Via database (direct query):**

```bash
docker exec -it infra-postgres \
  psql -U dev -d AuctionKingsDB \
  -c "SELECT COUNT(*) FROM outbox_messages WHERE processed_at IS NULL AND failed = false;"
```

---

## 5. Metrics

The `/metrics` endpoint (Prometheus format) is available at:

```
GET http://localhost:5081/metrics
```

Key metrics:

| Metric | Type | Description |
|--------|------|-------------|
| `http_request_duration_seconds` | Histogram | HTTP request duration by method/route/status |
| `auction_kings_bids_placed_total` | Counter | Successful bids placed since startup |
| `auction_kings_outbox_pending_messages_count` | Gauge | Pending outbox messages (approximation, updated each batch) |

---

## 6. Grafana Dashboard

When running the infra stack (`docker-compose.infra.yml`), Grafana is available at:

```
http://localhost:3000   (admin / admin)
```

The **AuctionKings Overview** dashboard is auto-provisioned and shows:
- Request rate and error rate (by status code)
- p50 / p99 latency
- In-progress requests
- Bids placed rate and cumulative total
- Outbox pending messages gauge (red threshold at 50+)

Prometheus is available at `http://localhost:9090` for ad-hoc PromQL queries.

**Quick check that Prometheus is scraping the API:**

```bash
curl -s 'http://localhost:9090/api/v1/targets' | jq '.data.activeTargets[0].health'
# Expected: "up"
```

---

## 7. AWS Staging

The staging environment runs on ECS Fargate. Access the staging API via the ALB DNS name output by Terraform:

```bash
cd infra/terraform/staging
terraform output alb_url
```

**Check staging health:**

```bash
ALB=$(terraform output -raw alb_dns_name)
curl -s "http://$ALB/health/ready" | jq .
```

**Tail staging logs (CloudWatch):**

```bash
aws logs tail /ecs/auction-kings-staging-api --follow --region us-east-1
```

**Force a new deployment** (after pushing a new image):

```bash
aws ecs update-service \
  --cluster auction-kings-staging \
  --service auction-kings-staging-api \
  --force-new-deployment \
  --region us-east-1
```

**Check ECS service stability:**

```bash
aws ecs wait services-stable \
  --cluster auction-kings-staging \
  --services auction-kings-staging-api \
  --region us-east-1
```

---

## 8. Troubleshooting

### Database is down

**Symptom:** `GET /health/db` returns `503 Unhealthy`. All write operations fail with 500.

**Resolution:**

```bash
docker ps | grep postgres
cd docker && docker-compose -f docker-compose.infra.yml up -d postgres
```

Wait ~10 seconds for the connection pool to recover. Health check will return `Healthy` automatically.

---

### Redis is down

**Symptom:** `GET /health/ready` shows Redis as `Unhealthy`. Rate limiting returns 500 instead of 429. SignalR connections may drop.

**Resolution:**

```bash
docker ps | grep redis
cd docker && docker-compose -f docker-compose.infra.yml up -d redis
```

The API reconnects automatically via StackExchange.Redis retry policy.

---

### Outbox is stuck (messages not processing)

**Symptom:** `auction_kings_outbox_pending_messages_count` gauge is non-zero and not decreasing. `GET /health/ready` shows outbox as `Degraded`.

**Checks:**

1. Is the background worker running?

   ```bash
   ps aux | grep BackgroundWorkers
   ```

2. Are messages failing repeatedly?

   ```bash
   docker exec -it infra-postgres \
     psql -U dev -d AuctionKingsDB \
     -c "SELECT id, type, retry_count, failed FROM outbox_messages WHERE failed = true ORDER BY occurred_on DESC LIMIT 10;"
   ```

**Resolution for failed messages:** Reset them for re-processing:

```bash
docker exec -it infra-postgres \
  psql -U dev -d AuctionKingsDB \
  -c "UPDATE outbox_messages SET failed = false, retry_count = 0 WHERE id = '<message-id>';"
```

**Known issue — every outbox message currently fails, permanently:** `OutboxProcessor.ProcessMessageAsync`
(`src/workers/AuctionKings.BackgroundWorkers/OutboxProcessor.cs`) calls
`IPublisher.Publish(domainEvent, ...)` (MediatR) on the deserialized event. Domain events only
implement the plain `IDomainEvent` marker interface (`src/common/AuctionKings.Shared/Common/IDomainEvent.cs`),
not MediatR's `INotification`, so this throws `System.ArgumentException: notification does not
implement $INotification` on every single message, exhausts `MaxRetries`, and the message is
marked `failed = true`. There are currently zero `INotificationHandler<T>` implementations
anywhere in the codebase, so this call has no handlers to reach even if it didn't throw — actual
external delivery goes through the separate `eventBus.PublishAsync(...)` call right after it,
which is unaffected. `/health/ready`'s outbox check only looks at messages stuck (unprocessed)
for 5+ minutes, not at `failed = true` rows, so this failure is currently invisible to that
health check — reported healthy while 100% of messages permanently fail. Not fixed here;
flagging for a deliberate decision (drop the dead `publisher.Publish` call, or add the missing
notification handlers / make domain events implement `INotification`, plus have the outbox
health check count `failed = true` rows).

---

### High bid latency / concurrency conflicts

**Symptom:** `http_request_duration_seconds` p99 spikes. Logs show frequent concurrency conflict errors.

**Diagnosis:** Expected under high contention on a single auction. Optimistic concurrency retry adds latency.

**Mitigation:**

- Increase `Outbox:BatchSize` to process events faster.
- Scale the API horizontally (Redis backplane handles SignalR fan-out).
- Reduce bidding concurrency at the client level (exponential backoff on 409 Conflict).

---

### Two different 409s on `POST /bids` — don't confuse them

Both are real, both return `409`, but they mean different things and have different bodies:

1. **`Auction.ConcurrencyConflict`** — optimistic concurrency exhausted its 3 retries in
   `PlaceBidCommandHandler`. Body: `{"code":"Auction.ConcurrencyConflict","message":"The bid
   could not be placed due to a concurrent update. Please try again."}`. Legitimate under real
   concurrent writers to the same auction; the client should retry.

2. **Duplicate `X-Request-Id`** — the idempotency key was already used for a prior request
   (`IdempotencyBehavior` checks existence only, not payload equality, so reusing a key with a
   *different* body still throws). Body is an RFC7807 `ProblemDetails`, not the `{code,message}`
   shape above: `{"type":"...","title":"Duplicate Request","status":409,"detail":"Command with
   idempotency key '<key>' has already been processed.", ...}`. This means the request was
   already processed — generate a fresh `X-Request-Id` per attempt, don't retry with the same one.

If you see `Auction.ConcurrencyConflict` on **strictly sequential** requests against a single
auction with no other writer in play, that's not expected — optimistic concurrency conflicts
require an actual competing writer between your read and your write. Rule out idempotency-key
reuse first (check the response body shape above), and check whether the auction's row was
mutated outside EF Core (e.g. a manual `UPDATE` via `psql`) between your requests.

---

### JWT token rejected (401 on bid)

**Steps:**

1. Generate a fresh Bidder token (`/dev/token` is also gated by the global idempotency
   filter — it needs `X-Request-Id` like any other POST):

   ```bash
   curl -s -X POST http://localhost:5081/dev/token \
     -H "Content-Type: application/json" \
     -H "X-Request-Id: $(uuidgen)" \
     -d '{"userId": "00000000-0000-0000-0000-000000000001", "role": "Bidder"}' | jq .token
   ```

2. Use the token:

   ```bash
   curl -s -X POST "http://localhost:5081/api/v1/auctions/{auctionId}/bids" \
     -H "Authorization: Bearer <token>" \
     -H "Content-Type: application/json" \
     -H "X-Request-Id: $(uuidgen)" \
     -d '{"bidderId": "00000000-0000-0000-0000-000000000001", "amount": 150, "currency": "USD"}'
   ```

3. If still 401, the token has expired (default: 60 min). Re-generate.

4. For CloseAuction (403 Forbidden), generate an Admin token:

   ```bash
   curl -s -X POST http://localhost:5081/dev/token \
     -H "Content-Type: application/json" \
     -H "X-Request-Id: $(uuidgen)" \
     -d '{"userId": "00000000-0000-0000-0000-000000000002", "role": "Admin"}' | jq .token
   ```
