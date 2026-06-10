# FlowForge — Görev Backlog'u

> Kural: Ajan, bir görevi yalnızca §8 doğrulama komutları yeşil olduğunda `[x]` yapabilir.
> Kısmen biten görev `[~]` ile işaretlenir ve PROGRESS.md'de nedeni açıklanır.

## Faz 1 — Çekirdek (ControlPlane + Worker + Kafka + Outbox)
- [x] 1.1 Solution iskeleti (`dotnet build` 0 warning)
- [x] 1.2 Contracts: EventEnvelope + 8 event record + KafkaTopics + round-trip testi
- [x] 1.3 ControlPlane veri katmanı: control_db entity + DbContext + Initial migration (partial index dahil)
- [x] 1.4 Job API: 6 endpoint + Swagger + seed job (`monthly-sales-report`, chaos=0)
- [x] 1.5 OutboxPublisher (FlowForge.Outbox classlib, 500ms, batch 100, Acks.All)
- [x] 1.6 Worker: consumer loop + StepExecutor + TX sıralaması (iş+inbox+outbox → commit → offset)
- [ ] 1.7 ControlPlane projeksiyon consumer'ı (`controlplane-projection` group)
- [ ] 1.8 docker-compose + kafka-init + scripts/smoke.sh yeşil
- [ ] **Faz 1 kapanış:** smoke yeşil, Kafka UI'da 4 StepCompleted + 1 JobRunCompleted, outbox lag = 0 → `git tag v0.1`

## Faz 2 — Dayanıklılık (Retry, DLQ, Saga, Test)
- [ ] 2.1 Worker replicas: 3, partition dağılımı loglarda
- [ ] 2.2 Polly retry (2s/4s/8s, max_retries job_steps'ten)
- [ ] 2.3 DLQ yönlendirmesi + StepFailed eventi
- [ ] 2.4 Saga compensation zinciri (ters sıra, Compensate metodları)
- [ ] 2.5 Chaos flag + `monthly-sales-report-chaos` seed job'ı
- [ ] 2.6 Heartbeat + zombi adım temizleyici
- [ ] 2.7 Testcontainers integration testleri (3 isimli test)
- [ ] 2.8 MudBlazor dashboard (Jobs + RunDetail timeline)
- [ ] **Faz 2 kapanış:** chaos run tam telafi üretiyor, `dotnet test` yeşil → `git tag v0.2`

## Faz 3 — Elasticsearch
- [ ] 3.1 Worker → flowforge.job.logs yapısal log üretimi
- [ ] 3.2 LogIndexer: bulk indexing + index template + aylık index
- [ ] 3.3 Compose'a elasticsearch + kibana
- [ ] **Faz 3 kapanış:** Kibana'da runId timeline → `git tag v0.3`

## Faz 4 — Kubernetes
- [ ] 4.1 /k8s manifest seti (k3d)
- [ ] 4.2 HPA (cpu %70, 2–6) + probe bağlantıları
- [ ] 4.3 scripts/chaos-pod-kill.sh + failover demosu
- [ ] **Faz 4 kapanış:** pod kill altında run Completed → `git tag v0.4`

## Faz 5 — CI + Cila
- [ ] 5.1 GitHub Actions (build+unit her push, integration nightly)
- [ ] 5.2 README: diyagram + GIF + test listesi
- [ ] 5.3 3 ADR (choreography, outbox, sql-queue-vs-kafka)
- [ ] 5.4 Grafana dashboard JSON'ları
- [ ] **Faz 5 kapanış:** CI badge yeşil → `git tag v0.5`
