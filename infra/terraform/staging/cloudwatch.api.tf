resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${local.name_prefix}-api"
  retention_in_days = var.cloudwatch.log_retention_days

  tags = { Name = "${local.name_prefix}-api-logs" }
}
