resource "aws_sqs_queue" "bid_events_dlq" {
  name                      = "${local.name_prefix}-bid-events-dlq"
  message_retention_seconds = var.sqs.bid_events_dlq_retention_seconds

  tags = { Name = "${local.name_prefix}-bid-events-dlq" }
}

resource "aws_sqs_queue" "bid_events" {
  name                       = "${local.name_prefix}-bid-events"
  message_retention_seconds  = var.sqs.bid_events_retention_seconds
  visibility_timeout_seconds = var.sqs.visibility_timeout_seconds
  receive_wait_time_seconds  = var.sqs.receive_wait_time_seconds

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.bid_events_dlq.arn
    maxReceiveCount     = var.sqs.max_receive_count
  })

  tags = { Name = "${local.name_prefix}-bid-events" }
}

# Allow ECS task role to send/receive/delete from both queues
resource "aws_sqs_queue_policy" "bid_events" {
  queue_url = aws_sqs_queue.bid_events.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowEcsTaskRole"
        Effect    = "Allow"
        Principal = { AWS = aws_iam_role.ecs_task.arn }
        Action    = ["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:GetQueueAttributes"]
        Resource  = aws_sqs_queue.bid_events.arn
      }
    ]
  })
}
