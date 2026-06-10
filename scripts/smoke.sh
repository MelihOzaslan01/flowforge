#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"

print_logs() {
  if command -v docker >/dev/null 2>&1; then
    docker compose logs --tail=200 controlplane worker
  elif command -v docker.exe >/dev/null 2>&1; then
    docker.exe compose logs --tail=200 controlplane worker
  else
    echo "Docker CLI not found; cannot print service logs."
  fi
}

sleep_seconds() {
  if command -v sleep >/dev/null 2>&1; then
    sleep "$1"
  elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command "Start-Sleep -Seconds $1" >/dev/null
  else
    echo "sleep command not found."
    exit 1
  fi
}

echo "Waiting for ControlPlane at ${BASE_URL}..."
ready=false
attempt=1
while [ "${attempt}" -le 60 ]; do
  if curl --max-time 2 -fsS "${BASE_URL}/healthz" >/dev/null 2>&1; then
    ready=true
    break
  fi
  attempt=$((attempt + 1))
  sleep_seconds 1
done

if [ "${ready}" != "true" ]; then
  echo "ControlPlane did not become healthy."
  print_logs
  exit 1
fi

echo "Triggering monthly-sales-report..."
response="$(curl --max-time 10 -fsS -X POST "${BASE_URL}/api/jobs/monthly-sales-report/run")"
case "${response}" in
  *\"runId\":\"*)
    run_id="${response#*\"runId\":\"}"
    run_id="${run_id%%\"*}"
    ;;
  *)
    run_id=""
    ;;
esac

if [ -z "${run_id}" ]; then
  echo "Could not parse runId from response: ${response}"
  print_logs
  exit 1
fi

echo "Run id: ${run_id}"
attempt=1
while [ "${attempt}" -le 60 ]; do
  run_json="$(curl --max-time 5 -fsS "${BASE_URL}/api/runs/${run_id}" || true)"
  case "${run_json}" in
    *\"status\":\"*)
      status="${run_json#*\"status\":\"}"
      status="${status%%\"*}"
      ;;
    *)
      status=""
      ;;
  esac
  echo "Status: ${status:-unknown}"

  if [ "${status}" = "Completed" ]; then
    exit 0
  fi

  attempt=$((attempt + 1))
  sleep_seconds 1
done

echo "Run did not complete in 60 seconds."
print_logs
exit 1
