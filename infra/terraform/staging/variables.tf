variable "aws_region" {
  description = "AWS region for all resources."
  type        = string
  nullable    = false
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
    name     = string
    username = string
  })
}

variable "jwt" {
  description = "JWT token configuration."
  type = object({
    issuer   = string
    audience = string
  })
}

variable "ecs" {
  description = "ECS Fargate service configuration."
  type = object({
    api_image     = string
    worker_image  = string
    desired_count = number
  })
}
