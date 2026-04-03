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
