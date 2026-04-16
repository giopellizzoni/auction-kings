# AuctionKings — Repeatable 5-Minute Demo Script

This demo showcases the AuctionKings real-time auction platform: creating auctions, placing concurrent bids, and inspecting results. All commands are copy-pasteable.

**Estimated time:** ~5 minutes  
**Requirements:** `curl`, `jq`, `bash`

---

## Setup Phase

### Option A: Full Docker Stack (Recommended)

```bash
# From the repo root — start all services (API, PostgreSQL, Redis, LocalStack, Moto, Prometheus, Grafana)
cd docker
docker compose -f docker-compose.yml up -d

# Wait ~15 seconds for services to be healthy
sleep 15

# Export base URL for demo
export API_URL="http://localhost:5081"
```

### Option B: Local Development (dotnet run)

```bash
# Start infrastructure first
cd docker && docker compose -f docker-compose.infra.yml up -d && cd ..

# In a separate terminal, run the API
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# Wait for startup (~20 seconds), then in your main terminal:
export API_URL="http://localhost:5081"
```

---

## Demo: Step-by-Step

### Step 1: Verify Health Endpoints

```bash
# Check liveness
curl -s "$API_URL/health/live" | jq .

# Check readiness (DB + Redis + Outbox)
curl -s "$API_URL/health/ready" | jq .
```

**Expected output:**
```json
{
  "status": "Healthy",
  "checks": { ... }
}
```

---

### Step 2: Generate Bidder Tokens (3 fixed UUIDs for repeatability)

```bash
# Bidder 1
BIDDER_1="550e8400-e29b-41d4-a716-446655440001"
TOKEN_1=$(curl -s -X POST "$API_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d "{\"userId\": \"$BIDDER_1\", \"role\": \"Bidder\"}" | jq -r '.token')

echo "Token 1: $TOKEN_1"

# Bidder 2
BIDDER_2="550e8400-e29b-41d4-a716-446655440002"
TOKEN_2=$(curl -s -X POST "$API_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d "{\"userId\": \"$BIDDER_2\", \"role\": \"Bidder\"}" | jq -r '.token')

echo "Token 2: $TOKEN_2"

# Bidder 3
BIDDER_3="550e8400-e29b-41d4-a716-446655440003"
TOKEN_3=$(curl -s -X POST "$API_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d "{\"userId\": \"$BIDDER_3\", \"role\": \"Bidder\"}" | jq -r '.token')

echo "Token 3: $TOKEN_3"
```

---

### Step 3: Create an Auction (10-minute duration for quick demo)

```bash
SELLER_ID="550e8400-e29b-41d4-a716-446655440000"
AUCTION_RESPONSE=$(curl -s -X POST "$API_URL/api/v1/auctions" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Vintage Rolex Submariner",
    "description": "Classic 1960s timepiece in excellent condition.",
    "startingPrice": 5000.00,
    "currency": "USD",
    "durationInHours": 0.166,
    "sellerId": "'$SELLER_ID'"
  }')

echo "$AUCTION_RESPONSE" | jq .
AUCTION_ID=$(echo "$AUCTION_RESPONSE" | jq -r '.id')
echo "Auction ID: $AUCTION_ID"
```

**Expected output:**
```json
{
  "id": "<uuid>",
  "title": "Vintage Rolex Submariner",
  "description": "Classic 1960s timepiece in excellent condition.",
  "startingPrice": 5000.00,
  "currentHighestBid": null,
  "status": "Active",
  "currency": "USD",
  "endsAt": "2026-04-15T...",
  "bidsCount": 0
}
```

---

### Step 4: Demonstrate 401 Without Token

Try placing a bid without authorization header:

```bash
curl -s -X POST "$API_URL/api/v1/auctions/$AUCTION_ID/bids" \
  -H "Content-Type: application/json" \
  -H "X-Request-Id: test-no-auth-001" \
  -d '{
    "bidderId": "'$BIDDER_1'",
    "amount": 5100.00,
    "currency": "USD"
  }' | jq .
```

**Expected output:**
```json
{
  "statusCode": 401,
  "message": "Unauthorized",
  "details": "Missing or invalid authorization token"
}
```

---

### Step 5: Place 20 Concurrent Bids (3 bidders in parallel)

This loop places 20 bids concurrently, cycling through the 3 bidders with incrementing amounts.

```bash
# Generate 20 bids in parallel — all fire simultaneously in the background
for i in {1..20}; do
  MOD=$((i % 3))
  if   [ $MOD -eq 1 ]; then BIDDER_ID="$BIDDER_1"; TOKEN="$TOKEN_1"
  elif [ $MOD -eq 2 ]; then BIDDER_ID="$BIDDER_2"; TOKEN="$TOKEN_2"
  else                       BIDDER_ID="$BIDDER_3"; TOKEN="$TOKEN_3"
  fi

  BID_AMOUNT=$((5000 + i * 50))
  REQUEST_ID="bid-$(printf '%03d' $i)"

  (
    curl -s -X POST "$API_URL/api/v1/auctions/$AUCTION_ID/bids" \
      -H "Content-Type: application/json" \
      -H "Authorization: Bearer $TOKEN" \
      -H "X-Request-Id: $REQUEST_ID" \
      -d "{\"bidderId\": \"$BIDDER_ID\", \"amount\": $BID_AMOUNT, \"currency\": \"USD\"}" \
      | jq -c '{id, amount: .amount}'
  ) &
done
wait
echo "All 20 bids fired."
```

**Expected output:** 20 JSON lines, each with an `id` and `amount`, then `All 20 bids fired.`

---

### Step 6: Inspect Bids

List all bids placed on the auction:

```bash
echo "=== All Bids on Auction ==="
curl -s "$API_URL/api/v1/auctions/$AUCTION_ID/bids" | \
  jq '.[] | {id: .id, bidderId: .bidderId, amount: .amount, placedAt: .placedAt}' | \
  head -50

echo ""
echo "=== Total Bids Count ==="
curl -s "$API_URL/api/v1/auctions/$AUCTION_ID/bids" | jq 'length'
```

---

### Step 7: Inspect Auction Detail (Highest Bid)

```bash
echo "=== Auction Detail ==="
curl -s "$API_URL/api/v1/auctions/$AUCTION_ID" | jq '{
  id: .id,
  title: .title,
  status: .status,
  startingPrice: .startingPrice,
  currentHighestBid: .currentHighestBid,
  bidsCount: .bidsCount,
  endsAt: .endsAt
}'
```

**Expected output:**
```json
{
  "id": "<uuid>",
  "title": "Vintage Rolex Submariner",
  "status": "Active",
  "startingPrice": 5000.00,
  "currentHighestBid": 6000.00,
  "bidsCount": 20,
  "endsAt": "2026-04-15T..."
}
```

---

### Step 8: View Structured JSON Logs (Docker Only)

```bash
echo "=== Last 20 API Logs ==="
docker logs auction-kings-api --tail 20 2>&1 | grep -i "bid\|auction\|error" | tail -20
```

Example log entries show:
- Bid placement commands with request IDs
- Auction queries
- Idempotency handling
- Correlation IDs for tracing

---

### Step 9: Verify Outbox Processor Health

```bash
curl -s "$API_URL/health/ready" | jq '.checks.outbox'
```

**Expected output:**
```json
{
  "status": "Healthy",
  "description": "Outbox processor is healthy and up to date"
}
```

---

### Step 10: Inspect Prometheus Metrics (Docker Only)

```bash
echo "=== Bids placed total ==="
curl -s "$API_URL/metrics" | grep "auction_kings_bids_placed_total"

echo ""
echo "=== Outbox pending ==="
curl -s "$API_URL/metrics" | grep "auction_kings_outbox_pending_messages_count"
```

**Expected output:**
```
# HELP auction_kings_bids_placed_total Total number of bids successfully placed.
# TYPE auction_kings_bids_placed_total counter
auction_kings_bids_placed_total 20

# HELP auction_kings_outbox_pending_messages_count ...
# TYPE auction_kings_outbox_pending_messages_count gauge
auction_kings_outbox_pending_messages_count 0
```

The Grafana dashboard at **http://localhost:3000** (admin / admin) shows these metrics live on the AuctionKings Overview dashboard.

---

## Cleanup

### Option A: Stop Docker Stack

```bash
cd docker
docker compose -f docker-compose.yml down

# Optional: remove volumes (clears PostgreSQL data, Redis data, Prometheus TSDB, Grafana state)
docker compose -f docker-compose.yml down -v
```

### Option B: Stop dotnet run

```bash
# Press Ctrl+C in the terminal where dotnet run is executing
```

---

## Key Features Demonstrated

| Feature | How to See It |
|---------|---------------|
| **Token Generation** | Step 2 shows JWT token issuance |
| **Auction CQRS** | Steps 3 & 7 show command (create) and query (get detail) |
| **Authorization** | Step 4 demonstrates 401 without token |
| **Concurrent Bids** | Step 5 places 20 bids in parallel |
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

**Token generation fails:**
- Verify `$API_URL` is set: `echo $API_URL`
- Check `/dev/token` endpoint is exposed (development mode only)

**Bids rejected with 400 Bad Request:**
- Ensure `bidderId` and `sellerId` are valid UUIDs
- Verify `currency` matches auction currency
- Check amount is > 0

**Docker logs show connection errors:**
- Services may still be starting; wait 30 seconds and retry
- Verify all containers are running: `docker ps | grep auction`

---

## Extending the Demo

**Add more bidders:**
```bash
BIDDER_N="550e8400-e29b-41d4-a716-446655440099"
TOKEN_N=$(curl -s -X POST "$API_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d "{\"userId\": \"$BIDDER_N\", \"role\": \"Bidder\"}" | jq -r '.token')
```

**Test auction closure:**
Wait 10 minutes (or modify duration to 0.001 hours = 3.6 seconds) to see auction close automatically.

**Monitor real-time updates (SignalR):**
Connect to `ws://localhost:5081/hubs/auction` after creating auction to receive live bid notifications.

---

**Last updated:** 2026-04-16  
**Tested with:** ASP.NET Core 10, PostgreSQL 16, Redis 7, Prometheus 2.52, Grafana 10.4
