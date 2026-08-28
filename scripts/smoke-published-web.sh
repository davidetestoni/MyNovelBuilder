#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
project="$repo_root/src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj"
port="${MYNOVELBUILDER_SMOKE_PORT:-15113}"
base_url="http://127.0.0.1:$port"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/mynovelbuilder-smoke.XXXXXX")"
publish_dir="$work_dir/publish"
data_dir="$work_dir/data"
responses_dir="$work_dir/responses"
log_file="$work_dir/application.log"
app_pid=""

cleanup() {
  if [[ -n "$app_pid" ]] && kill -0 "$app_pid" 2>/dev/null; then
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi

  rm -rf -- "$work_dir"
}

fail() {
  printf 'Smoke test failed: %s\n' "$1" >&2
  if [[ -f "$log_file" ]]; then
    printf '\nApplication log:\n' >&2
    sed -n '1,240p' "$log_file" >&2
  fi
  exit 1
}

request() {
  local path="$1"
  local expected_status="$2"
  local output_file="$3"
  local actual_status

  actual_status="$(curl --silent --show-error --max-time 10 \
    --output "$output_file" \
    --write-out '%{http_code}' \
    "$base_url$path")" || fail "request to $path could not be completed"

  if [[ "$actual_status" != "$expected_status" ]]; then
    fail "$path returned HTTP $actual_status; expected $expected_status"
  fi
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

for required_command in dotnet npm curl; do
  command -v "$required_command" >/dev/null 2>&1 \
    || fail "required command '$required_command' is not installed"
done

if [[ ! "$port" =~ ^[0-9]+$ ]] || (( port < 1 || port > 65535 )); then
  fail "MYNOVELBUILDER_SMOKE_PORT must be an integer between 1 and 65535"
fi

mkdir -p "$publish_dir" "$data_dir/static" "$responses_dir"
printf 'MyNovelBuilder static smoke test\n' >"$data_dir/static/smoke.txt"

printf 'Restoring locked .NET dependencies...\n'
dotnet restore "$project" --locked-mode

printf 'Publishing the combined ASP.NET Core and Angular application...\n'
dotnet publish "$project" \
  --configuration Release \
  --no-restore \
  --output "$publish_dir"

printf 'Starting published application at %s...\n' "$base_url"
(
  cd -- "$publish_dir"
  DataFolder="$data_dir" \
    ASPNETCORE_URLS="$base_url" \
    Logging__LogLevel__Default=Warning \
    dotnet MyNovelBuilder.WebApi.dll >"$log_file" 2>&1
) &
app_pid=$!

started=false
for _ in {1..120}; do
  if curl --silent --fail --max-time 2 "$base_url/health/live" >/dev/null 2>&1; then
    started=true
    break
  fi

  if ! kill -0 "$app_pid" 2>/dev/null; then
    fail "the application exited before becoming healthy"
  fi

  sleep 0.5
done

[[ "$started" == true ]] || fail "the application did not become healthy within 60 seconds"

request "/" "200" "$responses_dir/root.html"
request "/novels" "200" "$responses_dir/angular-route.html"
cmp --silent "$responses_dir/root.html" "$responses_dir/angular-route.html" \
  || fail "the Angular client route did not return index.html"
grep --quiet '<app-root></app-root>' "$responses_dir/root.html" \
  || fail "the root response is not the Angular application"

request "/api/does-not-exist" "404" "$responses_dir/missing-api.txt"
if cmp --silent "$responses_dir/root.html" "$responses_dir/missing-api.txt"; then
  fail "an unknown API route was swallowed by the SPA fallback"
fi

request "/static/smoke.txt" "200" "$responses_dir/static.txt"
cmp --silent "$data_dir/static/smoke.txt" "$responses_dir/static.txt" \
  || fail "the static response did not contain the user-owned test file"

request "/health/live" "200" "$responses_dir/live.txt"
request "/health/ready" "200" "$responses_dir/ready.txt"
grep --quiet '^Healthy$' "$responses_dir/live.txt" \
  || fail "the liveness endpoint did not report Healthy"
grep --quiet '^Healthy$' "$responses_dir/ready.txt" \
  || fail "the readiness endpoint did not report Healthy"

printf 'Published web smoke test passed.\n'
