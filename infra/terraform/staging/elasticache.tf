resource "random_password" "redis" {
  length  = 32
  special = true
  # ElastiCache auth tokens prohibit: space, ", /, @, and bracket-like chars.
  # Safe subset: ! # $ % & * - _ = + ?
  override_special = "!#$%&*-_=+?"
}

resource "aws_elasticache_subnet_group" "redis" {
  name       = "${local.name_prefix}"
  subnet_ids = aws_subnet.private[*].id

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-redis-subnet-group"
  })
}

resource "aws_elasticache_replication_group" "redis" {
  replication_group_id = "${local.name_prefix}"
  description          = "Redis for AuctionKings staging - cache + SignalR backplane"

  node_type          = "cache.t4g.micro"
  num_cache_clusters = 1 # Single node for staging; increase for prod
  engine_version     = "7.1"
  port               = 6379

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

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-redis"
  })
}
