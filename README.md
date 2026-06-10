# FlowForge

> **Distributed Job Orchestration Platform** — a from-scratch implementation of Kafka, the Outbox Pattern, choreography Sagas, and Kubernetes-based failover in .NET 9.
>
> I previously designed and ran a production orchestrator on a single-server, SQL-backed queue (80+ RPA bots). FlowForge is my redesign of the **same problem for distributed scale** — built to learn the trade-offs by living them, not by reading about them.

<!-- Faz 4 sonrası: buraya chaos demo GIF'i (pod kill → job survives) -->
<!-- Faz 5 sonrası: buraya CI badge -->

## What it demonstrates

| Concern | How it's solved here |
|---|---|
| Reliable messaging | Kafka (KRaft), key=`runId` for per-run ordering |
| Dual-write problem | Transactional **Outbox** + background publisher |
| Duplicate delivery | **Inbox** table → idempotent consumers (at-least-once, safe) |
| Multi-step failure | **Choreography Saga** with reverse compensation chain |
| Worker crash | Consumer-group rebalancing + heartbeat-based zombie recovery |
| Searchable history | Structured logs → Elasticsearch (monthly indices) |
| Operations | OpenTelemetry traces across Kafka hops, Prometheus + Grafana |
| Scale | Kubernetes HPA (2→6 workers under load) |
| Confidence | xUnit + **Testcontainers** integration tests (real Kafka & Postgres in tests) |

## Quick start

```bash
docker compose up -d --build
./scripts/smoke.sh          # triggers a run, waits for Completed
# Kafka UI → http://localhost:8080 | API/Swagger → http://localhost:5000
```

## Architecture

See [`docs/architecture.md`](docs/architecture.md) for the full design and
[`docs/adr/`](docs/adr) for the key decisions:

- ADR-001 — Choreography vs. orchestration sagas
- ADR-002 — Outbox vs. direct publish
- ADR-003 — SQL-backed queue vs. Kafka: what changes at which scale

## Project status

Current status: Phase 1 scaffolding is in progress. The repository is built in
phases, each independently runnable once its tag is cut — see tags `v0.1`–`v0.5`.
Development is AI-assisted under a strict protocol: task backlog, append-only
progress log, and mandatory decision records live in [`.ai/`](.ai) — kept in
the repo deliberately, as a record of *how* the system was built, not just what.

**Deliberately out of scope:** authentication, exactly-once semantics
(at-least-once + idempotency is the honest model), schema registry.

## License

MIT
