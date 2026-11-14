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

# ----- MediatR license key -----
# Terraform creates only the secret, never its value: the key is a licensed credential that must not enter the
# repository or the Terraform state. Set it once, out of band:
#   aws secretsmanager put-secret-value --secret-id /auction-kings/<environment>/mediatr-license \
#     --secret-string file://<path-to-a-file-holding-the-key>
# There is deliberately no aws_secretsmanager_secret_version here, so later applies never overwrite or read it.
# Do not reference this secret from an ECS task definition until it has a value: a task whose secret has no
# value fails to start.

resource "aws_secretsmanager_secret" "mediatr_license" {
  name                    = "/auction-kings/${var.environment}/mediatr-license"
  recovery_window_in_days = 0
  tags                    = { Name = "${local.name_prefix}-mediatr-license" }
}
