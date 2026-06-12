#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5001}"
NAMESPACE="${NAMESPACE:-flowforge}"
JOB_NAME="${JOB_NAME:-monthly-sales-report}"
KILL_DELAY_SECONDS="${KILL_DELAY_SECONDS:-8}"
STEP_NO="${STEP_NO:-2}"
RUN_TIMEOUT_SECONDS="${RUN_TIMEOUT_SECONDS:-150}"

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
    *\"${key}\":*)
      value="${json#*\"${key}\":}"
      value="${value%%,*}"
      value="${value%%\}*}"
      printf '%s' "${value}"
      ;;
    *)
      printf ''
      ;;
  esac
}

kubectl_cmd() {
  if command -v kubectl >/dev/null 2>&1; then
    kubectl "$@"
  elif command -v kubectl.exe >/dev/null 2>&1; then
    kubectl.exe "$@"
  else
    echo "kubectl CLI not found."
    exit 1
  fi
}

psql_value() {
  database="$1"
  sql="$2"
  kubectl_cmd -n "${NAMESPACE}" exec postgres-0 -- \
    psql -U postgres -d "${database}" -Atc "${sql}"
}

print_timeline() {
  run_id="$1"
  echo "Timeline for ${run_id}:"
  psql_value worker_db \
    "select step_no || ' | ' || status || ' | attempt=' || attempt_count || ' | worker=' || worker_id || ' | started=' || to_char(started_at at time zone 'utc', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') || ' | finished=' || coalesce(to_char(finished_at at time zone 'utc', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"'), '') || ' | error=' || coalesce(error, '') from job_step_runs where run_id = '${run_id}' order by started_at, step_no, attempt_count;"
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
  exit 1
fi

echo "Triggering ${JOB_NAME}..."
response="$(curl --max-time 10 -fsS -X POST "${BASE_URL}/api/jobs/${JOB_NAME}/run")"
run_id="$(json_value "runId" "${response}")"

if [ -z "${run_id}" ]; then
  echo "Could not parse runId from response: ${response}"
  exit 1
fi

echo "Run id: ${run_id}"
echo "Waiting ${KILL_DELAY_SECONDS}s before looking for running step ${STEP_NO}..."
sleep_seconds "${KILL_DELAY_SECONDS}"

worker_pod=""
attempt=1
while [ "${attempt}" -le 30 ]; do
  worker_pod="$(psql_value worker_db "select worker_id from job_step_runs where run_id = '${run_id}' and step_no = ${STEP_NO} and status = 'Running' order by started_at desc limit 1;" | tr -d '\r')"
  if [ -n "${worker_pod}" ]; then
    break
  fi

  echo "Step ${STEP_NO} is not Running yet; retry ${attempt}/30..."
  attempt=$((attempt + 1))
  sleep_seconds 1
done

if [ -z "${worker_pod}" ]; then
  echo "Could not find a Running step ${STEP_NO} worker for run ${run_id}."
  print_timeline "${run_id}"
  exit 1
fi

echo "Deleting worker pod ${worker_pod} while step ${STEP_NO} is Running..."
kubectl_cmd -n "${NAMESPACE}" delete pod "${worker_pod}" --wait=false

echo "Polling run ${run_id} until terminal status..."
status=""
failed_step=""
attempt=1
while [ "${attempt}" -le "${RUN_TIMEOUT_SECONDS}" ]; do
  run_json="$(curl --max-time 5 -fsS "${BASE_URL}/api/runs/${run_id}" || true)"
  status="$(json_value "status" "${run_json}")"
  failed_step="$(json_value "failedStep" "${run_json}")"
  echo "Status: ${status:-unknown}"

  if [ "${status}" = "Completed" ] || [ "${status}" = "Failed" ]; then
    break
  fi

  attempt=$((attempt + 1))
  sleep_seconds 1
done

print_timeline "${run_id}"

if [ "${status}" = "Completed" ]; then
  echo "Failover demo passed: run ${run_id} Completed after pod kill."
  exit 0
fi

if [ "${status}" = "Failed" ]; then
  compensated_steps="$(psql_value worker_db "select coalesce(string_agg(step_no::text, ',' order by started_at), '') from job_step_runs where run_id = '${run_id}' and status = 'Compensated';" | tr -d '\r')"
  if [ "${failed_step}" = "${STEP_NO}" ] && [ "${compensated_steps}" = "1" ]; then
    echo "Failover demo passed through zombie path: run ${run_id} Failed at step ${failed_step} and compensated steps ${compensated_steps}."
    exit 0
  fi

  echo "Run failed, but expected failedStep=${STEP_NO} and compensated step 1; got failedStep=${failed_step}, compensated=${compensated_steps}."
  exit 1
fi

echo "Run did not reach a terminal status within ${RUN_TIMEOUT_SECONDS}s."
exit 1
