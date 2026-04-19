resource "aws_ecs_task_definition" "worker" {
  family                   = "auction-kings-staging-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name      = "worker"
      image     = var.worker_image
      essential = true

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT",               value = "Production" },
        { name = "ConnectionStrings__DefaultConnection",  value = local.db_connection_string },
        { name = "ConnectionStrings__Redis",              value = local.redis_connection_string },
        { name = "Sqs__BidQueueUrl",                     value = aws_sqs_queue.bid_events.url },
        { name = "AWS_DEFAULT_REGION",                   value = var.aws_region },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.worker.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "worker"
        }
      }
    }
  ])

  tags = { Name = "auction-kings-staging-worker-task" }
}

resource "aws_ecs_service" "worker" {
  name            = "auction-kings-staging-worker"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.worker.id]
    assign_public_ip = false
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  deployment_controller {
    type = "ECS"
  }

  lifecycle {
    ignore_changes = [task_definition]
  }

  tags = { Name = "auction-kings-staging-worker-service" }
}
