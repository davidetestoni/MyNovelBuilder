#!/usr/bin/env bash

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <image>" >&2
  exit 2
fi

image="$1"
suffix="${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-0}-${RANDOM}"
container_name="mynovelbuilder-smoke-${suffix}"
volume_name="mynovelbuilder-smoke-${suffix}"

cleanup() {
  docker rm --force "${container_name}" > /dev/null 2>&1 || true
  docker volume rm "${volume_name}" > /dev/null 2>&1 || true
}
trap cleanup EXIT

wait_until_healthy() {
  local state
  local health

  for _attempt in $(seq 1 120); do
    state="$(docker inspect --format '{{.State.Status}}' "${container_name}" 2>/dev/null || true)"
    health="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}missing{{end}}' "${container_name}" 2>/dev/null || true)"

    if [[ "${state}" == "running" && "${health}" == "healthy" ]]; then
      return 0
    fi
    if [[ "${state}" == "exited" || "${state}" == "dead" ]]; then
      break
    fi
    sleep 1
  done

  docker inspect "${container_name}" || true
  docker logs "${container_name}" || true
  echo "Container did not become healthy." >&2
  return 1
}

start_container() {
  docker run \
    --detach \
    --name "${container_name}" \
    --mount "type=volume,source=${volume_name},target=/data" \
    "${image}" > /dev/null
  wait_until_healthy
}

docker image inspect "${image}" > /dev/null
docker volume create "${volume_name}" > /dev/null

start_container

if [[ "$(docker exec "${container_name}" id -u)" == "0" ]]; then
  echo "Container is running as root." >&2
  exit 1
fi
docker exec "${container_name}" \
  curl --fail --silent --show-error --output /dev/null \
  http://127.0.0.1:8080/health/ready
docker exec "${container_name}" \
  curl --fail --silent --show-error --output /dev/null \
  http://127.0.0.1:8080/
docker exec "${container_name}" sh -c \
  'printf "%s\n" "persistent smoke marker" > /data/.release-smoke'

docker rm --force "${container_name}" > /dev/null
start_container
docker exec "${container_name}" test -f /data/.release-smoke

echo "Docker smoke test passed for ${image}."
