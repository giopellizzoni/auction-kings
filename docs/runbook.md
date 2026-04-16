# AuctionKings Runbook

Operational reference for running, monitoring, and troubleshooting AuctionKings in local or staging environments.

---

## 1. Starting the Stack

```bash
# 1. Start infrastructure (PostgreSQL, Redis, LocalStack)
cd docker && docker-compose -f docker-compose.infra.yml up -d && cd ..

# 2. Start the API (port 5081)
dotnet run --project src/services/AuctionKings.API/AuctionKings.API.csproj

# 3. Start background workers (separate terminal)
dotnet run --project src/workers/AuctionKings.BackgroundWorkers/AuctionKings.BackgroundWorkers.csproj
```

Or with Docker Compose (all-in-one):

```bash
cd docker && docker-compose up
```

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

---

### High bid latency / concurrency conflicts

**Symptom:** `http_request_duration_seconds` p99 spikes. Logs show frequent concurrency conflict errors.

**Diagnosis:** Expected under high contention on a single auction. Optimistic concurrency retry adds latency.

**Mitigation:**

- Increase `Outbox:BatchSize` to process events faster.
- Scale the API horizontally (Redis backplane handles SignalR fan-out).
- Reduce bidding concurrency at the client level (exponential backoff on 409 Conflict).

---

### JWT token rejected (401 on bid)

**Steps:**

1. Generate a fresh Bidder token:

   ```bash
   curl -s -X POST http://localhost:5081/dev/token \
     -H "Content-Type: application/json" \
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
     -d '{"userId": "00000000-0000-0000-0000-000000000002", "role": "Admin"}' | jq .token
   ```
