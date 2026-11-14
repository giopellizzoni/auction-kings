# AuctionKings — Repeatable 5-Minute Demo Script

This demo showcases the AuctionKings real-time auction platform: creating auctions, placing concurrent bids, and inspecting results. All commands are copy-pasteable.

**Estimated time:** ~5 minutes
**Requirements:** `curl`, `jq`, `bash`

---

## Setup Phase

**First time on this checkout**, generate local secrets and env files once (see `docs/runbook.md` §0):

```bash
./scripts/setup-local-env.sh
```

### Option A: Full Docker Stack

```bash
# From the repo root — start all services (API, worker, PostgreSQL, Redis, LocalStack, Moto, Prometheus, Grafana)
cd docker
docker compose --env-file .env.local up -d
cd ..

# Wait ~15 seconds for services to be healthy
sleep 15

# Export base URL for demo (the API container migrated the schema on startup)
export API_URL="http://localhost:5081"
```

`docker compose` does **not** auto-load `.env.local` — `--env-file .env.local` is required, or
the API/worker containers crash on startup (missing JWT secret / Redis password).

### Option B: Local Development (dotnet run, recommended)

```bash
# Start infrastructure first
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# In a separate terminal, run the API (applies pending migrations on startup)
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# In another terminal, run the background workers (outbox processor, auction closer)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj

# Wait for startup (~20 seconds), then in your main terminal:
export API_URL="http://localhost:5081"
```

### Database schema

No manual step: in Development the API applies pending EF Core migrations on startup
(`Database:MigrateOnStartup` in `appsettings.Development.json`). To migrate by hand instead, see
`docs/runbook.md` §0.

---

## Demo: Step-by-Step

### Step 1: Verify Health Endpoints

```bash
# Check liveness (plain-text body, e.g. "Healthy" — not JSON)
curl -s "$API_URL/health/live"; echo

# Check readiness (DB + Redis + Outbox)
curl -s "$API_URL/health/ready" | jq .
```

**Expected output:**
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

`outbox` shows `Degraded` if the background worker isn't running (or hasn't caught up yet) —
that's expected until Option B's second `dotnet run` (or the Docker `worker` container) is up.

---

### Step 2: Generate Tokens (a seller and 3 bidders, fixed UUIDs for repeatability)

`/dev/token` works only when `Features:EnableDevTokenEndpoint` is `true` (it is in
`appsettings.Development.json`, so for both options above; otherwise it returns 404). It needs no
`X-Request-Id`: that header is required only on actions marked `[Idempotent]` (placing a bid).
Identities come from the token's `sub` claim — request bodies never carry a seller or bidder ID.

```bash
token() {
  curl -s -X POST "$API_URL/dev/token" -H "Content-Type: application/json" \
    -d "{\"userId\": \"$1\", \"role\": \"Bidder\"}" | jq -r '.token'
}

SELLER_ID="550e8400-e29b-41d4-a716-446655440000"; SELLER_TOKEN=$(token "$SELLER_ID")
BIDDER_1="550e8400-e29b-41d4-a716-446655440001"; TOKEN_1=$(token "$BIDDER_1")
BIDDER_2="550e8400-e29b-41d4-a716-446655440002"; TOKEN_2=$(token "$BIDDER_2")
BIDDER_3="550e8400-e29b-41d4-a716-446655440003"; TOKEN_3=$(token "$BIDDER_3")

echo "Seller token: $SELLER_TOKEN"
```

Creating an auction also requires the `Bidder` role, so the seller gets a Bidder token too. A
seller cannot bid on their own auction.

---

### Step 3: Create an Auction (10-minute duration for quick demo)

```bash
AUCTION_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/auctions" \
  -H "Authorization: Bearer $SELLER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Vintage Rolex Submariner",
    "description": "Classic 1960s timepiece in excellent condition.",
    "startingPrice": 5000.00,
    "currency": "USD",
    "durationInHours": 0.166
  }')

# The endpoint returns the new auction's id as a bare JSON string, not an object.
echo "$AUCTION_RESPONSE"
AUCTION_ID=$(echo "$AUCTION_RESPONSE" | jq -r '.')
echo "Auction ID: $AUCTION_ID"
```

**Expected output:** a quoted UUID, e.g. `"3042d012-ac4a-4520-a622-875ef50f3d16"`.

Without a `startTime` (or with one in the past) the auction is Active immediately; a future
`startTime` keeps it Pending until `AuctionActivationWorker` activates it. Here a follow-up `GET`
already shows `"status": 1` (Active):

```bash
curl -s "$API_URL/api/v1/auctions/$AUCTION_ID" | jq .
```

```json
{
  "id": "3042d012-ac4a-4520-a622-875ef50f3d16",
  "title": "Vintage Rolex Submariner",
  "description": "Classic 1960s timepiece in excellent condition.",
  "currentPrice": 5000.00,
  "currency": "USD",
  "startTime": "2026-04-15T12:00:00Z",
  "endTime": "2026-04-15T12:10:00Z",
  "status": 1,
  "bidCount": 0
}
```

`status` is the numeric `AuctionStatus` enum (`0`=Pending, `1`=Active, `2`=Closed,
`3`=Cancelled) — the API does not serialize it as a string.

---

### Step 4: Demonstrate 401 Without Token

Try placing a bid without an authorization header:

```bash
curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "$API_URL/api/v1/auctions/$AUCTION_ID/bids" \
  -H "Content-Type: application/json" \
  -H "X-Request-Id: $(uuidgen)" \
  -d '{"amount": 5100.00, "currency": "USD"}'
```

**Expected output:** `HTTP 401` with an empty body (the default ASP.NET Core JWT bearer
challenge — there's no custom error payload for this case).

---

### Step 5: Place 20 Sequential Bids (3 bidders, incrementing amounts)

Bids on the same auction must be sequential here, not truly concurrent — see the note below.

```bash
for i in {1..20}; do
  MOD=$((i % 3))
  # Rotate bidders: the current winner may not outbid themselves.
  if   [ $MOD -eq 1 ]; then TOKEN="$TOKEN_1"
  elif [ $MOD -eq 2 ]; then TOKEN="$TOKEN_2"
  else                       TOKEN="$TOKEN_3"
  fi

  # +100 per step: BidIncrement requires a $100 minimum raise once price is >= $5,000 (see
  # BidIncrement.cs) — a $50 step would alternate 201/400 as it repeatedly undercuts the minimum.
  BID_AMOUNT=$((5000 + i * 100))

  curl -s -w " -> HTTP %{http_code}\n" -X POST "$API_URL/api/v1/auctions/$AUCTION_ID/bids" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -H "X-Request-Id: $(uuidgen)" \
    -d "{\"amount\": $BID_AMOUNT, \"currency\": \"USD\"}"
done
echo "All 20 bids placed."
```

**Expected output:** 20 lines, each a quoted bid-id UUID followed by `HTTP 201`, then
`All 20 bids placed.`

**Why sequential, not parallel:** the original version of this script fired all 20 requests as
background jobs (`... &` + `wait`). Each bidder's own retries aside, running them truly in
parallel is a legitimate way to trigger `Auction.ConcurrencyConflict` (409) by design — that's
optimistic concurrency working as intended under real contention, not a bug. If you want to see
that behavior on purpose, add `&` back before the closing `done` and `wait` after the loop, and
expect (and handle) some `409`s.

Always use a **fresh** `X-Request-Id` per bid. Reusing one after the first request finished
replays its stored response (same status and body, no new bid) — even if you changed the amount.
Reusing it while the first request is still running returns a 409 RFC7807 `ProblemDetails` with
`"title": "Duplicate Request"`. See `docs/runbook.md` → "Two different 409s on `POST /bids`".

Each bidder places about 7 bids here, under the `place-bid` rate limit of 10 per 10 seconds per
user; faster or larger loops get `429 Too Many Requests`.

---

### Step 6: Inspect Bids

List the bids placed on the auction (`GET /bids` returns a paged envelope, not a bare array).
Bidding is never public: the call needs a token, and bidders stay anonymous — each bid only carries
`isOwnBid`, which is `true` for the caller's own bids. The anonymous call is a 401.

```bash
echo "=== Without a token (expect HTTP 401) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "$API_URL/api/v1/auctions/$AUCTION_ID/bids"

echo "=== All Bids on Auction, as bidder 1 ==="
curl -s -H "Authorization: Bearer $TOKEN_1" "$API_URL/api/v1/auctions/$AUCTION_ID/bids" | \
  jq '.items[] | {id, amount, placedAt, status, isOwnBid}'

echo ""
echo "=== Total Bids Count ==="
curl -s -H "Authorization: Bearer $TOKEN_1" "$API_URL/api/v1/auctions/$AUCTION_ID/bids" | jq '.totalCount'
```

---

### Step 7: Inspect Auction Detail (Highest Bid)

```bash
echo "=== Auction Detail ==="
curl -s "$API_URL/api/v1/auctions/$AUCTION_ID" | jq '{
  id,
  title,
  status,
  currentPrice,
  bidCount,
  endTime
}'
```

**Expected output:**
```json
{
  "id": "3042d012-ac4a-4520-a622-875ef50f3d16",
  "title": "Vintage Rolex Submariner",
  "status": 1,
  "currentPrice": 7000.00,
  "bidCount": 20,
  "endTime": "2026-04-15T12:15:00Z"
}
```

(`endTime` may have moved past the original 10-minute window: anti-sniping extends it by 5
minutes whenever a bid lands within 5 minutes of the current end time.)

---

### Step 8: View Structured JSON Logs (Docker Only)

```bash
echo "=== Last 20 API Logs ==="
docker logs auction-kings-api --tail 20 2>&1 | grep -i "bid\|auction\|error" | tail -20

echo "=== Last 20 Worker Logs ==="
docker logs auction-kings-worker --tail 20 2>&1 | tail -20
```

Example log entries show:
- Bid placement commands with request IDs
- Auction queries
- Idempotency handling
- Correlation IDs for tracing

---

### Step 9: Verify Outbox Processor Health

```bash
curl -s "$API_URL/health/ready" | jq '.checks[] | select(.name == "outbox")'
```

**Expected output:**
```json
{
  "name": "outbox",
  "status": "Healthy",
  "durationMs": 1.9
}
```

---

### Step 10: Inspect Prometheus Metrics (Docker Only)

```bash
echo "=== Bids placed total ==="
curl -s "$API_URL/metrics" | grep "auction_kings_bids_placed_total"

```

**Expected output:**
```
# HELP auction_kings_bids_placed_total Total number of bids successfully placed.
# TYPE auction_kings_bids_placed_total counter
auction_kings_bids_placed_total 20
```

The outbox gauge (`auction_kings_outbox_pending_messages_count`) is set by `OutboxProcessor`, which
runs in the worker process. The worker exposes no `/metrics` endpoint, so the gauge does not appear
on the API's `/metrics`; use the `outbox` health check (Step 9) instead.

The Grafana dashboard at **http://localhost:3000** (user `admin`, password generated by `./scripts/setup-local-env.sh` in `docker/secrets/grafana_admin_password.txt`) shows these metrics live on the AuctionKings Overview dashboard.

---

## Cleanup

### Option A: Stop Docker Stack

```bash
cd docker
docker compose down

# Optional: remove volumes (clears PostgreSQL data, Redis data, Prometheus TSDB, Grafana state)
docker compose down -v
```

### Option B: Stop dotnet run

```bash
# Press Ctrl+C in each terminal running `dotnet run` (API and worker)
```

---

## Key Features Demonstrated

| Feature | How to See It |
|---------|---------------|
| **Token Generation** | Step 2 shows JWT token issuance |
| **Auction CQRS** | Steps 3 & 7 show command (create) and query (get detail) |
| **Authorization** | Step 4 demonstrates 401 without token |
| **Sequential Bids / Optimistic Concurrency** | Step 5 places 20 bids; see its note on parallel firing |
| **Idempotency** | Each bid has `X-Request-Id` header for replay protection |
| **Real-time Highest Bid** | Step 7 shows current highest bid updated |
| **Outbox Pattern** | Step 9 confirms outbox processor health |
| **Structured Logging** | Step 8 shows JSON logs with correlation IDs |
| **Prometheus Metrics** | Step 10 shows live counters and gauges |
| **Grafana Dashboard** | http://localhost:3000 — AuctionKings Overview |

---

## Troubleshooting

**No response from API:**
- Ensure Docker stack or dotnet run has completed startup (wait 20+ seconds)
- Check `curl "$API_URL/health/live"` returns 200

**Bid fails with `{"code":"Idempotency.MissingKey", ...}` or `Idempotency.InvalidKey`:**
- `POST .../bids` needs an `X-Request-Id` header holding a GUID. No other endpoint requires it.

**Errors like `relation "auctions" does not exist`:**
- The schema was not created. Check the API log for `N pending migration(s)`; if the API
  runs outside Development, set `Database__MigrateOnStartup=true` or migrate by hand (runbook §0).

**API refuses to start (`'Jwt:Secret' is not configured`) or Redis `NOAUTH`:**
- Run `./scripts/setup-local-env.sh` first, and for Option A pass
  `docker compose --env-file .env.local up -d` (not plain `docker compose up`).

**Bids rejected with 400 Bad Request:**
- The body is only `{"amount", "currency"}`; the bidder comes from the token
- Verify `currency` matches auction currency
- The seller cannot bid on their own auction, and the current winner cannot outbid themselves
- Check amount is >= the minimum increment over the current price (see `BidIncrement`)

**Bids rejected with 409 `Auction.ConcurrencyConflict` on requests you know were sequential:**
- Check the response body — if it's an RFC7807 `ProblemDetails` with `"title": "Duplicate
  Request"` instead, you reused an `X-Request-Id`. See `docs/runbook.md` for the full breakdown.

**Docker logs show connection errors:**
- Services may still be starting; wait 30 seconds and retry
- Verify all containers are running: `docker ps | grep auction`

---

## Extending the Demo

**Add more bidders:**
```bash
BIDDER_N="550e8400-e29b-41d4-a716-446655440099"
TOKEN_N=$(token "$BIDDER_N")   # helper from Step 2
```

**Test auction closure:**
Wait for the end time to see the auction close automatically — the `AuctionCloseWorker` polls every
10 seconds (background worker must be running). The shortest allowed duration is 5 minutes
(`durationInHours` ≥ 0.0834); shorter values are rejected with 400. Bids in the last 5 minutes
extend the end time by 5 minutes (anti-sniping), so stop bidding to let it close.

**Monitor real-time updates (SignalR):**
Connect to `ws://localhost:5081/hubs/auction` after creating an auction to receive live bid notifications. `scripts/signalr-test-client.html` is a ready-made browser client for this.

---

**Last updated:** 2026-11-14
**Tested with:** ASP.NET Core 10, PostgreSQL 16, Redis 7, Prometheus 2.52, Grafana 10.4
