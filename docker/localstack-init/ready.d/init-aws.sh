#!/bin/bash
set -euo pipefail

REGION="us-east-1"

echo "=== Provisioning LocalStack resources ==="

# -----------------------------------------------
# DynamoDB
# -----------------------------------------------
echo "Creating DynamoDB table: AuctionEvents..."
awslocal dynamodb describe-table --table-name AuctionEvents --region "$REGION" > /dev/null 2>&1 || \
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
awslocal dynamodb describe-table --table-name AuctionSnapshots --region "$REGION" > /dev/null 2>&1 || \
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
  --region "$REGION" 2>/dev/null || true

# -----------------------------------------------
# SQS
# -----------------------------------------------
echo "Creating SQS queues..."

# Get or create DLQ
awslocal sqs get-queue-url --queue-name auction-bids-dlq --region "$REGION" > /dev/null 2>&1 || \
  awslocal sqs create-queue --queue-name auction-bids-dlq --region "$REGION"

DLQ_URL=$(awslocal sqs get-queue-url --queue-name auction-bids-dlq --region "$REGION" --query QueueUrl --output text)
DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url "$DLQ_URL" \
  --attribute-names QueueArn \
  --query "Attributes.QueueArn" \
  --output text \
  --region "$REGION")

REDRIVE_JSON=$(printf '{"deadLetterTargetArn":"%s","maxReceiveCount":"3"}' "$DLQ_ARN")
ATTRS_JSON=$(printf '{"RedrivePolicy":"%s"}' "$(echo "$REDRIVE_JSON" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')")

awslocal sqs get-queue-url --queue-name auction-bids-queue --region "$REGION" > /dev/null 2>&1 || \
  awslocal sqs create-queue \
    --queue-name auction-bids-queue \
    --attributes "$ATTRS_JSON" \
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
awslocal s3api head-bucket --bucket auctionkings-assets 2>/dev/null || \
  awslocal s3 mb s3://auctionkings-assets --region "$REGION"

# -----------------------------------------------
# Secrets Manager (partial — Cognito IDs added by moto-init)
# -----------------------------------------------
echo "Creating Secrets Manager entries..."
awslocal secretsmanager describe-secret --secret-id "AuctionKings/Database/ConnectionString" --region "$REGION" > /dev/null 2>&1 || \
  awslocal secretsmanager create-secret \
    --name "AuctionKings/Database/ConnectionString" \
    --secret-string "Host=postgres;Port=5432;Database=AuctionKingsDB;Username=dev;Password=localdev" \
    --region "$REGION"

awslocal secretsmanager describe-secret --secret-id "AuctionKings/Redis/ConnectionString" --region "$REGION" > /dev/null 2>&1 || \
  awslocal secretsmanager create-secret \
    --name "AuctionKings/Redis/ConnectionString" \
    --secret-string "redis:6379,password=localdev" \
    --region "$REGION"

# -----------------------------------------------
# CloudWatch Logs
# -----------------------------------------------
echo "Creating CloudWatch Log groups..."
awslocal logs describe-log-groups --log-group-name-prefix /auctionkings/api --region "$REGION" | grep -q "/auctionkings/api" 2>/dev/null || \
  awslocal logs create-log-group --log-group-name /auctionkings/api --region "$REGION"

awslocal logs describe-log-groups --log-group-name-prefix /auctionkings/worker --region "$REGION" | grep -q "/auctionkings/worker" 2>/dev/null || \
  awslocal logs create-log-group --log-group-name /auctionkings/worker --region "$REGION"

echo "=== LocalStack provisioning complete ==="
