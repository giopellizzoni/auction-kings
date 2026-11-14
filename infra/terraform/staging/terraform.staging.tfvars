# Staging variable values — committed and passed by CI (-var-file=terraform.staging.tfvars) to the
# infrastructure and teardown workflows. Contains NO secrets: the DB password, Redis AUTH token, JWT
# secret and /dev/token key come from random_password and live in Secrets Manager; the MediatR license
# is set out of band. The image tags are only the initial value: the ECS services ignore
# task_definition changes and deploy-staging.yml rolls out the real image.

environment = "staging"
aws_region  = "us-east-1"

github = {
  repo = "repo:giopellizzoni/auction-kings:*"
}

vpc = {
  cidr            = "10.0.0.0/16"
  public_subnets  = ["10.0.1.0/24", "10.0.2.0/24"]
  private_subnets = ["10.0.11.0/24", "10.0.12.0/24"]
}

rds = {
  name                    = "AuctionKingsDB"
  username                = "auctionkings"
  engine_version          = "16"
  instance_class          = "db.t4g.micro"
  allocated_storage       = 20
  max_allocated_storage   = 100
  backup_retention_period = 3
}

elasticache = {
  node_type                = "cache.t4g.micro"
  engine_version           = "7.1"
  num_cache_clusters       = 1
  snapshot_retention_limit = 1
}

ecs = {
  api_image            = "ghcr.io/giopellizzoni/auction-kings-api:latest"
  worker_image         = "ghcr.io/giopellizzoni/auction-kings-worker:latest"
  api_desired_count    = 1
  worker_desired_count = 1
  api_cpu              = 256
  api_memory           = 512
  worker_cpu           = 256
  worker_memory        = 512
}

jwt = {
  issuer             = "auction-kings-staging"
  audience           = "auction-kings-staging"
  expires_in_minutes = 60
}

cloudwatch = {
  log_retention_days = 7
}

sqs = {
  bid_events_retention_seconds     = 86400
  bid_events_dlq_retention_seconds = 1209600
  visibility_timeout_seconds       = 30
  receive_wait_time_seconds        = 20
  max_receive_count                = 5
}
