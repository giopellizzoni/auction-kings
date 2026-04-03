# LocalStack + Moto Local AWS Emulation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LocalStack and Moto containers to the local Docker infrastructure so all AWS services from the architecture can be tested locally.

**Architecture:** LocalStack Community emulates DynamoDB, SQS, SNS, S3, Secrets Manager, and CloudWatch Logs. Moto emulates Cognito, AppConfig, EventBridge, and X-Ray. Both sit alongside existing PostgreSQL and Redis containers. Init scripts auto-provision all resources on `docker compose up`.

**Tech Stack:** Docker Compose, LocalStack, Moto, AWS CLI (in init scripts), ASP.NET Core configuration, AWSSDK NuGet packages

**Spec:** `docs/superpowers/specs/2026-04-03-localstack-moto-design.md`

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Modify | `docker/docker-compose.infra.yml` | Add localstack, moto, moto-init services |
| Create | `docker/localstack-init/ready.d/init-aws.sh` | Provision DynamoDB, SQS, SNS, S3, Secrets Manager, CloudWatch on LocalStack |
| Create | `docker/moto-init.sh` | Provision Cognito, AppConfig, EventBridge on Moto; write secret files; push Cognito IDs to LocalStack Secrets Manager |
| Create | `src/services/AuctionKings.API/appsettings.LocalStack.json` | AWS endpoint overrides for local dev |
| Modify | `Directory.Packages.props` | Add AWS SDK NuGet package versions |
| Modify | `.gitignore` | Ensure cognito secret files are ignored |

---

### Task 1: Create the LocalStack init script

**Files:**
- Create: `docker/localstack-init/ready.d/init-aws.sh`

- [ ] **Step 1: Create the init directory**

```bash
mkdir -p docker/localstack-init/ready.d
```

- [ ] **Step 2: Write the init script**

Create `docker/localstack-init/ready.d/init-aws.sh`:

```bash
#!/bin/bash
set -euo pipefail

ENDPOINT="http://localhost:4566"
REGION="us-east-1"

echo "=== Provisioning LocalStack resources ==="

# -----------------------------------------------
# DynamoDB
# -----------------------------------------------
echo "Creating DynamoDB table: AuctionEvents..."
awslocal dynamodb create-table \
  --table-name AuctionEvents \
  --attribute-definitions \
    AttributeName=StreamId,AttributeType=S \
    AttributeName=Version,AttributeType=N \
    AttributeName=EventType,AttributeType=S \
    AttributeName=Timestamp,AttributeType=N \
  --key-schema \
    AttributeName=StreamId,KeyType=HASH \
    AttributeName=Version,KeyType=RANGE \
  --global-secondary-indexes '[
    {
      "IndexName": "EventTypeTimestampIndex",
      "KeySchema": [
        {"AttributeName": "EventType", "KeyType": "HASH"},
        {"AttributeName": "Timestamp", "KeyType": "RANGE"}
      ],
      "Projection": {"ProjectionType": "ALL"}
    }
  ]' \
  --billing-mode PAY_PER_REQUEST \
  --stream-specification StreamEnabled=true,StreamViewType=NEW_IMAGE \
  --region "$REGION"

echo "Creating DynamoDB table: AuctionSnapshots..."
awslocal dynamodb create-table \
  --table-name AuctionSnapshots \
  --attribute-definitions \
    AttributeName=StreamId,AttributeType=S \
    AttributeName=Version,AttributeType=N \
  --key-schema \
    AttributeName=StreamId,KeyType=HASH \
    AttributeName=Version,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region "$REGION"

awslocal dynamodb update-time-to-live \
  --table-name AuctionSnapshots \
  --time-to-live-specification "Enabled=true,AttributeName=ExpiresAt" \
  --region "$REGION"

# -----------------------------------------------
# SQS
# -----------------------------------------------
echo "Creating SQS queues..."
awslocal sqs create-queue \
  --queue-name auction-bids-dlq \
  --region "$REGION"

DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "http://localhost:4566/000000000000/auction-bids-dlq" \
  --attribute-names QueueArn \
  --query "Attributes.QueueArn" \
  --output text \
  --region "$REGION")

awslocal sqs create-queue \
  --queue-name auction-bids-queue \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":\\\"3\\\"}\"}" \
  --region "$REGION"

# -----------------------------------------------
# SNS
# -----------------------------------------------
echo "Creating SNS topics..."
awslocal sns create-topic --name auction-notifications --region "$REGION"
awslocal sns create-topic --name auction-winner-notifications --region "$REGION"
awslocal sns create-topic --name auction-seller-notifications --region "$REGION"

# -----------------------------------------------
# S3
# -----------------------------------------------
echo "Creating S3 bucket..."
awslocal s3 mb s3://auctionkings-assets --region "$REGION"

# -----------------------------------------------
# Secrets Manager (partial — Cognito IDs added by moto-init)
# -----------------------------------------------
echo "Creating Secrets Manager entries..."
awslocal secretsmanager create-secret \
  --name "AuctionKings/Database/ConnectionString" \
  --secret-string "Host=postgres;Port=5432;Database=AuctionKingsDB;Username=dev;Password=$(cat /run/secrets/postgres_password 2>/dev/null || echo 'changeme')" \
  --region "$REGION"

awslocal secretsmanager create-secret \
  --name "AuctionKings/Redis/ConnectionString" \
  --secret-string "redis:6379,password=$(cat /run/secrets/redis_password 2>/dev/null || echo 'changeme')" \
  --region "$REGION"

# -----------------------------------------------
# CloudWatch Logs
# -----------------------------------------------
echo "Creating CloudWatch Log groups..."
awslocal logs create-log-group --log-group-name /auctionkings/api --region "$REGION"
awslocal logs create-log-group --log-group-name /auctionkings/worker --region "$REGION"

echo "=== LocalStack provisioning complete ==="
```

- [ ] **Step 3: Make the script executable**

```bash
chmod +x docker/localstack-init/ready.d/init-aws.sh
```

- [ ] **Step 4: Commit**

```bash
git add docker/localstack-init/ready.d/init-aws.sh
git commit -m "feat(devops): add LocalStack init script for DynamoDB, SQS, SNS, S3, Secrets Manager, CloudWatch"
```

---

### Task 2: Create the Moto init script

**Files:**
- Create: `docker/moto-init.sh`

- [ ] **Step 1: Write the Moto init script**

Create `docker/moto-init.sh`:

```bash
#!/bin/bash
set -euo pipefail

MOTO_ENDPOINT="http://moto:5555"
LOCALSTACK_ENDPOINT="http://localstack:4566"
REGION="us-east-1"
SECRETS_DIR="/secrets"

export AWS_ACCESS_KEY_ID="testing"
export AWS_SECRET_ACCESS_KEY="testing"
export AWS_DEFAULT_REGION="$REGION"

echo "=== Waiting for Moto to be ready ==="
until curl -sf "$MOTO_ENDPOINT/moto-api/" > /dev/null 2>&1; do
  echo "Moto not ready, retrying in 2s..."
  sleep 2
done

echo "=== Waiting for LocalStack to be ready ==="
until curl -sf "$LOCALSTACK_ENDPOINT/_localstack/health" > /dev/null 2>&1; do
  echo "LocalStack not ready, retrying in 2s..."
  sleep 2
done

echo "=== Provisioning Moto resources ==="

# -----------------------------------------------
# Cognito
# -----------------------------------------------
echo "Creating Cognito User Pool..."
CREATE_POOL_OUTPUT=$(aws cognito-idp create-user-pool \
  --pool-name AuctionKingsDevPool \
  --auto-verified-attributes email \
  --schema \
    Name=email,Required=true,Mutable=true \
    Name=custom:reputation,AttributeDataType=Number,Mutable=true \
    Name=custom:is_verified,AttributeDataType=String,Mutable=true \
    Name=custom:total_bids,AttributeDataType=Number,Mutable=true \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

USER_POOL_ID=$(echo "$CREATE_POOL_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['UserPool']['Id'])")
echo "User Pool ID: $USER_POOL_ID"

echo "Creating App Client..."
CREATE_CLIENT_OUTPUT=$(aws cognito-idp create-user-pool-client \
  --user-pool-id "$USER_POOL_ID" \
  --client-name AuctionKingsDevClient \
  --no-generate-secret \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

APP_CLIENT_ID=$(echo "$CREATE_CLIENT_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['UserPoolClient']['ClientId'])")
echo "App Client ID: $APP_CLIENT_ID"

# Write Cognito IDs to secret files
echo -n "$USER_POOL_ID" > "$SECRETS_DIR/cognito_user_pool_id.txt"
echo -n "$APP_CLIENT_ID" > "$SECRETS_DIR/cognito_app_client_id.txt"

# Push Cognito IDs to LocalStack Secrets Manager
aws secretsmanager create-secret \
  --name "AuctionKings/Cognito/UserPoolId" \
  --secret-string "$USER_POOL_ID" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$REGION"

aws secretsmanager create-secret \
  --name "AuctionKings/Cognito/AppClientId" \
  --secret-string "$APP_CLIENT_ID" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$REGION"

# Create groups
echo "Creating Cognito groups..."
for GROUP in Sellers Admins Buyers; do
  aws cognito-idp create-group \
    --user-pool-id "$USER_POOL_ID" \
    --group-name "$GROUP" \
    --endpoint-url "$MOTO_ENDPOINT" \
    --region "$REGION"
done

# Create test users
echo "Creating test users..."
declare -A USERS=(
  ["admin@test.com"]="Admins"
  ["seller@test.com"]="Sellers"
  ["buyer@test.com"]="Buyers"
)

for EMAIL in "${!USERS[@]}"; do
  GROUP="${USERS[$EMAIL]}"

  aws cognito-idp admin-create-user \
    --user-pool-id "$USER_POOL_ID" \
    --username "$EMAIL" \
    --user-attributes Name=email,Value="$EMAIL" Name=email_verified,Value=true \
    --temporary-password "Test1234!" \
    --endpoint-url "$MOTO_ENDPOINT" \
    --region "$REGION"

  aws cognito-idp admin-set-user-password \
    --user-pool-id "$USER_POOL_ID" \
    --username "$EMAIL" \
    --password "Test1234!" \
    --permanent \
    --endpoint-url "$MOTO_ENDPOINT" \
    --region "$REGION"

  aws cognito-idp admin-add-user-to-group \
    --user-pool-id "$USER_POOL_ID" \
    --username "$EMAIL" \
    --group-name "$GROUP" \
    --endpoint-url "$MOTO_ENDPOINT" \
    --region "$REGION"

  echo "  Created user $EMAIL in group $GROUP"
done

# -----------------------------------------------
# AppConfig
# -----------------------------------------------
echo "Creating AppConfig resources..."
APP_OUTPUT=$(aws appconfig create-application \
  --name AuctionKings \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

APP_ID=$(echo "$APP_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['Id'])")

ENV_OUTPUT=$(aws appconfig create-environment \
  --application-id "$APP_ID" \
  --name Development \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

ENV_ID=$(echo "$ENV_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['Id'])")

PROFILE_OUTPUT=$(aws appconfig create-configuration-profile \
  --application-id "$APP_ID" \
  --name FeatureFlags \
  --location-uri hosted \
  --type AWS.AppConfig.FeatureFlags \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

PROFILE_ID=$(echo "$PROFILE_OUTPUT" | python3 -c "import sys,json; print(json.load(sys.stdin)['Id'])")

FEATURE_FLAGS=$(cat <<'FLAGJSON'
{
  "version": "1",
  "flags": {
    "enable_event_sourcing": {
      "name": "enable_event_sourcing",
      "attributes": {}
    },
    "enable_sns_notifications": {
      "name": "enable_sns_notifications",
      "attributes": {}
    },
    "high_value_bid_threshold": {
      "name": "high_value_bid_threshold",
      "attributes": {
        "threshold": { "constraints": { "type": "number" } }
      }
    }
  },
  "values": {
    "enable_event_sourcing": { "enabled": true },
    "enable_sns_notifications": { "enabled": true },
    "high_value_bid_threshold": {
      "enabled": true,
      "threshold": 10000
    }
  }
}
FLAGJSON
)

aws appconfig create-hosted-configuration-version \
  --application-id "$APP_ID" \
  --configuration-profile-id "$PROFILE_ID" \
  --content-type "application/json" \
  --content "$FEATURE_FLAGS" \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION"

echo "AppConfig provisioned (App=$APP_ID, Env=$ENV_ID, Profile=$PROFILE_ID)"

# -----------------------------------------------
# EventBridge
# -----------------------------------------------
echo "Creating EventBridge resources..."
aws events create-event-bus \
  --name auction-events \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION"

QUEUE_ARN="arn:aws:sqs:${REGION}:000000000000:auction-bids-queue"

aws events put-rule \
  --name auction-domain-events \
  --event-bus-name auction-events \
  --event-pattern '{"source":["auctionkings"]}' \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION"

aws events put-targets \
  --rule auction-domain-events \
  --event-bus-name auction-events \
  --targets "Id=sqs-target,Arn=$QUEUE_ARN" \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION"

echo "=== Moto provisioning complete ==="
echo "Cognito User Pool ID: $USER_POOL_ID"
echo "Cognito App Client ID: $APP_CLIENT_ID"
```

- [ ] **Step 2: Make the script executable**

```bash
chmod +x docker/moto-init.sh
```

- [ ] **Step 3: Commit**

```bash
git add docker/moto-init.sh
git commit -m "feat(devops): add Moto init script for Cognito, AppConfig, EventBridge"
```

---

### Task 3: Update docker-compose.infra.yml

**Files:**
- Modify: `docker/docker-compose.infra.yml`

- [ ] **Step 1: Add localstack, moto, and moto-init services**

Append the following three services to the `services:` block in `docker/docker-compose.infra.yml`, before the `networks:` block:

```yaml
  localstack:
    image: localstack/localstack:latest
    container_name: infra-localstack
    restart: unless-stopped
    environment:
      SERVICES: dynamodb,sqs,sns,s3,secretsmanager,logs
      DEFAULT_REGION: us-east-1
      DEBUG: 0
    volumes:
      - localstack_data:/var/lib/localstack
      - ./localstack-init/ready.d:/etc/localstack/init/ready.d:ro
    secrets:
      - postgres_password
      - redis_password
    networks:
      - backend
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:4566/_localstack/health | grep -q '\"dynamodb\": \"available\"'"]
      interval: 5s
      timeout: 5s
      retries: 30
    ports:
      - "127.0.0.1:4566:4566"

  moto:
    image: motoserver/moto:latest
    container_name: infra-moto
    restart: unless-stopped
    networks:
      - backend
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:5555/moto-api/"]
      interval: 5s
      timeout: 3s
      retries: 20
    ports:
      - "127.0.0.1:5555:5555"

  moto-init:
    image: amazon/aws-cli:latest
    container_name: infra-moto-init
    restart: "no"
    depends_on:
      moto:
        condition: service_healthy
      localstack:
        condition: service_healthy
    environment:
      AWS_ACCESS_KEY_ID: testing
      AWS_SECRET_ACCESS_KEY: testing
      AWS_DEFAULT_REGION: us-east-1
    entrypoint: ["/bin/bash", "/scripts/moto-init.sh"]
    volumes:
      - ./moto-init.sh:/scripts/moto-init.sh:ro
      - ./secrets:/secrets
    networks:
      - backend
```

- [ ] **Step 2: Add the `localstack_data` volume**

Add to the `volumes:` block:

```yaml
  localstack_data:
```

- [ ] **Step 3: Verify the complete file is valid YAML**

```bash
cd docker && docker compose -f docker-compose.infra.yml config --quiet
```

Expected: no output (valid YAML).

- [ ] **Step 4: Commit**

```bash
git add docker/docker-compose.infra.yml
git commit -m "feat(devops): add LocalStack, Moto, and Moto-init services to docker-compose.infra.yml"
```

---

### Task 4: Add .gitignore entries for generated secret files

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Add cognito secret files to .gitignore**

Append to the end of `.gitignore`:

```gitignore
# Generated Cognito secret files (written by moto-init container)
docker/secrets/cognito_user_pool_id.txt
docker/secrets/cognito_app_client_id.txt
```

- [ ] **Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: ignore generated Cognito secret files"
```

---

### Task 5: Create appsettings.LocalStack.json

**Files:**
- Create: `src/services/AuctionKings.API/appsettings.LocalStack.json`

- [ ] **Step 1: Write the configuration file**

Create `src/services/AuctionKings.API/appsettings.LocalStack.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AuctionKingsDB;Username=dev;Password=",
    "Redis": "localhost:6379"
  },
  "AWS": {
    "Region": "us-east-1",
    "Cognito": {
      "Region": "us-east-1",
      "ServiceURL": "http://localhost:5555"
    },
    "SQS": {
      "BidQueueUrl": "http://localhost:4566/000000000000/auction-bids-queue",
      "ServiceURL": "http://localhost:4566"
    },
    "DynamoDB": {
      "ServiceURL": "http://localhost:4566"
    },
    "S3": {
      "BucketName": "auctionkings-assets",
      "ServiceURL": "http://localhost:4566"
    },
    "SNS": {
      "ServiceURL": "http://localhost:4566"
    },
    "EventBridge": {
      "ServiceURL": "http://localhost:5555"
    },
    "AppConfig": {
      "ServiceURL": "http://localhost:5555"
    },
    "XRay": {
      "ServiceURL": "http://localhost:5555"
    },
    "SecretsManager": {
      "ServiceURL": "http://localhost:4566"
    },
    "CloudWatch": {
      "ServiceURL": "http://localhost:4566"
    }
  },
  "Cache": {
    "ListingsTtl": "00:00:30",
    "DetailsTtl": "00:01:00",
    "CountersTtl": "00:00:05"
  },
  "RateLimit": {
    "MaxRequests": 10,
    "Window": "00:00:10"
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/services/AuctionKings.API/appsettings.LocalStack.json
git commit -m "feat(devops): add appsettings.LocalStack.json with AWS endpoint overrides"
```

---

### Task 6: Add AWS SDK NuGet packages to Directory.Packages.props

**Files:**
- Modify: `Directory.Packages.props`

- [ ] **Step 1: Add a new ItemGroup for AWS packages**

Add the following `<ItemGroup>` block after the existing `Infrastructure` ItemGroup in `Directory.Packages.props`:

```xml
  <ItemGroup Label="AWS">
    <PackageVersion Include="AWSSDK.DynamoDBv2" Version="4.0.17.5" />
    <PackageVersion Include="AWSSDK.SQS" Version="4.0.2.22" />
    <PackageVersion Include="AWSSDK.SimpleNotificationService" Version="4.0.2.24" />
    <PackageVersion Include="AWSSDK.S3" Version="4.0.20.2" />
    <PackageVersion Include="AWSSDK.CognitoIdentityProvider" Version="4.0.6.7" />
    <PackageVersion Include="AWSSDK.SecretsManager" Version="4.0.4.14" />
    <PackageVersion Include="AWSSDK.AppConfig" Version="4.0.3.21" />
    <PackageVersion Include="AWSSDK.CloudWatch" Version="4.0.9" />
    <PackageVersion Include="AWSSDK.CloudWatchLogs" Version="4.0.18" />
    <PackageVersion Include="AWSSDK.XRay" Version="4.0.3.21" />
    <PackageVersion Include="AWSSDK.EventBridge" Version="4.0.5.23" />
    <PackageVersion Include="Kralizek.Extensions.Configuration.AWSSecretsManager" Version="1.7.0" />
  </ItemGroup>
```

- [ ] **Step 2: Verify the solution still builds**

```bash
dotnet restore && dotnet build
```

Expected: Build succeeded (package versions are declared centrally but not yet referenced by any `.csproj`).

- [ ] **Step 3: Commit**

```bash
git add Directory.Packages.props
git commit -m "feat(devops): add AWS SDK NuGet package versions to Directory.Packages.props"
```

---

### Task 7: Smoke test — bring up the full stack

- [ ] **Step 1: Start all infrastructure containers**

```bash
cd docker && docker compose -f docker-compose.infra.yml up -d
```

Expected: All containers start (postgres, redis, localstack, moto, moto-init).

- [ ] **Step 2: Wait for health checks and verify container status**

```bash
docker compose -f docker-compose.infra.yml ps
```

Expected: `postgres`, `redis`, `localstack`, `moto` show `healthy`. `moto-init` shows `Exited (0)`.

- [ ] **Step 3: Verify LocalStack resources were created**

```bash
# DynamoDB tables
aws --endpoint-url=http://localhost:4566 dynamodb list-tables --region us-east-1

# SQS queues
aws --endpoint-url=http://localhost:4566 sqs list-queues --region us-east-1

# SNS topics
aws --endpoint-url=http://localhost:4566 sns list-topics --region us-east-1

# S3 buckets
aws --endpoint-url=http://localhost:4566 s3 ls

# Secrets
aws --endpoint-url=http://localhost:4566 secretsmanager list-secrets --region us-east-1

# CloudWatch Log groups
aws --endpoint-url=http://localhost:4566 logs describe-log-groups --region us-east-1
```

Expected: All resources from the init script are listed.

- [ ] **Step 4: Verify Moto resources were created**

```bash
# Cognito User Pool
aws --endpoint-url=http://localhost:5555 cognito-idp list-user-pools --max-results 10 --region us-east-1

# EventBridge
aws --endpoint-url=http://localhost:5555 events list-event-buses --region us-east-1

# AppConfig
aws --endpoint-url=http://localhost:5555 appconfig list-applications --region us-east-1
```

Expected: Cognito pool `AuctionKingsDevPool`, event bus `auction-events`, and AppConfig application `AuctionKings` are listed.

- [ ] **Step 5: Verify Cognito secret files were written**

```bash
cat docker/secrets/cognito_user_pool_id.txt && echo
cat docker/secrets/cognito_app_client_id.txt && echo
```

Expected: Non-empty values for both files.

- [ ] **Step 6: Verify Cognito IDs in LocalStack Secrets Manager**

```bash
aws --endpoint-url=http://localhost:4566 secretsmanager get-secret-value \
  --secret-id "AuctionKings/Cognito/UserPoolId" --region us-east-1 \
  --query SecretString --output text

aws --endpoint-url=http://localhost:4566 secretsmanager get-secret-value \
  --secret-id "AuctionKings/Cognito/AppClientId" --region us-east-1 \
  --query SecretString --output text
```

Expected: Values match the contents of the secret files from Step 5.

- [ ] **Step 7: Clean up**

```bash
cd docker && docker compose -f docker-compose.infra.yml down -v
```
