resource "aws_sqs_queue" "bid_events_dlq" {
  name                       = "auction-kings-staging-bid-events-dlq"
  message_retention_seconds  = 1209600  # 14 days
  tags                       = { Name = "auction-kings-staging-bid-events-dlq" }
}

resource "aws_sqs_queue" "bid_events" {
  name                       = "auction-kings-staging-bid-events"
  message_retention_seconds  = 86400    # 1 day
  visibility_timeout_seconds = 30
  receive_wait_time_seconds  = 20       # Long polling

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.bid_events_dlq.arn
    maxReceiveCount     = 5
  })

  tags = { Name = "auction-kings-staging-bid-events" }
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
