#!/usr/bin/env bash
# Moves the GitHub Actions deploy role in and out of the staging Terraform state.
#
# The role (auction-kings-staging-github-deploy) is the identity every staging workflow assumes through
# GitHub OIDC, so a teardown must never delete it: Terraform would revoke its own permissions halfway
# through the destroy (and iam.tf marks it prevent_destroy, which makes a plain `terraform destroy` fail).
#
#   forget  Remove the role and its inline policies from the state, leaving them in AWS.
#           Run before `terraform destroy`.
#   adopt   Import them back when the state does not hold the role (after a teardown).
#           Run before the first apply that follows a teardown. A no-op when the role is already tracked.
#
# Run from infra/terraform/staging after `terraform init`. Extra arguments are passed to
# `terraform import` (CI passes -var-file=terraform.staging.tfvars).
set -euo pipefail

ROLE_ADDRESS="aws_iam_role.github_actions_deploy"
ROLE_NAME="auction-kings-staging-github-deploy"

# Terraform address suffix -> inline policy name (see iam.tf).
declare -A POLICIES=(
  [github_actions_tf_state]="terraform-state"
  [github_actions_networking]="vpc-networking"
  [github_actions_alb]="load-balancer"
  [github_actions_deploy]="ecs-deploy"
  [github_actions_rds]="rds"
  [github_actions_elasticache]="elasticache"
  [github_actions_sqs]="sqs"
  [github_actions_secrets]="secrets-manager"
  [github_actions_logs]="cloudwatch-logs"
  [github_actions_cognito]="cognito"
  [github_actions_lambda]="lambda"
  [github_actions_iam]="iam-roles"
)

in_state() {
  terraform state list 2>/dev/null | grep -qxF "$1"
}

forget() {
  local addresses=()
  in_state "$ROLE_ADDRESS" && addresses+=("$ROLE_ADDRESS")
  for key in "${!POLICIES[@]}"; do
    in_state "aws_iam_role_policy.${key}" && addresses+=("aws_iam_role_policy.${key}")
  done

  if [ "${#addresses[@]}" -eq 0 ]; then
    echo "Deploy role is not in the state; nothing to forget."
    return
  fi

  terraform state rm "${addresses[@]}"
  echo "Removed ${#addresses[@]} deploy-role addresses from the state; they stay in AWS."
}

adopt() {
  if in_state "$ROLE_ADDRESS"; then
    echo "Deploy role is already in the state; nothing to adopt."
    return
  fi

  terraform import -input=false "$@" "$ROLE_ADDRESS" "$ROLE_NAME"
  for key in "${!POLICIES[@]}"; do
    # A policy added to iam.tf after the last apply does not exist in AWS yet; the apply creates it.
    terraform import -input=false "$@" "aws_iam_role_policy.${key}" "${ROLE_NAME}:${POLICIES[$key]}" ||
      echo "::warning::Inline policy ${POLICIES[$key]} not imported (missing in AWS?); the apply will create it."
  done
}

case "${1:-}" in
  forget) shift; forget ;;
  adopt) shift; adopt "$@" ;;
  *) echo "usage: $0 forget | adopt [terraform import args]" >&2; exit 2 ;;
esac
