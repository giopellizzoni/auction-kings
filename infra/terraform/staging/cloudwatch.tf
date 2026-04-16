resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/auction-kings-staging-api"
  retention_in_days = 7   # Short retention for staging cost control

  tags = { Name = "auction-kings-staging-api-logs" }
}
