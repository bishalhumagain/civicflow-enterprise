#!/usr/bin/env bash
set -euo pipefail

: "${KEYCLOAK_BASE_URL:=http://localhost:8080}"
: "${KEYCLOAK_REALM:=civicflow}"
: "${KEYCLOAK_TEST_USERNAME:?Set KEYCLOAK_TEST_USERNAME for a disposable local user}"
: "${KEYCLOAK_TEST_PASSWORD:?Set KEYCLOAK_TEST_PASSWORD for a disposable local user}"
: "${CIVICFLOW_API_BASE_URL:=https://localhost:7248}"
: "${CIVICFLOW_ORGANIZATION_ID:?Set CIVICFLOW_ORGANIZATION_ID for an application-user membership}"

token_response="$(curl --fail-with-body --silent --show-error \
  --request POST \
  --header 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode grant_type=password \
  --data-urlencode client_id=civicflow-staff \
  --data-urlencode username="$KEYCLOAK_TEST_USERNAME" \
  --data-urlencode password="$KEYCLOAK_TEST_PASSWORD" \
  "$KEYCLOAK_BASE_URL/realms/$KEYCLOAK_REALM/protocol/openid-connect/token")"

access_token="$(jq -er '.access_token' <<<"$token_response")"
token_payload="$(cut -d. -f2 <<<"$access_token" | tr '_-' '/+' | base64 -d 2>/dev/null || true)"

test -n "$token_payload"
test "$(jq -r '.azp' <<<"$token_payload")" = "civicflow-staff"
test "$(jq -r '.iss' <<<"$token_payload")" = "$KEYCLOAK_BASE_URL/realms/$KEYCLOAK_REALM"

curl --fail-with-body --silent --show-error \
  --header "Authorization: Bearer $access_token" \
  --header "X-Organization-Id: $CIVICFLOW_ORGANIZATION_ID" \
  "$CIVICFLOW_API_BASE_URL/api/health/ready" >/dev/null

echo "Keycloak token validation and API readiness request succeeded without printing the token."
