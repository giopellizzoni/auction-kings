resource "aws_ecs_cluster" "main" {
  name = local.name_prefix

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-cluster"
  })
}

resource "aws_ecs_cluster_capacity_providers" "fargate" {
  cluster_name       = aws_ecs_cluster.main.name
  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    base              = 1
    weight            = 100
    capacity_provider = "FARGATE"
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${local.name_prefix}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.ecs.api_cpu
  memory                   = var.ecs.api_memory
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.ecs.api_image
      essential = true

      portMappings = [
        { containerPort = 8080, protocol = "tcp" }
      ]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Staging" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        # /dev/token is the only token issuer until Cognito. Outside Development it requires the
        # X-Dev-Token-Secret header to match Features__DevTokenSecret (injected below), so it is not
        # an anonymous admin-token mint behind the public ALB.
        { name = "Features__EnableDevTokenEndpoint", value = "true" },
        # Tasks are only reachable through the ALB (private subnets + ALB-only SG ingress), so
        # X-Forwarded-For/Proto can be trusted here — and only here.
        { name = "ForwardedHeaders__Enabled", value = "true" },
        # Applies EF Core migrations at startup (EF takes a DB lock, so replicas serialize).
        { name = "Database__MigrateOnStartup", value = "true" },
        { name = "Jwt__Issuer", value = var.jwt.issuer },
        { name = "Jwt__Audience", value = var.jwt.audience },
        { name = "Jwt__ExpiresInMinutes", value = tostring(var.jwt.expires_in_minutes) },
        # Bound from the "AWS:SQS" configuration section (SqsOptions) — "Sqs__…" is never read.
        { name = "AWS__SQS__BidQueueUrl", value = aws_sqs_queue.bid_events.url },
        { name = "AWS_DEFAULT_REGION", value = var.aws_region },
      ]

      # Secrets injected at task start by the execution role — never appear in plain-text env vars in the console.
      secrets = [
        { name = "Jwt__Secret", valueFrom = aws_secretsmanager_secret.jwt_secret.arn },
        { name = "ConnectionStrings__DefaultConnection", valueFrom = aws_secretsmanager_secret.db_connection_string.arn },
        { name = "ConnectionStrings__Redis", valueFrom = aws_secretsmanager_secret.redis_connection_string.arn },
        { name = "Features__DevTokenSecret", valueFrom = aws_secretsmanager_secret.dev_token_secret.arn },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "api"
        }
      }

      healthCheck = {
        command     = ["CMD-SHELL", "curl -sf http://localhost:8080/health/live || exit 1"]
        interval    = 30
        timeout     = 10
        retries     = 3
        startPeriod = 60
      }
    }
  ])

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-api-task"
  })
}

resource "aws_ecs_service" "api" {
  name            = "${local.name_prefix}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.ecs.api_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.api.id]
    assign_public_ip = false # Traffic via NAT Gateway
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  deployment_controller {
    type = "ECS"
  }

  # Allow Terraform to update the task definition without destroying the service.
  lifecycle {
    ignore_changes = [task_definition]
  }

  depends_on = [aws_lb_listener.http]

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-api-service"
  })
}
