# ----- Cognito post-confirmation trigger -----
# Adds every newly confirmed account to the Bidder group, so a user who signs up can create auctions
# and bid without an admin step. Code: infra/lambda/cognito-post-confirmation/handler.py.

# Built at plan time; infra-staging.yml ships .build/ to the apply job with the plan.
data "archive_file" "cognito_post_confirmation" {
  type        = "zip"
  source_dir  = "${path.module}/../../lambda/cognito-post-confirmation"
  output_path = "${path.module}/.build/cognito-post-confirmation.zip"
  excludes    = ["__pycache__"]
}

resource "aws_iam_role" "cognito_post_confirmation" {
  name = local.post_confirmation_function_name

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })

  tags = merge(local.common_tags, {
    Name = local.post_confirmation_function_name
  })
}

resource "aws_iam_role_policy" "cognito_post_confirmation" {
  name = "add-user-to-default-group"
  role = aws_iam_role.cognito_post_confirmation.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "AddToGroup"
        Effect   = "Allow"
        Action   = "cognito-idp:AdminAddUserToGroup"
        Resource = aws_cognito_user_pool.users.arn
      },
      {
        Sid      = "Logs"
        Effect   = "Allow"
        Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = "${aws_cloudwatch_log_group.cognito_post_confirmation.arn}:*"
      }
    ]
  })
}

resource "aws_cloudwatch_log_group" "cognito_post_confirmation" {
  name              = "/aws/lambda/${local.post_confirmation_function_name}"
  retention_in_days = var.cloudwatch.log_retention_days

  tags = merge(local.common_tags, {
    Name = "${local.post_confirmation_function_name}-logs"
  })
}

resource "aws_lambda_function" "cognito_post_confirmation" {
  function_name = local.post_confirmation_function_name
  role          = aws_iam_role.cognito_post_confirmation.arn
  runtime       = "python3.13"
  handler       = "handler.handler"

  filename         = data.archive_file.cognito_post_confirmation.output_path
  source_code_hash = data.archive_file.cognito_post_confirmation.output_base64sha256
  timeout          = 5
  memory_size      = 128

  environment {
    variables = {
      # local.default_user_group, not the group resource: see locals.tf.
      DEFAULT_GROUP = local.default_user_group
    }
  }

  logging_config {
    log_format = "Text"
    log_group  = aws_cloudwatch_log_group.cognito_post_confirmation.name
  }

  tags = merge(local.common_tags, {
    Name = local.post_confirmation_function_name
  })
}

resource "aws_lambda_permission" "cognito_post_confirmation" {
  statement_id  = "AllowCognitoInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.cognito_post_confirmation.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.users.arn
}
