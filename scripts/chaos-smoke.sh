#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"
MIN_RUNS="${MIN_RUNS:-5}"
MAX_RUNS="${MAX_RUNS:-20}"

docker_cmd() {
  if command -v docker >/dev/null 2>&1; then
    docker "$@"
  elif command -v docker.exe >/dev/null 2>&1; then
    docker.exe "$@"
  else
    echo "Docker CLI not found."
    exit 1
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

psql_value() {
  database="$1"
  sql="$2"
  docker_cmd compose exec -T postgres psql -U postgres -d "${database}" -Atc "${sql}"
}

print_logs() {
  docker_cmd compose logs --tail=250 controlplane worker
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

failed_run_id=""
failed_step=""
run_count=0

while [ "${run_count}" -lt "${MAX_RUNS}" ]; do
  run_count=$((run_count + 1))
  echo "Triggering monthly-sales-report-chaos run ${run_count}/${MAX_RUNS}..."
  response="$(curl --max-time 10 -fsS -X POST "${BASE_URL}/api/jobs/monthly-sales-report-chaos/run")"
  run_id="$(json_value "runId" "${response}")"

  if [ -z "${run_id}" ]; then
    echo "Could not parse runId from response: ${response}"
    print_logs
    exit 1
  fi

  poll=1
  status=""
  while [ "${poll}" -le 90 ]; do
    run_json="$(curl --max-time 5 -fsS "${BASE_URL}/api/runs/${run_id}" || true)"
    status="$(json_value "status" "${run_json}")"
    echo "Run ${run_id}: ${status:-unknown}"

    if [ "${status}" = "Completed" ] || [ "${status}" = "Failed" ]; then
      break
    fi

    poll=$((poll + 1))
    sleep_seconds 1
  done

  if [ "${status}" = "Failed" ]; then
    failed_run_id="${run_id}"
    failed_step="$(json_value "failedStep" "${run_json}")"
  fi

  if [ -n "${failed_run_id}" ] && [ "${run_count}" -ge "${MIN_RUNS}" ]; then
    break
  fi
done

if [ -z "${failed_run_id}" ]; then
  echo "No failed chaos run observed after ${MAX_RUNS} runs."
  print_logs
  exit 1
fi

if [ -z "${failed_step}" ] || [ "${failed_step}" = "null" ]; then
  echo "Run ${failed_run_id} is Failed but failedStep is empty."
  print_logs
  exit 1
fi

echo "Failed run: ${failed_run_id}, failedStep=${failed_step}"

db_status="$(psql_value control_db "select status || ':' || coalesce(failed_step::text, '') from job_runs where id = '${failed_run_id}';")"
if [ "${db_status}" != "Failed:${failed_step}" ]; then
  echo "Unexpected job_runs state: ${db_status}"
  print_logs
  exit 1
fi

compensated_steps="$(psql_value worker_db "select coalesce(string_agg(step_no::text, ',' order by started_at), '') from job_step_runs where run_id = '${failed_run_id}' and status = 'Compensated';")"
if [ "${compensated_steps}" != "2,1" ]; then
  echo "Unexpected compensated step order for ${failed_run_id}: ${compensated_steps}"
  print_logs
  exit 1
fi

dlq_messages="$(docker_cmd compose exec -T kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:9092 --topic flowforge.job.events.dlq --from-beginning --timeout-ms 5000 2>/dev/null || true)"
case "${dlq_messages}" in
  *"${failed_run_id}"*)
    echo "DLQ contains failed run ${failed_run_id}."
    ;;
  *)
    echo "DLQ does not contain failed run ${failed_run_id}."
    print_logs
    exit 1
    ;;
esac

echo "Chaos smoke passed after ${run_count} runs."
