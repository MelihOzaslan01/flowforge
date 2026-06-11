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

kubectl -n flowforge rollout status deployment/controlplane --timeout=180s
kubectl -n flowforge rollout status deployment/worker --timeout=180s
kubectl -n flowforge rollout status deployment/logindexer --timeout=180s
kubectl -n flowforge rollout status deployment/kibana --timeout=180s
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

## Reset Demo State

To recreate databases and Kafka/Elasticsearch data from scratch:

```bash
kubectl delete namespace flowforge
kubectl apply -f k8s/00-namespace.yaml
```
