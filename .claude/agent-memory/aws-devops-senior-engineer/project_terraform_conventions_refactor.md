---
name: terraform-conventions-refactor
description: Terraform naming conventions refactor completed for infra/terraform/staging/ — file split, object variables, locals consolidation
metadata:
  type: project
---

Completed a structural refactor of `infra/terraform/staging/` to comply with `.claude/rules/terraform-naming-conventions.md`.

**Why:** Enforce consistent naming, grouping, and file structure before further infrastructure work on this environment.

**How to apply:** All future Terraform work in this directory must follow these conventions. The rule file at `.claude/rules/terraform-naming-conventions.md` is the canonical reference.

Key changes made:
- Monolithic `vpc.tf` split into 7 files: `vpc.tf`, `vpc.internet-gateway.tf`, `vpc.public-subnets.tf`, `vpc.private-subnets.tf`, `vpc.nat-gateway.tf`, `vpc.public-route-table.tf`, `vpc.private-route-table.tf`
- Security groups extracted into 5 files: `security-group.alb.tf`, `security-group.api.tf`, `security-group.worker.tf`, `security-group.rds.tf`, `security-group.elasticache.tf`
- `aws_vpc.main` → `aws_vpc.this`, `aws_internet_gateway.main` → `aws_internet_gateway.this`, `aws_nat_gateway.main` → `aws_nat_gateway.this`
- All `locals {}` blocks consolidated into `locals.tf` (including `github_oidc_provider_arn` from `iam.tf` and db/redis connection strings from `ecs.api.tf`)
- `locals.tf` defines: `name_prefix`, `common_tags` (empty — provider `default_tags` handles Project/Environment/ManagedBy), `azs`, `public_cidrs`, `private_cidrs`, `github_oidc_provider_arn`, `db_connection_string`, `redis_connection_string`
- `variables.tf` rewritten: scalar vars grouped into 4 objects — `vpc` (cidr, public_subnets, private_subnets), `rds` (name, username), `jwt` (issuer, audience), `ecs` (api_image, worker_image, desired_count); `aws_region` kept as scalar with `nullable = false`; all `default =` removed
- `terraform.tfvars.example` rewritten to match new object-typed variables
- Tags updated to `merge(local.common_tags, { Name = "${local.name_prefix}-..." })` in all network/security-group files and in `alb.tf`, `ecs.api.tf`, `ecs.worker.tf`, `rds.tf`, `elasticache.tf`

[[terraform-naming-conventions]]
