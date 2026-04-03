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
