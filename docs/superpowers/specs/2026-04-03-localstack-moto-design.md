# LocalStack + Moto Local AWS Emulation — Design Spec

**Date:** 2026-04-03
**Status:** Approved
**Approach:** LocalStack Community (DynamoDB, SQS, SNS, S3, Secrets Manager, CloudWatch) + Moto (Cognito, AppConfig, EventBridge, X-Ray)

## Goal

Enable local testing of all AWS services referenced in `architecture/ARCHITECTURE.md` without a Pro license, using two emulator containers alongside the existing PostgreSQL + Redis infrastructure.

## Container Topology

| Container    | Image                          | Services Emulated                                        | Port |
| ------------ | ------------------------------ | -------------------------------------------------------- | ---- |
| `localstack` | `localstack/localstack:latest` | DynamoDB, SQS, SNS, S3, Secrets Manager, CloudWatch Logs | 4566 |
| `moto`       | `motoserver/moto:latest`       | Cognito, AppConfig, EventBridge, X-Ray                   | 5555 |

Both containers join the existing `backend` network in `docker-compose.infra.yml`. Existing `postgres` and `redis` services are unchanged.

## LocalStack Resources (Init Script)

Provisioned via `docker/localstack-init/ready.d/init-aws.sh` (mounted to `/etc/localstack/init/ready.d/`).

### DynamoDB

**AuctionEvents table:**

- Partition Key: `StreamId` (S)
- Sort Key: `Version` (N)
- GSI: `EventTypeTimestampIndex` — PK: `EventType` (S), SK: `Timestamp` (N), Projection: ALL
- Streams: Enabled (NEW_IMAGE)
- Billing: PAY_PER_REQUEST

**AuctionSnapshots table:**

- Partition Key: `StreamId` (S)
- Sort Key: `Version` (N)
- TTL: Enabled on `ExpiresAt`
- Billing: PAY_PER_REQUEST

### SQS

- `auction-bids-queue` — main bid processing queue, redrive policy to DLQ (maxReceiveCount: 3)
- `auction-bids-dlq` — dead letter queue

### SNS

- `auction-notifications` — general notification topic
- `auction-winner-notifications` — winner-specific notifications
- `auction-seller-notifications` — seller-specific notifications

### S3

- `auctionkings-assets` — auction images and documents

### Secrets Manager

- `AuctionKings/Database/ConnectionString` — PostgreSQL connection string for local dev
- `AuctionKings/Redis/ConnectionString` — Redis connection string for local dev
- `AuctionKings/Cognito/UserPoolId` — populated by Moto init script
- `AuctionKings/Cognito/AppClientId` — populated by Moto init script

### CloudWatch Logs

- `/auctionkings/api` — API log group
- `/auctionkings/worker` — worker log group

## Moto Resources (Init Script)

Provisioned via `docker/moto-init.sh`, executed by a one-shot init container (`moto-init`) that waits for Moto health, then provisions resources.

### Cognito

**User Pool:** `AuctionKingsDevPool`

- Required attributes: `email`
- Custom attributes: `custom:reputation`, `custom:is_verified`, `custom:total_bids`
- App Client: `AuctionKingsDevClient` (no secret, flows: `ALLOW_USER_PASSWORD_AUTH`, `ALLOW_REFRESH_TOKEN_AUTH`)
- Groups: `Sellers`, `Admins`, `Buyers`
- Test users:
  - `admin@test.com` / `Test1234!` — member of `Admins`
  - `seller@test.com` / `Test1234!` — member of `Sellers`
  - `buyer@test.com` / `Test1234!` — member of `Buyers`

**Dynamic ID capture:** The init script captures `UserPoolId` and `AppClientId` from Moto output and writes them to:

- `docker/secrets/cognito_user_pool_id.txt`
- `docker/secrets/cognito_app_client_id.txt`

These are also pushed into LocalStack Secrets Manager under `AuctionKings/Cognito/*`.

### AppConfig

- Application: `AuctionKings`
- Environment: `Development`
- Configuration Profile: `FeatureFlags`
- Initial flags:
  - `enable_event_sourcing: true`
  - `enable_sns_notifications: true`
  - `high_value_bid_threshold: 10000`

### EventBridge

- Event Bus: `auction-events`
- Rule: `auction-domain-events` — pattern: `{"source": ["auctionkings"]}`, target: SQS `auction-bids-queue` on LocalStack (`http://localstack:4566`)

### X-Ray

No init needed — Moto accepts trace segments on the standard X-Ray API endpoint.

## Configuration Integration

### appsettings.LocalStack.json

New environment-specific settings file loaded when `ASPNETCORE_ENVIRONMENT=LocalStack`:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566",
    "Cognito": {
      "Region": "us-east-1",
      "UserPoolId": "RUNTIME: read from /run/secrets/cognito_user_pool_id",
      "AppClientId": "RUNTIME: read from /run/secrets/cognito_app_client_id",
      "ServiceURL": "http://localhost:5555"
    },
    "SQS": {
      "BidQueueUrl": "http://localhost:4566/000000000000/auction-bids-queue",
      "ServiceURL": "http://localhost:4566"
    },
    "DynamoDB": { "ServiceURL": "http://localhost:4566" },
    "S3": {
      "BucketName": "auctionkings-assets",
      "ServiceURL": "http://localhost:4566"
    },
    "SNS": { "ServiceURL": "http://localhost:4566" },
    "EventBridge": { "ServiceURL": "http://localhost:5555" },
    "AppConfig": { "ServiceURL": "http://localhost:5555" },
    "XRay": { "ServiceURL": "http://localhost:5555" },
    "SecretsManager": { "ServiceURL": "http://localhost:4566" },
    "CloudWatch": { "ServiceURL": "http://localhost:4566" }
  }
}
```

Cognito `UserPoolId` and `AppClientId` are read at runtime from `/run/secrets/` via `AddKeyPerFile` (same pattern as existing PostgreSQL/Redis secrets).

### Docker Compose Changes

Added to `docker-compose.infra.yml`:

- `localstack` service with health check on `_localstack/health`
- `moto` service with health check on `/moto-api/`
- `moto-init` one-shot container (`restart: "no"`) that depends on both `moto` (healthy) and `localstack` (healthy), runs the init script, then exits
- Init script volumes mounted appropriately
- Secret files for Cognito IDs added to secrets block

### NuGet Packages

Added to `Directory.Packages.props` (versions to be pinned at latest stable):

- `AWSSDK.DynamoDBv2`
- `AWSSDK.SQS`
- `AWSSDK.SimpleNotificationService`
- `AWSSDK.S3`
- `AWSSDK.CognitoIdentityProvider`
- `AWSSDK.SecretsManager`
- `AWSSDK.AppConfig`
- `AWSSDK.CloudWatch`
- `AWSSDK.XRay`
- `AWSSDK.EventBridge`
- `Amazon.Extensions.Configuration.SecretsManager`

## File Layout

```
docker/
├── docker-compose.infra.yml          # Updated: +localstack, +moto, +moto-init
├── localstack-init/
│   └── ready.d/
│       └── init-aws.sh               # New: LocalStack resource provisioning
├── moto-init.sh                      # New: Moto resource provisioning
├── secrets/
│   ├── postgres_password.txt          # Existing
│   ├── redis_password.txt             # Existing
│   ├── cognito_user_pool_id.txt       # New: written by moto-init
│   └── cognito_app_client_id.txt      # New: written by moto-init
src/services/AuctionKings.API/
├── appsettings.LocalStack.json        # New: AWS endpoint overrides
```

## Scope Boundaries

**In scope:**

- Docker infrastructure (containers, init scripts, health checks, networking)
- `appsettings.LocalStack.json` configuration file
- NuGet package declarations in `Directory.Packages.props`

**Out of scope (future work):**

- C# service implementations (`DynamoDbEventStore.cs`, `SqsMessagePublisher.cs`, etc.)
- Modifying `Program.cs` or `DependencyInjection.cs` to wire up AWS services
- Integration tests against LocalStack
- CI/CD pipeline changes

The goal is to have the infrastructure ready so that AWS service implementations can be developed and tested against it immediately.

