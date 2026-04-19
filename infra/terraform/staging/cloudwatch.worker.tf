resource "aws_cloudwatch_log_group" "worker" {
  name              = "/ecs/auction-kings-staging-worker"
  retention_in_days = 7

  tags = { Name = "auction-kings-staging-worker-logs" }
}
