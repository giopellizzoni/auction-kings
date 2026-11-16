#!/usr/bin/env bash
# Creates a ready-to-use account in the local Cognito emulator (Moto, docker-compose.infra.yml).
#
#   ./scripts/cognito-local-signup.sh <email> <password> [display-name] [group]
#
# Does what a user + staging Cognito do together: sign-up (the public call a frontend makes), confirm
# (real Cognito emails a code; locally we confirm as admin), then join the group (staging's
# post-confirmation Lambda adds every new account to Bidder; Moto does not run triggers). Prints the
# access token, so `TOKEN=$(./scripts/cognito-local-signup.sh ...)` gives you a token to call the API with.
#
# Requires the AWS CLI v2. Moto accepts any credentials, so dummy ones are used if none are set.
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <email> <password> [display-name] [group: Bidder|Admin|none]" >&2
  exit 2
fi

EMAIL="$1"
PASSWORD="$2"
NAME="${3:-${EMAIL%@*}}"
GROUP="${4:-Bidder}"

# Same pool and app client as appsettings.Development.json (Cognito section) and docker/moto-init.sh.
ENDPOINT="${COGNITO_ENDPOINT:-http://localhost:5555}"
POOL="us-east-1_d85380f7cde7bc8c1b0ca2ed3ee5ddc1affc77945b39d"
CLIENT="123b1f3863b67d1a3e89103c68"

export AWS_ACCESS_KEY_ID="${AWS_ACCESS_KEY_ID:-testing}"
export AWS_SECRET_ACCESS_KEY="${AWS_SECRET_ACCESS_KEY:-testing}"
export AWS_DEFAULT_REGION="us-east-1"
export AWS_PAGER=""
cognito() { aws cognito-idp --endpoint-url "$ENDPOINT" "$@"; }

if ! cognito describe-user-pool --user-pool-id "$POOL" > /dev/null 2>&1; then
  echo "The local user pool is missing. Start the infra (cd docker && docker compose -f docker-compose.infra.yml up -d)," >&2
  echo "or re-seed a restarted Moto with: cd docker && docker compose -f docker-compose.infra.yml up moto-init" >&2
  exit 1
fi

cognito sign-up --client-id "$CLIENT" --username "$EMAIL" --password "$PASSWORD" \
  --user-attributes Name=email,Value="$EMAIL" Name=name,Value="$NAME" > /dev/null
cognito admin-confirm-sign-up --user-pool-id "$POOL" --username "$EMAIL"

if [ "$GROUP" != "none" ]; then
  cognito admin-add-user-to-group --user-pool-id "$POOL" --username "$EMAIL" --group-name "$GROUP"
fi

echo "Created $EMAIL (group: $GROUP)" >&2

cognito initiate-auth --client-id "$CLIENT" --auth-flow USER_PASSWORD_AUTH \
  --auth-parameters "USERNAME=$EMAIL,PASSWORD=$PASSWORD" \
  --query AuthenticationResult.AccessToken --output text
