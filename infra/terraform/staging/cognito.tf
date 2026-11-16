# ----- Cognito user pool — the identity provider -----
# Sign-up, sign-in, passwords and email verification live here; the API only validates the RS256
# access tokens it issues (Cognito__* env vars in ecs.api.tf). Group membership becomes the JWT role.
#
# The staging teardown (infra-staging-destroy.yml) deletes this pool and every account in it, which
# is why deletion protection is off. A recreated pool has a new id: ecs.api.tf picks it up on apply.

resource "aws_cognito_user_pool" "users" {
  name = "${local.name_prefix}-users"

  user_pool_tier           = "LITE"
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]
  deletion_protection      = "INACTIVE"

  password_policy {
    minimum_length                   = var.cognito.password_minimum_length
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = false
    require_uppercase                = false
    temporary_password_validity_days = 7
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  # Uses Cognito's built-in email sender (low daily quota, fine for staging).
  email_configuration {
    email_sending_account = "COGNITO_DEFAULT"
  }

  lambda_config {
    post_confirmation = aws_lambda_function.cognito_post_confirmation.arn
  }

  schema {
    name                = "name"
    attribute_data_type = "String"
    mutable             = true
    required            = false

    string_attribute_constraints {
      min_length = 1
      max_length = 100
    }
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-users"
  })
}
