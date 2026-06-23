# ----- ECS Task Execution Role -----
# Used by the ECS agent to pull images and push logs.

resource "aws_iam_role" "ecs_execution" {
  name = "auction-kings-staging-ecs-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_execution_managed" {
  role       = aws_iam_role.ecs_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Allow the execution role to read secrets so ECS can inject them as env vars.
resource "aws_iam_role_policy" "ecs_execution_secrets" {
  name = "read-staging-secrets"
  role = aws_iam_role.ecs_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = ["secretsmanager:GetSecretValue"]
      Resource = [
        aws_secretsmanager_secret.db_password.arn,
        aws_secretsmanager_secret.redis_auth.arn,
        aws_secretsmanager_secret.jwt_secret.arn,
      ]
    }]
  })
}

# ----- ECS Task Role -----
# Assumed by the running application container for runtime AWS API calls.

resource "aws_iam_role" "ecs_task" {
  name = "auction-kings-staging-ecs-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })
}

resource "aws_iam_role_policy" "ecs_task_sqs" {
  name = "sqs-bid-events"
  role = aws_iam_role.ecs_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes",
        "sqs:GetQueueUrl",
      ]
      Resource = [
        aws_sqs_queue.bid_events.arn,
        aws_sqs_queue.bid_events_dlq.arn,
      ]
    }]
  })
}

# ----- GitHub Actions OIDC role -----
# Allows the deploy workflow to assume this role without long-lived credentials.

data "aws_caller_identity" "current" {}

# local.github_oidc_provider_arn is defined in locals.tf

resource "aws_iam_role" "github_actions_deploy" {
  name = "auction-kings-staging-github-deploy"

  # Trust policy is managed by Terraform. The role can update its own trust policy because
  # iam:UpdateAssumeRolePolicy is granted via the github_actions_iam inline policy below.
  # The bootstrap step in infra-staging.yml applies that policy first so subsequent runs
  # can reconcile the trust policy without manual AWS console intervention.
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Federated = local.github_oidc_provider_arn
      }
      Action = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringLike = {
          "token.actions.githubusercontent.com:sub" = "repo:PellizzoniCode/auction-kings:*"
        }
        StringEquals = {
          "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })

  # Prevent destroy: deleting this role mid-run invalidates the OIDC session
  # that Terraform itself is using, causing all subsequent AWS calls to fail.
  # Delete this role manually via the AWS console/CLI after the rest of the
  # infrastructure has been torn down.
  lifecycle {
    prevent_destroy = true
  }
}

# -- Terraform state backend (S3 + DynamoDB) --

resource "aws_iam_role_policy" "github_actions_tf_state" {
  name = "terraform-state"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "TerraformStateBackend"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:ListBucket",
        ]
        Resource = [
          "arn:aws:s3:::auction-kings-terraform-state",
          "arn:aws:s3:::auction-kings-terraform-state/staging/*",
        ]
      },
      {
        Sid    = "TerraformStateLocking"
        Effect = "Allow"
        Action = [
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:DeleteItem",
          "dynamodb:DescribeTable",
        ]
        Resource = "arn:aws:dynamodb:${var.aws_region}:${data.aws_caller_identity.current.account_id}:table/auction-kings-terraform-locks"
      }
    ]
  })
}

# -- Networking (VPC, subnets, NAT, SGs) --

resource "aws_iam_role_policy" "github_actions_networking" {
  name = "vpc-networking"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "VPCNetworking"
      Effect = "Allow"
      Action = [
        "ec2:CreateVpc", "ec2:DeleteVpc", "ec2:DescribeVpcs", "ec2:ModifyVpcAttribute", "ec2:DescribeVpcAttribute",
        "ec2:CreateSubnet", "ec2:DeleteSubnet", "ec2:DescribeSubnets",
        "ec2:CreateInternetGateway", "ec2:DeleteInternetGateway", "ec2:AttachInternetGateway", "ec2:DetachInternetGateway", "ec2:DescribeInternetGateways",
        "ec2:AllocateAddress", "ec2:ReleaseAddress", "ec2:DescribeAddresses",
        "ec2:CreateNatGateway", "ec2:DeleteNatGateway", "ec2:DescribeNatGateways",
        "ec2:CreateRouteTable", "ec2:DeleteRouteTable", "ec2:DescribeRouteTables",
        "ec2:CreateRoute", "ec2:DeleteRoute", "ec2:AssociateRouteTable", "ec2:DisassociateRouteTable",
        "ec2:CreateSecurityGroup", "ec2:DeleteSecurityGroup", "ec2:DescribeSecurityGroups", "ec2:DescribeSecurityGroupRules",
        "ec2:AuthorizeSecurityGroupIngress", "ec2:RevokeSecurityGroupIngress",
        "ec2:AuthorizeSecurityGroupEgress", "ec2:RevokeSecurityGroupEgress",
        "ec2:ModifySubnetAttribute",
        "ec2:DescribeAddressesAttribute",
        "ec2:DescribeAvailabilityZones", "ec2:DescribeNetworkInterfaces", "ec2:DescribeAccountAttributes",
        "ec2:CreateTags", "ec2:DeleteTags",
      ]
      Resource = "*"
    }]
  })
}

# -- ALB --

resource "aws_iam_role_policy" "github_actions_alb" {
  name = "load-balancer"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "LoadBalancer"
      Effect = "Allow"
      Action = [
        "elasticloadbalancing:CreateLoadBalancer", "elasticloadbalancing:DeleteLoadBalancer",
        "elasticloadbalancing:DescribeLoadBalancers", "elasticloadbalancing:DescribeLoadBalancerAttributes",
        "elasticloadbalancing:ModifyLoadBalancerAttributes", "elasticloadbalancing:SetSecurityGroups",
        "elasticloadbalancing:CreateTargetGroup", "elasticloadbalancing:DeleteTargetGroup",
        "elasticloadbalancing:DescribeTargetGroups", "elasticloadbalancing:DescribeTargetGroupAttributes",
        "elasticloadbalancing:ModifyTargetGroupAttributes", "elasticloadbalancing:DescribeTargetHealth",
        "elasticloadbalancing:CreateListener", "elasticloadbalancing:DeleteListener",
        "elasticloadbalancing:DescribeListeners", "elasticloadbalancing:DescribeListenerAttributes",
        "elasticloadbalancing:ModifyListener",
        "elasticloadbalancing:AddTags", "elasticloadbalancing:RemoveTags", "elasticloadbalancing:DescribeTags",
      ]
      Resource = "*"
    }]
  })
}

# -- ECS (deploy + Terraform provisioning) --

resource "aws_iam_role_policy" "github_actions_deploy" {
  name = "ecs-deploy"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ECS"
        Effect = "Allow"
        Action = [
          "ecs:CreateCluster", "ecs:DeleteCluster", "ecs:DescribeClusters",
          "ecs:PutClusterCapacityProviders",
          "ecs:CreateService", "ecs:DeleteService", "ecs:DescribeServices", "ecs:UpdateService",
          "ecs:RegisterTaskDefinition", "ecs:DeregisterTaskDefinition",
          "ecs:DescribeTaskDefinition", "ecs:ListTaskDefinitions",
          "ecs:ListTasks", "ecs:DescribeTasks",
          "ecs:TagResource", "ecs:UntagResource", "ecs:ListTagsForResource",
        ]
        Resource = "*"
      },
      {
        Sid    = "PassExecutionRole"
        Effect = "Allow"
        Action = "iam:PassRole"
        Resource = [
          aws_iam_role.ecs_execution.arn,
          aws_iam_role.ecs_task.arn,
        ]
      }
    ]
  })
}

# -- RDS --

resource "aws_iam_role_policy" "github_actions_rds" {
  name = "rds"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "RDS"
      Effect = "Allow"
      Action = [
        "rds:CreateDBInstance", "rds:DeleteDBInstance", "rds:DescribeDBInstances", "rds:ModifyDBInstance",
        "rds:CreateDBSubnetGroup", "rds:DeleteDBSubnetGroup", "rds:DescribeDBSubnetGroups",
        "rds:AddTagsToResource", "rds:RemoveTagsFromResource", "rds:ListTagsForResource",
      ]
      Resource = "*"
    }]
  })
}

# -- ElastiCache --

resource "aws_iam_role_policy" "github_actions_elasticache" {
  name = "elasticache"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "ElastiCache"
      Effect = "Allow"
      Action = [
        "elasticache:CreateReplicationGroup", "elasticache:DeleteReplicationGroup",
        "elasticache:DescribeReplicationGroups", "elasticache:ModifyReplicationGroup",
        "elasticache:CreateCacheSubnetGroup", "elasticache:DeleteCacheSubnetGroup",
        "elasticache:DescribeCacheSubnetGroups", "elasticache:DescribeCacheClusters",
        "elasticache:AddTagsToResource", "elasticache:RemoveTagsFromResource", "elasticache:ListTagsForResource",
      ]
      Resource = "*"
    }]
  })
}

# -- SQS --

resource "aws_iam_role_policy" "github_actions_sqs" {
  name = "sqs"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "SQS"
      Effect = "Allow"
      Action = [
        "sqs:CreateQueue", "sqs:DeleteQueue",
        "sqs:GetQueueAttributes", "sqs:SetQueueAttributes", "sqs:GetQueueUrl",
        "sqs:TagQueue", "sqs:UntagQueue", "sqs:ListQueueTags",
      ]
      Resource = "arn:aws:sqs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:auction-kings-staging-*"
    }]
  })
}

# -- Secrets Manager --

resource "aws_iam_role_policy" "github_actions_secrets" {
  name = "secrets-manager"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid    = "SecretsManager"
      Effect = "Allow"
      Action = [
        "secretsmanager:CreateSecret", "secretsmanager:DeleteSecret",
        "secretsmanager:DescribeSecret", "secretsmanager:GetSecretValue",
        "secretsmanager:PutSecretValue", "secretsmanager:UpdateSecret",
        "secretsmanager:TagResource", "secretsmanager:UntagResource",
        "secretsmanager:GetResourcePolicy", "secretsmanager:PutResourcePolicy", "secretsmanager:DeleteResourcePolicy",
      ]
      Resource = "arn:aws:secretsmanager:${var.aws_region}:${data.aws_caller_identity.current.account_id}:secret:/auction-kings/staging/*"
    }]
  })
}

# -- CloudWatch Logs --

resource "aws_iam_role_policy" "github_actions_logs" {
  name = "cloudwatch-logs"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "CloudWatchLogsManage"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup", "logs:DeleteLogGroup",
          "logs:PutRetentionPolicy", "logs:DeleteRetentionPolicy",
          "logs:TagLogGroup", "logs:UntagLogGroup", "logs:ListTagsLogGroup",
          "logs:TagResource", "logs:UntagResource", "logs:ListTagsForResource",
        ]
        Resource = "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:/ecs/auction-kings-staging-*"
      },
      {
        # DescribeLogGroups is a list-level action; AWS evaluates it against
        # arn:...:log-group::log-stream: (empty name), so it requires Resource = "*".
        Sid      = "CloudWatchLogsDescribe"
        Effect   = "Allow"
        Action   = ["logs:DescribeLogGroups"]
        Resource = "*"
      }
    ]
  })
}

# -- IAM roles managed by Terraform --

resource "aws_iam_role_policy" "github_actions_iam" {
  name = "iam-roles"
  role = aws_iam_role.github_actions_deploy.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "IAMRolesAndPolicies"
        Effect = "Allow"
        Action = [
          "iam:CreateRole", "iam:DeleteRole", "iam:GetRole", "iam:UpdateRole",
          "iam:TagRole", "iam:UntagRole", "iam:ListRoleTags",
          "iam:PutRolePolicy", "iam:GetRolePolicy", "iam:DeleteRolePolicy", "iam:ListRolePolicies",
          "iam:AttachRolePolicy", "iam:DetachRolePolicy", "iam:ListAttachedRolePolicies",
          "iam:ListInstanceProfilesForRole", "iam:PassRole",
          "iam:UpdateAssumeRolePolicy",
        ]
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/auction-kings-staging-*"
      },
      {
        Sid    = "IAMOIDCProvider"
        Effect = "Allow"
        Action = [
          "iam:CreateOpenIDConnectProvider", "iam:DeleteOpenIDConnectProvider",
          "iam:GetOpenIDConnectProvider", "iam:TagOpenIDConnectProvider",
          "iam:UntagOpenIDConnectProvider",
        ]
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"
      },
      {
        Sid      = "IAMOIDCProviderList"
        Effect   = "Allow"
        Action   = "iam:ListOpenIDConnectProviders"
        Resource = "*"
      },
      {
        Sid      = "ServiceLinkedRoles"
        Effect   = "Allow"
        Action   = "iam:CreateServiceLinkedRole"
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/aws-service-role/*"
        Condition = {
          StringLike = {
            "iam:AWSServiceName" = [
              "ecs.amazonaws.com",
              "elasticache.amazonaws.com"
            ]
          }
        }
      }
    ]
  })
}
