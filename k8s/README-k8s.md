# FlowForge on k3d

Target cluster: `flowforge` with 2 agents and load balancer mapped as `5001:80`.

These manifests use local images imported into k3d. There is no registry in this
demo path, and app deployments use `imagePullPolicy: IfNotPresent`.

## Build and Import Images

```bash
docker build -t flowforge-controlplane:latest -f src/FlowForge.ControlPlane/Dockerfile .
docker build -t flowforge-worker:latest -f src/FlowForge.Worker/Dockerfile .
docker build -t flowforge-logindexer:latest -f src/FlowForge.LogIndexer/Dockerfile .

k3d image import flowforge-controlplane:latest flowforge-worker:latest flowforge-logindexer:latest -c flowforge
```

## Apply Manifests

```bash
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/01-config.yaml
kubectl apply -f k8s/10-kafka.yaml
kubectl apply -f k8s/11-postgres.yaml
kubectl apply -f k8s/12-elasticsearch.yaml
kubectl apply -f k8s/13-kibana.yaml

kubectl -n flowforge rollout status statefulset/kafka --timeout=180s
kubectl -n flowforge rollout status statefulset/postgres --timeout=180s
kubectl -n flowforge rollout status statefulset/elasticsearch --timeout=240s

kubectl apply -f k8s/20-kafka-init-job.yaml
kubectl -n flowforge wait --for=condition=complete job/kafka-init --timeout=180s

kubectl apply -f k8s/30-controlplane.yaml
kubectl apply -f k8s/31-worker.yaml
kubectl apply -f k8s/32-logindexer.yaml
kubectl apply -f k8s/33-worker-hpa.yaml

kubectl -n flowforge rollout status deployment/controlplane --timeout=180s
kubectl -n flowforge rollout status deployment/worker --timeout=180s
kubectl -n flowforge rollout status deployment/logindexer --timeout=180s
kubectl -n flowforge rollout status deployment/kibana --timeout=180s
kubectl -n flowforge get hpa worker
```

## Verify

```bash
kubectl -n flowforge get pods

curl -fsS http://localhost:5001/api/jobs

run_id="$(curl -fsS -X POST http://localhost:5001/api/jobs/monthly-sales-report/run \
  | sed -n 's/.*"runId":"\([^"]*\)".*/\1/p')"

for i in $(seq 1 90); do
  status="$(curl -fsS "http://localhost:5001/api/runs/${run_id}" \
    | sed -n 's/.*"status":"\([^"]*\)".*/\1/p')"
  echo "${run_id}: ${status}"
  [ "${status}" = "Completed" ] && break
  sleep 1
done
```

Expected result:

- `kubectl -n flowforge get pods` shows every pod `Running`, plus
  `kafka-init` as `Completed`.
- `http://localhost:5001/api/jobs` returns JSON.
- The triggered `monthly-sales-report` run reaches `Completed`.

## Failover Demo

This demo starts a normal `monthly-sales-report` run, waits until step 2 is
running, reads the worker pod name from `worker_db.job_step_runs.worker_id`, and
deletes that pod.

```bash
scripts/chaos-pod-kill.sh
```

Expected output shape:

```text
Run id: 00000000-0000-0000-0000-000000000000
Deleting worker pod worker-... while step 2 is Running...
Status: Running
...
Timeline for 00000000-0000-0000-0000-000000000000:
1 | Completed | attempt=1 | worker=worker-...
2 | Failed | attempt=1 | worker=worker-... | ...Zombie step detected...
1 | Compensated | attempt=2 | worker=worker-...
Failover demo passed through zombie path...
```

### Two valid outcomes

Depending on timing, a fully redelivered run may finish as `Completed`. This
happens when Kubernetes replaces the pod and Kafka redelivery reaches a worker
before the running step is closed as stale.

The run may also finish as `Failed` with compensation. This happens when the
heartbeat for the killed pod's running step crosses the zombie threshold; the
system marks that step failed, emits `StepFailed`, and compensates already
completed steps in reverse order.

Both outcomes are successful failover demonstrations because the run is not
lost or left stuck:

- `Completed` after worker replacement.
- `Failed` at step 2 with step 1 compensated by the zombie cleanup path.

Useful overrides:

```bash
BASE_URL=http://localhost:5001 NAMESPACE=flowforge scripts/chaos-pod-kill.sh
KILL_DELAY_SECONDS=10 RUN_TIMEOUT_SECONDS=180 scripts/chaos-pod-kill.sh
```

## Watching HPA Scale

In one terminal, watch the Worker HPA:

```bash
kubectl -n flowforge get hpa worker -w
```

In another terminal, generate a burst of work:

```bash
scripts/hpa-load.sh
```

Expected output shape from the load script:

```text
Triggering 30 monthly-sales-report-chaos runs against http://localhost:5001...
1/30: 00000000-0000-0000-0000-000000000000
...
30/30: 00000000-0000-0000-0000-000000000000
Load submitted. Keep watching HPA and worker pods:
```

Expected watcher shape:

```text
NAME     REFERENCE           TARGETS    MINPODS   MAXPODS   REPLICAS
worker   Deployment/worker   75%/70%    2         6         3
```

CPU scaling depends on local machine capacity and metrics-server sampling
windows. If `TARGETS` starts as `<unknown>`, wait for the next metrics sample
or confirm `kubectl top pods -n flowforge` works.

Useful overrides:

```bash
RUNS=50 INTERVAL_SECONDS=0.1 scripts/hpa-load.sh
BASE_URL=http://localhost:5001 JOB_NAME=monthly-sales-report scripts/hpa-load.sh
```

## Windows Notes

The scripts are LF-terminated Bash scripts. On Windows, run them from Git Bash
or WSL so `curl`, `kubectl`, and POSIX shell behavior are available:

```bash
bash scripts/chaos-pod-kill.sh
bash scripts/hpa-load.sh
```

## Reset Demo State

To recreate databases and Kafka/Elasticsearch data from scratch:

```bash
kubectl delete namespace flowforge
kubectl apply -f k8s/00-namespace.yaml
```
