resource "aws_ecs_cluster" "main" {
  name = "auction-kings-staging"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = { Name = "auction-kings-staging-cluster" }
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

locals {
  # Build the PostgreSQL connection string from RDS outputs.
  db_connection_string = "Host=${aws_db_instance.postgres.address};Port=${aws_db_instance.postgres.port};Database=${var.db_name};Username=${var.db_username};Password=${random_password.db.result};SSL Mode=Require;Trust Server Certificate=true"

  # Redis connection string — TLS required by ElastiCache AUTH token.
  redis_connection_string = "${aws_elasticache_replication_group.redis.primary_endpoint_address}:6379,password=${random_password.redis.result},ssl=true,abortConnect=false"
}

resource "aws_ecs_task_definition" "api" {
  family                   = "auction-kings-staging-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256    # 0.25 vCPU — adequate for staging
  memory                   = 512
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.app_image
      essential = true

      portMappings = [
        { containerPort = 8080, protocol = "tcp" }
      ]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT",              value = "Staging" },
        { name = "ASPNETCORE_URLS",                     value = "http://+:8080" },
        { name = "Features__EnableDevTokenEndpoint",    value = "true" },
        { name = "ConnectionStrings__DefaultConnection", value = local.db_connection_string },
        { name = "ConnectionStrings__Redis",        value = local.redis_connection_string },
        { name = "Jwt__Issuer",                     value = var.jwt_issuer },
        { name = "Jwt__Audience",                   value = var.jwt_audience },
        { name = "Jwt__ExpiresInMinutes",           value = "60" },
        { name = "Sqs__BidQueueUrl",                value = aws_sqs_queue.bid_events.url },
        { name = "AWS_DEFAULT_REGION",              value = var.aws_region },
      ]

      # Secrets injected at task start by the execution role — never appear in plain-text env vars in the console.
      secrets = [
        { name = "Jwt__Secret", valueFrom = aws_secretsmanager_secret.jwt_secret.arn },
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

  tags = { Name = "auction-kings-staging-api-task" }
}

resource "aws_ecs_service" "api" {
  name            = "auction-kings-staging-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.api.id]
    assign_public_ip = false   # Traffic via NAT Gateway
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

  tags = { Name = "auction-kings-staging-api-service" }
}
