#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <image>" >&2
  exit 2
fi

image="$1"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/mynovelbuilder-image-audit.XXXXXX")"
container_name="mynovelbuilder-image-audit-${RANDOM}"
file_listing="$work_dir/files.txt"
metadata="$work_dir/metadata.txt"

cleanup() {
  docker rm --force "${container_name}" > /dev/null 2>&1 || true
  rm -rf -- "$work_dir"
}
trap cleanup EXIT

fail() {
  printf 'Release image audit failed: %s\n' "$1" >&2
  exit 1
}

docker image inspect "$image" > /dev/null
docker create --name "$container_name" "$image" > /dev/null
docker export "$container_name" | tar --list --file=- > "$file_listing"

forbidden_path_pattern='(^|/)(AppData|app\.db|integrations\.json|appsettings\.Development\.json)(/|$)|(^|/)[^/]+\.db(-shm|-wal)?$|\.map$'
if grep --extended-regexp --line-number "$forbidden_path_pattern" "$file_listing"; then
  fail 'the final filesystem contains development, user-data, database, credential, or source-map files'
fi

docker image inspect \
  --format '{{range .Config.Env}}{{println .}}{{end}}{{range $key, $value := .Config.Labels}}{{println $key "=" $value}}{{end}}' \
  "$image" > "$metadata"
if grep --extended-regexp --ignore-case --line-number \
  '(api[_-]?key|access[_-]?token|secret|password)=' \
  "$metadata"; then
  fail 'the image configuration contains a credential-like environment variable or label'
fi

if docker run --rm --entrypoint /bin/sh "$image" -c \
  'grep --recursive --binary-files=text --fixed-strings --quiet "/home/scar/" /app'; then
  fail 'the final application contains a maintainer-specific absolute path'
fi

echo "Release image audit passed for ${image}."
