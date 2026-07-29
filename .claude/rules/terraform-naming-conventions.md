---
name: terraform-naming-conventions
description: Terraform naming conventions and file structure rules for the AuctionKings infrastructure. Apply these rules every time Terraform code is written, reviewed, or refactored.
---

# Terraform Naming Conventions

This document defines the mandatory naming conventions and structural rules for all Terraform code in the `infra/` directory. These rules apply to every agent writing or reviewing infrastructure code.

---

## 1. General Identifiers

- **Use underscores (`_`)** as word separators in all Terraform identifiers (resource names, variable names, output names, local names). Never use dashes (`-`).
- **Use lowercase letters and numbers only.** No uppercase in Terraform identifiers.
- **Note:** Cloud resource names (e.g., ECS service names, S3 bucket names) may require dashes due to cloud provider restrictions — that is acceptable in the `name` tag/argument, but the Terraform identifier itself must still use underscores.

```hcl
# Good
resource "aws_security_group" "alb_public" {}
variable "vpc" {}

# Bad
resource "aws_security_group" "alb-public" {}
variable "VPC" {}
```

---

## 2. Resource and Data Source Names

### 2.1 Avoid Redundancy
Do not repeat the resource type in the resource name — the block type already provides that context.

```hcl
# Good
resource "aws_route_table" "public" {}
resource "aws_security_group" "alb" {}

# Bad
resource "aws_route_table" "public_route_table" {}
resource "aws_security_group" "alb_security_group" {}
```

### 2.2 Use `this` When Context is Unambiguous
When a module or file contains a single resource of a given type and no more descriptive name adds value, use `this`.

```hcl
resource "aws_vpc" "this" {}
resource "aws_internet_gateway" "this" {}
```

### 2.3 Use Singular Nouns
Resource names should be singular, even when the resource manages multiple underlying objects.

```hcl
# Good
resource "aws_subnet" "public" {}

# Bad
resource "aws_subnet" "publics" {}
```

### 2.4 Argument Order Inside a Resource Block
Follow this order to ensure consistency and readability:

1. `count` / `for_each` (separated by a blank line from the rest)
2. Required arguments
3. Optional arguments
4. `tags` (if supported)
5. `depends_on` (separated by a blank line)
6. `lifecycle` (separated by a blank line)

```hcl
resource "aws_instance" "this" {
  count = var.ecs.desired_count

  ami           = data.aws_ami.this.id
  instance_type = "t3.micro"

  tags = local.common_tags

  depends_on = [aws_iam_role_policy_attachment.this]

  lifecycle {
    create_before_destroy = true
  }
}
```

### 2.5 Boolean Conditions in `count` / `for_each`
Prefer explicit boolean expressions over `length()` checks.

```hcl
# Good
count = var.vpc.enable_nat_gateway ? 1 : 0

# Bad
count = length(var.vpc.public_subnets) > 0 ? 1 : 0
```

---

## 3. Variable Naming and Structure

### 3.1 Use Object Types — Group by Resource Domain
**Do not declare standalone scalar variables when multiple related inputs belong to the same resource or concept.** Group them into a single object-typed variable named after the resource domain.

```hcl
# Good — one object variable per domain
variable "vpc" {
  description = "VPC configuration."
  type = object({
    cidr             = string
    public_subnets   = list(string)
    private_subnets  = list(string)
    enable_nat_gateway = bool
    single_nat_gateway = bool
  })
}

variable "rds" {
  description = "RDS PostgreSQL configuration."
  type = object({
    name              = string
    username          = string
    instance_class    = string
    allocated_storage = number
    multi_az          = bool
  })
}

variable "ecs" {
  description = "ECS Fargate service configuration."
  type = object({
    api_image     = string
    worker_image  = string
    desired_count = number
    cpu           = number
    memory        = number
  })
}

# Bad — scattered standalone variables
variable "vpc_cidr" {}
variable "public_subnets" {}
variable "private_subnets" {}
variable "db_name" {}
variable "db_username" {}
```

### 3.2 Use Plural Forms for Collections
Variable names referring to `list(...)` or `map(...)` types must be plural.

```hcl
type = object({
  public_subnets  = list(string)   # plural — list
  private_subnets = list(string)   # plural — list
  availability_zones = list(string)
})
```

### 3.3 Key Order Inside a Variable Block
Always declare variable attributes in this order:

1. `description`
2. `type`
3. `validation` (if needed)
4. `nullable` (if needed)

**Do not use `default`.** All default values live in the separate values file (see Section 5).

```hcl
variable "vpc" {
  description = "VPC configuration."
  type = object({
    cidr            = string
    public_subnets  = list(string)
    private_subnets = list(string)
  })
}
```

### 3.4 Avoid Double Negatives
Name boolean flags with positive framing.

```hcl
# Good
enable_nat_gateway = bool
encryption_enabled = bool

# Bad
nat_gateway_disabled = bool
encryption_disabled  = bool
```

### 3.5 Set `nullable = false` When Null is Not Acceptable

```hcl
variable "aws_region" {
  description = "AWS region."
  type        = string
  nullable    = false
}
```

---

## 4. Output Naming

Follow the pattern `{name}_{type}_{attribute}`:

- `{name}` — the resource or data source identifier
- `{type}` — the resource type without the provider prefix
- `{attribute}` — the exported attribute

```hcl
# Good
output "vpc_vpc_id" { value = aws_vpc.this.id }
output "alb_lb_dns_name" { value = aws_lb.this.dns_name }
output "rds_db_instance_endpoint" { value = aws_db_instance.this.endpoint }
output "elasticache_replication_group_primary_endpoint" {
  value = aws_elasticache_replication_group.this.primary_endpoint_address
}

# Bad
output "vpc_id" {}
output "alb_dns" {}
```

Use plural names for outputs that return lists or maps. Always include `description`. Prefer `try()` to avoid errors on optional attributes.

---

## 5. File Naming

### 5.1 Pattern: `{resource}.tf` and `{resource}.{sub-resource}.tf`

Every `.tf` file is named after the primary AWS resource it manages. When a resource has multiple logical sub-components that would make a single file unwieldy, split them using a dot-separated sub-resource name.

```
vpc.tf                        # VPC resource itself
vpc.public-subnets.tf         # Public subnets and their associations
vpc.private-subnets.tf        # Private subnets
vpc.internet-gateway.tf       # Internet Gateway
vpc.nat-gateway.tf            # NAT Gateway(s)
vpc.public-route-table.tf     # Public route table and associations
vpc.private-route-table.tf    # Private route table(s) and associations

rds.tf                        # RDS instance
rds.subnet-group.tf           # DB subnet group
rds.parameter-group.tf        # DB parameter group

ecs.api.tf                    # ECS service and task definition for API
ecs.worker.tf                 # ECS service and task definition for Worker
ecs.cluster.tf                # Shared ECS cluster

alb.tf                        # Application Load Balancer
alb.target-group.tf           # Target groups
alb.listener.tf               # Listeners and rules

iam.tf                        # IAM roles and policies
iam.oidc.tf                   # OIDC provider and GitHub Actions role

cloudwatch.api.tf             # CloudWatch log group for API
cloudwatch.worker.tf          # CloudWatch log group for Worker

elasticache.tf                # ElastiCache replication group
elasticache.subnet-group.tf   # ElastiCache subnet group

sqs.tf                        # SQS queues and DLQ
secrets.tf                    # Secrets Manager secrets
```

### 5.2 Standard Root Files
Every Terraform module/environment must contain these files:

| File | Purpose |
|------|---------|
| `main.tf` | Provider configuration, Terraform backend, and required_providers block |
| `variables.tf` | All `variable` blocks — no `default` values here |
| `outputs.tf` | All `output` blocks |
| `locals.tf` | All `locals` blocks (common tags, computed values) |
| `terraform.tfvars` | **Default / baseline variable values** (replaces `default` in variable blocks) |
| `terraform.staging.tfvars` | Environment-specific overrides for staging |
| `terraform.production.tfvars` | Environment-specific overrides for production |

### 5.3 No `default` in `variables.tf` — Use `terraform.tfvars`
Variable values are never set via `default` in `variables.tf`. All baseline values live in `terraform.tfvars`. This makes every variable's actual value visible in one place and avoids hidden defaults that differ between environments.

```hcl
# variables.tf — types and descriptions only, no defaults
variable "aws_region" {
  description = "AWS region for all resources."
  type        = string
  nullable    = false
}

variable "vpc" {
  description = "VPC configuration."
  type = object({
    cidr            = string
    public_subnets  = list(string)
    private_subnets = list(string)
  })
}
```

```hcl
# terraform.tfvars — all baseline values
aws_region = "us-east-1"

vpc = {
  cidr            = "10.0.0.0/16"
  public_subnets  = ["10.0.1.0/24", "10.0.2.0/24"]
  private_subnets = ["10.0.10.0/24", "10.0.11.0/24"]
}
```

---

## 6. Local Values (`locals`)

- Place all locals in `locals.tf`.
- Use locals to compute values reused in more than two places, and for `common_tags`.
- Name locals with the same underscore convention as variables.

```hcl
locals {
  name_prefix = "auction-kings-${var.environment}"

  common_tags = {
    Project     = "AuctionKings"
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}
```

---

## 7. Module Naming

- Module source directory names use **dashes** (filesystem convention).
- Module call identifiers in `.tf` files use **underscores** (Terraform convention).

```hcl
# Module call — underscore identifier
module "vpc" {
  source = "./modules/vpc-network"   # directory name uses dashes
}
```

---

## 8. Tags

Every AWS resource that supports tags must include at minimum:

```hcl
tags = merge(local.common_tags, {
  Name = "${local.name_prefix}-<resource-descriptor>"
})
```

Tag keys use `PascalCase`. The `Name` tag value uses `kebab-case`.

---

## Quick Reference Cheat-Sheet

| Concern | Rule |
|---------|------|
| Identifier separator | `_` (underscore) |
| Case | lowercase |
| Resource type in name | Never repeat it |
| Single resource of type | Name it `this` |
| Noun form | Singular (except collections) |
| Variables | Object type grouped by domain |
| Variable defaults | `terraform.tfvars` only, never `default =` |
| Variable key order | description → type → validation → nullable |
| Output pattern | `{name}_{type}_{attribute}` |
| File pattern | `{resource}.tf` / `{resource}.{sub-resource}.tf` |
| Boolean flags | Positive framing (`enabled`, not `disabled`) |
| Locals file | `locals.tf` |
| Values file | `terraform.tfvars` |
