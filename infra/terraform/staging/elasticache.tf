resource "random_password" "redis" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]<>?"
}

resource "aws_elasticache_subnet_group" "redis" {
  name       = "auction-kings-staging"
  subnet_ids = aws_subnet.private[*].id
  tags       = { Name = "auction-kings-staging-redis-subnet-group" }
}

resource "aws_elasticache_replication_group" "redis" {
  replication_group_id = "auction-kings-staging"
  description          = "Redis for AuctionKings staging - cache + SignalR backplane"

  node_type            = "cache.t4g.micro"
  num_cache_clusters   = 1   # Single node for staging; increase for prod
  engine_version       = "7.1"
  port                 = 6379

  subnet_group_name  = aws_elasticache_subnet_group.redis.name
  security_group_ids = [aws_security_group.elasticache.id]

  # AUTH token for TLS-capable Redis connections
  auth_token                 = random_password.redis.result
  transit_encryption_enabled = true
  at_rest_encryption_enabled = true

  # Minimal backup for staging
  snapshot_retention_limit = 1
  snapshot_window          = "03:00-04:00"

  apply_immediately = true

  tags = { Name = "auction-kings-staging-redis" }
}
