output "alb_dns_name" {
  description = "Public DNS of the Application Load Balancer — staging endpoint."
  value       = aws_lb.api.dns_name
}

output "alb_url" {
  description = "Full HTTP URL for the staging API."
  value       = "http://${aws_lb.api.dns_name}"
}

output "ecs_cluster_name" {
  description = "ECS cluster name (used in deploy workflow)."
  value       = aws_ecs_cluster.main.name
}

output "ecs_service_name" {
  description = "ECS API service name (used in deploy workflow)."
  value       = aws_ecs_service.api.name
}

output "ecs_task_definition_family" {
  description = "ECS API task definition family name."
  value       = aws_ecs_task_definition.api.family
}

output "ecs_worker_service_name" {
  description = "ECS worker service name (used in deploy workflow)."
  value       = aws_ecs_service.worker.name
}

output "ecs_worker_task_definition_family" {
  description = "ECS worker task definition family name."
  value       = aws_ecs_task_definition.worker.family
}

output "cloudwatch_worker_log_group" {
  description = "CloudWatch log group for ECS worker logs."
  value       = aws_cloudwatch_log_group.worker.name
}

output "sqs_bid_queue_url" {
  description = "SQS queue URL for domain event publishing."
  value       = aws_sqs_queue.bid_events.url
}

output "cloudwatch_log_group" {
  description = "CloudWatch log group for ECS task logs."
  value       = aws_cloudwatch_log_group.api.name
}

output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint (host:port)."
  value       = "${aws_db_instance.postgres.address}:${aws_db_instance.postgres.port}"
  sensitive   = true
}
