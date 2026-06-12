#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
JOB_NAME="${JOB_NAME:-monthly-sales-report-chaos}"
RUNS="${RUNS:-30}"
INTERVAL_SECONDS="${INTERVAL_SECONDS:-0.2}"

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

json_value() {
  key="$1"
  json="$2"
  case "${json}" in
    *\"${key}\":\"*)
      value="${json#*\"${key}\":\"}"
      value="${value%%\"*}"
      printf '%s' "${value}"
      ;;
    *)
      printf ''
      ;;
  esac
}

echo "Generate load with:"
echo "  kubectl -n flowforge get hpa worker -w"
echo
echo "Triggering ${RUNS} ${JOB_NAME} runs against ${BASE_URL}..."

i=1
while [ "${i}" -le "${RUNS}" ]; do
  response="$(curl --max-time 10 -fsS -X POST "${BASE_URL}/api/jobs/${JOB_NAME}/run")"
  run_id="$(json_value "runId" "${response}")"

  if [ -z "${run_id}" ]; then
    echo "Could not parse runId from response: ${response}"
    exit 1
  fi

  echo "${i}/${RUNS}: ${run_id}"
  i=$((i + 1))
  sleep_seconds "${INTERVAL_SECONDS}"
done

echo "Load submitted. Keep watching HPA and worker pods:"
echo "  kubectl -n flowforge get hpa worker -w"
echo "  kubectl -n flowforge get pods -l app=worker -w"
