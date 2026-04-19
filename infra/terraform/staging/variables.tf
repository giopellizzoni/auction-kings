variable "aws_region" {
  description = "AWS region for all resources."
  type        = string
  default     = "us-east-1"
}

variable "app_image" {
  description = "Full ECR/GHCR image URI for the API (e.g. ghcr.io/pellizzonicode/auction-kings-api:sha-abc123)."
  type        = string
}

variable "worker_image" {
  description = "Full ECR/GHCR image URI for the BackgroundWorkers (e.g. ghcr.io/pellizzonicode/auction-kings-worker:sha-abc123)."
  type        = string
}

variable "db_name" {
  description = "PostgreSQL database name."
  type        = string
  default     = "AuctionKingsDB"
}

variable "db_username" {
  description = "PostgreSQL master username."
  type        = string
  default     = "auctionkings"
}

variable "jwt_issuer" {
  description = "JWT issuer claim."
  type        = string
  default     = "auction-kings-staging"
}

variable "jwt_audience" {
  description = "JWT audience claim."
  type        = string
  default     = "auction-kings-staging"
}

variable "desired_count" {
  description = "Desired number of ECS tasks."
  type        = number
  default     = 1
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.0.0.0/16"
}
