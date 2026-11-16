# Public app client (no secret) used by the API's callers: the CLI walkthrough, a future SPA/mobile app.
resource "aws_cognito_user_pool_client" "api" {
  name         = "${local.name_prefix}-api"
  user_pool_id = aws_cognito_user_pool.users.id

  generate_secret               = false
  prevent_user_existence_errors = "ENABLED"
  explicit_auth_flows = [
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_USER_SRP_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH",
  ]

  access_token_validity  = var.cognito.access_token_validity_minutes
  id_token_validity      = var.cognito.access_token_validity_minutes
  refresh_token_validity = var.cognito.refresh_token_validity_days

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }
}
