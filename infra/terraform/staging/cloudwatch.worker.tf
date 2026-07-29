resource "aws_cloudwatch_log_group" "worker" {
  name              = "/ecs/${local.name_prefix}-worker"
  retention_in_days = var.cloudwatch.log_retention_days

  tags = { Name = "${local.name_prefix}-worker-logs" }
}
