resource "aws_ecs_task_definition" "worker" {
  family                   = "${local.name_prefix}-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.ecs.worker_cpu
  memory                   = var.ecs.worker_memory
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name      = "worker"
      image     = var.ecs.worker_image
      essential = true

      environment = [
        # Host.CreateApplicationBuilder reads DOTNET_ENVIRONMENT; ASPNETCORE_ENVIRONMENT is ignored here.
        { name = "DOTNET_ENVIRONMENT", value = "Staging" },
        # Bound from the "AWS:SQS" configuration section (SqsOptions) — "Sqs__…" is never read.
        { name = "AWS__SQS__BidQueueUrl", value = aws_sqs_queue.bid_events.url },
        { name = "AWS_DEFAULT_REGION", value = var.aws_region },
      ]

      secrets = [
        { name = "ConnectionStrings__DefaultConnection", valueFrom = aws_secretsmanager_secret.db_connection_string.arn },
        { name = "ConnectionStrings__Redis", valueFrom = aws_secretsmanager_secret.redis_connection_string.arn },
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

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-worker-task"
  })
}

resource "aws_ecs_service" "worker" {
  name            = "${local.name_prefix}-worker"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.ecs.worker_desired_count
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

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-worker-service"
  })
}
