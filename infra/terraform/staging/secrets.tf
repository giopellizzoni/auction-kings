resource "random_password" "jwt_secret" {
  length  = 64
  special = false
}

# ----- DB password -----

resource "aws_secretsmanager_secret" "db_password" {
  name                    = "/auction-kings/${var.environment}/db-password"
  recovery_window_in_days = 0
  tags                    = { Name = "${local.name_prefix}-db-password" }
}

resource "aws_secretsmanager_secret_version" "db_password" {
  secret_id     = aws_secretsmanager_secret.db_password.id
  secret_string = random_password.db.result
}

# ----- Redis AUTH token -----

resource "aws_secretsmanager_secret" "redis_auth" {
  name                    = "/auction-kings/${var.environment}/redis-auth"
  recovery_window_in_days = 0
  tags                    = { Name = "${local.name_prefix}-redis-auth" }
}

resource "aws_secretsmanager_secret_version" "redis_auth" {
  secret_id     = aws_secretsmanager_secret.redis_auth.id
  secret_string = random_password.redis.result
}

# ----- JWT secret -----

resource "aws_secretsmanager_secret" "jwt_secret" {
  name                    = "/auction-kings/${var.environment}/jwt-secret"
  recovery_window_in_days = 0
  tags                    = { Name = "${local.name_prefix}-jwt-secret" }
}

resource "aws_secretsmanager_secret_version" "jwt_secret" {
  secret_id     = aws_secretsmanager_secret.jwt_secret.id
  secret_string = random_password.jwt_secret.result
}
