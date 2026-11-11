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

# ----- Connection strings -----
# Full connection strings (they embed the DB password / Redis AUTH token) are injected into the
# containers through ECS `secrets`, so the credentials never appear as plain-text environment
# variables in the task definition (visible via the console / DescribeTaskDefinition).

resource "aws_secretsmanager_secret" "db_connection_string" {
  name                    = "/auction-kings/${var.environment}/db-connection-string"
  recovery_window_in_days = 0
  tags                    = merge(local.common_tags, { Name = "${local.name_prefix}-db-connection-string" })
}

resource "aws_secretsmanager_secret_version" "db_connection_string" {
  secret_id     = aws_secretsmanager_secret.db_connection_string.id
  secret_string = local.db_connection_string
}

resource "aws_secretsmanager_secret" "redis_connection_string" {
  name                    = "/auction-kings/${var.environment}/redis-connection-string"
  recovery_window_in_days = 0
  tags                    = merge(local.common_tags, { Name = "${local.name_prefix}-redis-connection-string" })
}

resource "aws_secretsmanager_secret_version" "redis_connection_string" {
  secret_id     = aws_secretsmanager_secret.redis_connection_string.id
  secret_string = local.redis_connection_string
}

# ----- Dev token endpoint secret -----
# /dev/token is the only token issuer until Cognito lands. Outside Development it only answers
# requests carrying X-Dev-Token-Secret with this value. Retrieve it with:
#   aws secretsmanager get-secret-value --secret-id /auction-kings/staging/dev-token-secret \
#     --query SecretString --output text

resource "random_password" "dev_token_secret" {
  length  = 48
  special = false
}

resource "aws_secretsmanager_secret" "dev_token_secret" {
  name                    = "/auction-kings/${var.environment}/dev-token-secret"
  recovery_window_in_days = 0
  tags                    = merge(local.common_tags, { Name = "${local.name_prefix}-dev-token-secret" })
}

resource "aws_secretsmanager_secret_version" "dev_token_secret" {
  secret_id     = aws_secretsmanager_secret.dev_token_secret.id
  secret_string = random_password.dev_token_secret.result
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
