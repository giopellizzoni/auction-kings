locals {
  name_prefix = "auction-kings-${var.environment}"

  # common_tags is intentionally minimal: Project, Environment, and ManagedBy
  # are already applied globally via the provider's default_tags in main.tf.
  # Add resource-specific tags with merge(local.common_tags, { Name = "..." }).
  common_tags = {}

  azs           = slice(data.aws_availability_zones.available.names, 0, 2)
  public_cidrs  = var.vpc.public_subnets
  private_cidrs = var.vpc.private_subnets

  # GitHub OIDC provider ARN — constructed to avoid needing iam:ListOpenIDConnectProviders
  # on the bootstrap role.
  github_oidc_provider_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"

  # Build the PostgreSQL connection string from RDS outputs.
  db_connection_string = "Host=${aws_db_instance.postgres.address};Port=${aws_db_instance.postgres.port};Database=${var.rds.name};Username=${var.rds.username};Password=${random_password.db.result};SSL Mode=Require;Trust Server Certificate=true"

  # Redis connection string — TLS required by ElastiCache AUTH token.
  redis_connection_string = "${aws_elasticache_replication_group.redis.primary_endpoint_address}:6379,password=${random_password.redis.result},ssl=true,abortConnect=false"
}
