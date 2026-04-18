# ──────────────────────────────────────────────────────────────
# One-time imports for resources that already exist in AWS
# from a previous partial apply with local-only state.
#
# After a successful `terraform apply` that reconciles state,
# delete this file — the import blocks are idempotent but no
# longer needed once the resources are in state.
# ──────────────────────────────────────────────────────────────

import {
  to = aws_iam_role.ecs_execution
  id = "auction-kings-staging-ecs-execution"
}

import {
  to = aws_iam_role.ecs_task
  id = "auction-kings-staging-ecs-task"
}

import {
  to = aws_cloudwatch_log_group.api
  id = "/ecs/auction-kings-staging-api"
}

import {
  to = aws_lb_target_group.api
  id = "arn:aws:elasticloadbalancing:us-east-1:314146334477:targetgroup/auction-kings-staging-api"
}

import {
  to = aws_db_subnet_group.postgres
  id = "auction-kings-staging"
}

import {
  to = aws_secretsmanager_secret.db_password
  id = "arn:aws:secretsmanager:us-east-1:314146334477:secret:/auction-kings/staging/db-password"
}

import {
  to = aws_secretsmanager_secret.redis_auth
  id = "arn:aws:secretsmanager:us-east-1:314146334477:secret:/auction-kings/staging/redis-auth"
}

import {
  to = aws_secretsmanager_secret.jwt_secret
  id = "arn:aws:secretsmanager:us-east-1:314146334477:secret:/auction-kings/staging/jwt-secret"
}

import {
  to = aws_elasticache_subnet_group.redis
  id = "auction-kings-staging"
}

# SQS queues — created successfully in the last run.
import {
  to = aws_sqs_queue.bid_events
  id = "https://sqs.us-east-1.amazonaws.com/314146334477/auction-kings-staging-bid-events"
}

import {
  to = aws_sqs_queue.bid_events_dlq
  id = "https://sqs.us-east-1.amazonaws.com/314146334477/auction-kings-staging-bid-events-dlq"
}

# VPC resources created in the previous partial apply.
# Subnet and EIP IDs taken from error output.
import {
  to = aws_subnet.public[0]
  id = "subnet-064314e80ee52476e"
}

import {
  to = aws_subnet.public[1]
  id = "subnet-078043f975e66348c"
}

import {
  to = aws_eip.nat
  id = "eipalloc-0de5215ec10b8aecd"
}
