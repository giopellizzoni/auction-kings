#!/bin/bash
set -euo pipefail

MOTO_ENDPOINT="http://moto:5555"
LOCALSTACK_ENDPOINT="http://localstack:4566"
REGION="us-east-1"
SECRETS_DIR="/secrets"

export AWS_ACCESS_KEY_ID="testing"
export AWS_SECRET_ACCESS_KEY="testing"
export AWS_DEFAULT_REGION="$REGION"

# C2: Validate SECRETS_DIR before doing anything else
if [ ! -d "$SECRETS_DIR" ] || [ ! -w "$SECRETS_DIR" ]; then
  echo "ERROR: SECRETS_DIR '$SECRETS_DIR' is not a writable directory." >&2
  exit 1
fi

# C1: Wait for Moto with timeout
echo "=== Waiting for Moto to be ready ==="
MAX_RETRIES=30
RETRY_COUNT=0
until curl -sf "$MOTO_ENDPOINT/moto-api/" > /dev/null 2>&1; do
  RETRY_COUNT=$((RETRY_COUNT + 1))
  if [ "$RETRY_COUNT" -ge "$MAX_RETRIES" ]; then
    echo "ERROR: Moto did not become ready after $MAX_RETRIES attempts. Aborting." >&2
    exit 1
  fi
  echo "Moto not ready, retrying in 2s... ($RETRY_COUNT/$MAX_RETRIES)"
  sleep 2
done

# C1: Wait for LocalStack with timeout (reset counter)
echo "=== Waiting for LocalStack to be ready ==="
RETRY_COUNT=0
until curl -sf "$LOCALSTACK_ENDPOINT/_localstack/health" > /dev/null 2>&1; do
  RETRY_COUNT=$((RETRY_COUNT + 1))
  if [ "$RETRY_COUNT" -ge "$MAX_RETRIES" ]; then
    echo "ERROR: LocalStack did not become ready after $MAX_RETRIES attempts. Aborting." >&2
    exit 1
  fi
  echo "LocalStack not ready, retrying in 2s... ($RETRY_COUNT/$MAX_RETRIES)"
  sleep 2
done

echo "=== Provisioning Moto resources ==="

# -----------------------------------------------
# Cognito
# -----------------------------------------------
echo "Creating Cognito User Pool..."
# I1: Use --query/--output text instead of python3
USER_POOL_ID=$(aws cognito-idp create-user-pool \
  --pool-name AuctionKingsDevPool \
  --auto-verified-attributes email \
  --schema \
    Name=email,Required=true,Mutable=true \
    Name=custom:reputation,AttributeDataType=Number,Mutable=true \
    Name=custom:is_verified,AttributeDataType=String,Mutable=true \
    Name=custom:total_bids,AttributeDataType=Number,Mutable=true \
  --query 'UserPool.Id' \
  --output text \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")
echo "User Pool ID: $USER_POOL_ID"

echo "Creating App Client..."
# I1: Use --query/--output text instead of python3
APP_CLIENT_ID=$(aws cognito-idp create-user-pool-client \
  --user-pool-id "$USER_POOL_ID" \
  --client-name AuctionKingsDevClient \
  --no-generate-secret \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --query 'UserPoolClient.ClientId' \
  --output text \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")
echo "App Client ID: $APP_CLIENT_ID"

# Write Cognito IDs to secret files
echo -n "$USER_POOL_ID" > "$SECRETS_DIR/cognito_user_pool_id.txt"
echo -n "$APP_CLIENT_ID" > "$SECRETS_DIR/cognito_app_client_id.txt"

# I2: Push Cognito IDs to LocalStack Secrets Manager — idempotent
aws secretsmanager describe-secret \
  --secret-id "AuctionKings/Cognito/UserPoolId" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$REGION" > /dev/null 2>&1 || \
  aws secretsmanager create-secret \
    --name "AuctionKings/Cognito/UserPoolId" \
    --secret-string "$USER_POOL_ID" \
    --endpoint-url "$LOCALSTACK_ENDPOINT" \
    --region "$REGION"

aws secretsmanager describe-secret \
  --secret-id "AuctionKings/Cognito/AppClientId" \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$REGION" > /dev/null 2>&1 || \
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
# I1: Use --query/--output text instead of python3
APP_ID=$(aws appconfig create-application \
  --name AuctionKings \
  --query 'Id' \
  --output text \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

ENV_ID=$(aws appconfig create-environment \
  --application-id "$APP_ID" \
  --name Development \
  --query 'Id' \
  --output text \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

PROFILE_ID=$(aws appconfig create-configuration-profile \
  --application-id "$APP_ID" \
  --name FeatureFlags \
  --location-uri hosted \
  --type AWS.AppConfig.FeatureFlags \
  --query 'Id' \
  --output text \
  --endpoint-url "$MOTO_ENDPOINT" \
  --region "$REGION")

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

# I3: Resolve SQS ARN dynamically from LocalStack instead of hardcoding it
QUEUE_ARN=$(aws sqs get-queue-attributes \
  --queue-url "http://localstack:4566/000000000000/auction-bids-queue" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' \
  --output text \
  --endpoint-url "$LOCALSTACK_ENDPOINT" \
  --region "$REGION")

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
