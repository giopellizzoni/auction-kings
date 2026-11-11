variable "environment" {
  description = "Deployment environment name (dev, staging, production)."
  type        = string
  nullable    = false
}

variable "aws_region" {
  description = "AWS region for all resources."
  type        = string
  nullable    = false
}

variable "github" {
  description = "GitHub OIDC configuration for the deploy role trust policy. oidc_subjects are the exact token 'sub' claims allowed to assume the role (e.g. repo:OWNER/REPO:ref:refs/heads/develop, repo:OWNER/REPO:environment:staging)."
  type = object({
    oidc_subjects = list(string)
  })

  validation {
    condition     = length(var.github.oidc_subjects) > 0
    error_message = "github.oidc_subjects must contain at least one subject."
  }

  validation {
    # A trailing wildcard (repo:OWNER/REPO:*) lets ANY ref — including pull_request runs and
    # arbitrary branches — assume a role that can modify IAM, RDS and ECS.
    condition     = alltrue([for s in var.github.oidc_subjects : !endswith(s, ":*") && !strcontains(s, "pull_request")])
    error_message = "github.oidc_subjects must name specific refs/environments; wildcard (':*') and pull_request subjects are not allowed."
  }
}

variable "vpc" {
  description = "VPC network configuration."
  type = object({
    cidr            = string
    public_subnets  = list(string)
    private_subnets = list(string)
  })
}

variable "rds" {
  description = "RDS PostgreSQL configuration."
  type = object({
    name                    = string
    username                = string
    engine_version          = string
    instance_class          = string
    allocated_storage       = number
    max_allocated_storage   = number
    backup_retention_period = number
  })
}

variable "elasticache" {
  description = "ElastiCache Redis configuration."
  type = object({
    node_type                = string
    engine_version           = string
    num_cache_clusters       = number
    snapshot_retention_limit = number
  })
}

variable "ecs" {
  description = "ECS Fargate service configuration."
  type = object({
    api_image            = string
    worker_image         = string
    api_desired_count    = number
    worker_desired_count = number
    api_cpu              = number
    api_memory           = number
    worker_cpu           = number
    worker_memory        = number
  })
}

variable "jwt" {
  description = "JWT token configuration."
  type = object({
    issuer             = string
    audience           = string
    expires_in_minutes = number
  })
}

variable "cloudwatch" {
  description = "CloudWatch Logs configuration."
  type = object({
    log_retention_days = number
  })
}

variable "sqs" {
  description = "SQS bid-events queue configuration."
  type = object({
    bid_events_retention_seconds     = number
    bid_events_dlq_retention_seconds = number
    visibility_timeout_seconds       = number
    receive_wait_time_seconds        = number
    max_receive_count                = number
  })
}
