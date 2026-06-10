# FlowForge — AI Kodlama Ajanı Uygulama Talimatı (v1.0)

> **Bu doküman nasıl kullanılır:** `FlowForge-Tasarim-Dokumani.md` ile BİRLİKTE ajana verilir.
> Tasarım dokümanı = NE ve NEDEN. Bu doküman = NASIL ve HANGİ SIRAYLA.
> Çelişki olursa bu doküman kazanır.

---

## 0. Ajan İçin Rol ve Genel Kurallar

Sen kıdemli bir .NET backend mühendisisin. FlowForge adlı dağıtık job orchestration platformunu, ekteki tasarım dokümanına sadık kalarak, AŞAĞIDAKİ FAZ SIRASIYLA implemente edeceksin.

**Mutlak kurallar:**
1. SADECE istenen fazı implemente et. Sonraki fazlara ait kod, paket veya "ileride lazım olur" soyutlaması EKLEME (YAGNI).
2. Tasarım dokümanındaki şemaları (tablo adları, kolon adları, topic adları, event adları, endpoint'ler) BİREBİR kullan. Yeniden adlandırma yapma.
3. Her görev sonunda §8'deki doğrulama komutlarını çalıştır; geçmeyen görevi tamamlandı sayma.
4. Hata yutma yasak: tüm consumer/publisher döngülerinde exception loglanır, süreç ayakta kalır.
5. Yorum satırları İngilizce, kısa ve "neden" odaklı olsun ("ne yaptığını" kod zaten söylüyor).
6. Secrets/connection string'ler appsettings + environment variable üzerinden; koda gömme.
7. Her faz sonunda README.md'nin ilgili bölümünü güncelle ve `git tag v0.X` öner.
8. Takip disiplini: her görev sonunda `.ai/BACKLOG.md`'de görevi işaretle ve `.ai/PROGRESS.md`'ye şablona uygun blok ekle. Talimatta belirtilmeyen bir karar almak veya talimattan sapmak zorunda kalırsan, kodu yazmadan ÖNCE `.ai/DECISIONS.md`'ye gerekçesiyle kaydet. Oturum sonunda `.ai/sessions/` altındaki aktif dosyanın özet bölümünü doldur. Yeni oturuma başlarken önce PROGRESS.md ve son session dosyasını oku.

---

## 1. Teknoloji Sabitlemesi (Sürümler — değiştirme)

| Bileşen | Sürüm / Paket |
|---|---|
| Runtime | .NET 9 (net9.0) |
| Web | ASP.NET Core Minimal API + Blazor Server (yalnız ControlPlane) |
| ORM | Microsoft.EntityFrameworkCore 9.x + Npgsql.EntityFrameworkCore.PostgreSQL 9.x |
| Kafka | Confluent.Kafka 2.6.x |
| Retry | Polly 8.x (ResiliencePipeline API) |
| Scheduler | Quartz 3.13.x (yalnız ControlPlane cron tetikleme) |
| Log | Serilog.AspNetCore 8.x (console sink, JSON formatter) |
| Elasticsearch client | Elastic.Clients.Elasticsearch 8.x (yalnız LogIndexer) |
| OTel | OpenTelemetry.Extensions.Hosting + OTLP exporter (Faz 1'de sadece iskelet, Faz 2'de metrik) |
| Test | xUnit 2.9 + NSubstitute 5.x + Testcontainers 4.x (Kafka, PostgreSQL modülleri) |
| Container | docker compose v2 söz dizimi; imajlar tasarım dokümanı §11'deki gibi |

UI bileşeni: MudBlazor 7.x (dashboard yalnız Faz 2 sonunda, minimum 2 sayfa: Jobs, RunDetail).

---

## 2. Solution İskeleti (Faz 1 — Görev 1)

```
flowforge.sln
src/
  FlowForge.Contracts/         (classlib)  → event records, topic sabitleri, EventEnvelope
  FlowForge.ControlPlane/      (web)       → API + Quartz + OutboxPublisher + EF (control_db)
  FlowForge.Worker/            (worker)    → consumer loop + StepExecutor + outbox/inbox + EF (worker_db)
  FlowForge.LogIndexer/        (worker)    → Faz 3'te dolacak, Faz 1'de PROJE OLUŞTURMA
tests/
  FlowForge.UnitTests/
  FlowForge.IntegrationTests/  → Faz 1'de boş iskelet, Faz 2'de Testcontainers
infra/
  init-dbs.sql                 → CREATE DATABASE control_db; CREATE DATABASE worker_db;
docker-compose.yml             → Faz 1: kafka, kafka-ui, postgres, controlplane, worker (replicas:1)
docker-compose.override.yml    → sadece infra servisleri (lokal IDE geliştirme için)
.editorconfig                  → nullable enable, file-scoped namespace, var tercih
Directory.Build.props          → TargetFramework, LangVersion, TreatWarningsAsErrors=true
.github/workflows/ci.yml       → Faz 5'te; Faz 1'de OLUŞTURMA
```

**Konvansiyonlar:**
- Namespace = klasör yapısı. Feature-folder düzeni: `Features/Jobs/`, `Features/Runs/`, `Outbox/`, `Inbox/`.
- EF migrations her servis kendi içinde (`dotnet ef migrations add Initial`), startup'ta `Database.Migrate()` (demo projesi olduğu için kabul; ADR'ye not düş).
- Tüm I/O metodları `CancellationToken` alır.
- DateTimeOffset.UtcNow kullan; DateTime.Now YASAK.

---

## 3. FAZ 1 — Görev Listesi (sırayla, her biri ayrı commit)

**Görev 1.1 — İskelet:** §2'deki yapıyı kur. `dotnet build` temiz geçmeli (0 warning).

**Görev 1.2 — Contracts:** `EventEnvelope` (messageId, eventType, occurredAt, traceParent, version, payload — System.Text.Json, camelCase). 8 event record'u (tasarım §4.2 tablosundaki adlarla). `KafkaTopics` static sınıfı (3 topic sabiti). Round-trip serileştirme unit testi yaz.

**Görev 1.3 — ControlPlane veri katmanı:** control_db entity'leri + DbContext + Initial migration (tasarım §6.1 şemasıyla birebir; outbox_messages + processed_messages dahil, partial index dahil — migration'da `HasFilter("published_at IS NULL")`).

**Görev 1.4 — Job API:** `POST /api/jobs`, `GET /api/jobs`, `POST /api/jobs/{name}/run`, `GET /api/runs/{runId}`, `/healthz`, `/readyz`. Run endpoint'i: tek transaction'da job_runs(Scheduled) + outbox(JobRunRequested) → 202 + runId. Swagger açık. Seed: uygulama ayağa kalkarken `monthly-sales-report` job'ı 4 adımıyla (tasarım §2.1 tablosu) yoksa eklenir; chaos_fail_rate=0 (Faz 1'de hata enjeksiyonu YOK).

**Görev 1.5 — OutboxPublisher:** Tasarım §5.4'teki kalıp. 500ms PeriodicTimer, batch 100, key=aggregateId, Acks.All + EnableIdempotence. Publish başarısızsa attempt_count++ ve kayıt bekler (silme yok). Hem ControlPlane hem Worker'da kullanılacağı için paylaşılan bir `FlowForge.Outbox` classlib'ine koy (tek istisna olarak yeni proje eklemene izin var).

**Görev 1.6 — Worker:** worker_db (tasarım §6.2) + consumer loop (tasarım §4.3 config'i, EnableAutoCommit=false, CooperativeSticky). Mesaj işleme sırası tasarım §5.5 kalıbıyla BİREBİR: TX(iş + inbox + outbox) → commit → Kafka offset commit. StepExecutor: step_type'a göre simülasyon (Task.Delay süreleri tasarım §2.1; her adım job_step_runs'a yazar). Adım bitince sonraki event'i outbox'a yaz: son adımsa JobRunCompleted, değilse StepCompleted.

**Görev 1.7 — ControlPlane projeksiyon consumer'ı:** ControlPlane içinde ikinci bir BackgroundService: `flowforge.job.events` topic'ini AYRI consumer group (`controlplane-projection`) ile dinler, JobRunCompleted/JobRunFailed geldiğinde job_runs.status günceller. (Not: bu consumer idempotent olmalı — status zaten Completed ise atla; inbox tablosu kullanmasına gerek yok, doğal idempotent.)

**Görev 1.8 — Compose + smoke:** docker-compose.yml (tasarım §11; worker replicas Faz 1'de 1). Topic'ler auto-create yerine compose'da bir `kafka-init` one-shot container ile `kafka-topics.sh --create` (partition sayıları tasarım §4.1). Smoke script `scripts/smoke.sh`: run tetikle → 60 sn içinde status=Completed bekle, değilse exit 1.

**FAZ 1 BİTTİ tanımı:** `docker compose up -d --build` sonrası `./scripts/smoke.sh` yeşil; Kafka UI'da 4 StepCompleted + 1 JobRunCompleted eventi görünüyor; outbox'ta published_at NULL kayıt kalmıyor.

---

## 4. FAZ 2 — Görev Listesi (özet; detay şemalar tasarım dokümanında)

2.1 Worker replicas:3; partition dağılımının loglarda görünmesi.
2.2 Retry: Polly exponential backoff (2s/4s/8s, max 3 — job_steps.max_retries'tan oku); attempt_count job_step_runs'a işlenir.
2.3 DLQ: retry tükenince orijinal mesaj + hata metadata'sı `flowforge.job.events.dlq`'ya; ardından StepFailed eventi outbox'a.
2.4 Saga compensation: StepFailed → ters sırayla CompensateStep/StepCompensated zinciri → JobRunFailed (tasarım §2.3 akışı birebir). StepExecutor'a her step_type için Compensate metodu ekle.
2.5 Chaos flag: job_steps.config.chaos_fail_rate (0–1); seed'de GenerateReport=0.3'e çekilebilir bir ikinci demo job'ı ekle (`monthly-sales-report-chaos`).
2.6 Heartbeat: çalışan adımda 5 sn'de bir last_heartbeat_at güncelle; worker startup'ta "Running + heartbeat > 60 sn eski" adımları Failed'a çekip StepFailed yayınlayan bir zombi-temizleyici ekle.
2.7 Idempotency integration testleri (Testcontainers): tasarım §10'daki 3 isimli test.
2.8 MudBlazor dashboard: Jobs listesi + RunDetail timeline (5 sn polling yeterli; SignalR'ı opsiyonel bırak).

**FAZ 2 BİTTİ:** chaos job'ı çalıştırıldığında compensation zinciri uçtan uca; `dotnet test` (unit+integration) yeşil; duplicate mesaj testi tek işlem kanıtlıyor.

## 5. FAZ 3 — LogIndexer (özet)
Worker'lar adım olaylarında `flowforge.job.logs`'a yapısal kayıt basar (tasarım §8.2 alanları). LogIndexer: ayrı consumer group, 500 kayıt VEYA 2 sn buffer ile bulk index, index adı `flowforge-logs-{yyyy.MM}`, startup'ta index template PUT eder. Compose'a elasticsearch+kibana eklenir.
**BİTTİ:** Kibana'da runId araması bir run'ın tüm adımlarını gösteriyor.

## 6. FAZ 4 — k8s (özet)
`/k8s` manifest seti (tasarım §12). k3d, HPA (cpu %70, 2–6), probe'lar mevcut /healthz /readyz'a bağlanır. Chaos demo scripti: `scripts/chaos-pod-kill.sh`.
**BİTTİ:** pod kill sırasında başlatılan run Completed bitiyor; HPA ölçeklenmesi gözlemleniyor.

## 7. FAZ 5 — CI + cila (özet)
GitHub Actions: build + unit test her push; integration test nightly (Testcontainers, ubuntu-latest). README: mimari diyagram, chaos GIF, test listesi, 3 ADR (tasarım §13). Grafana dashboard JSON'ları `infra/grafana/`a.

---

## 8. Doğrulama Komutları (her görev sonunda ilgili olanlar)

```bash
dotnet build -warnaserror
dotnet test tests/FlowForge.UnitTests
docker compose up -d --build && docker compose ps          # hepsi healthy
./scripts/smoke.sh                                          # Faz 1 sonu itibarıyla
docker compose exec postgres psql -U postgres -d control_db \
  -c "SELECT count(*) FROM outbox_messages WHERE published_at IS NULL;"   # 0 beklenir
# Kafka eventlerini doğrula:
docker compose exec kafka /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server localhost:9092 --topic flowforge.job.events --from-beginning --max-messages 6
dotnet test tests/FlowForge.IntegrationTests               # Faz 2'den itibaren
```

---

## 9. Ajanın YAPMAYACAKLARI (anti-scope)

- MassTransit, Wolverine, CAP, MediatR, AutoMapper gibi framework'ler EKLEME — outbox/saga elle yazılacak, projenin amacı bu.
- Authentication/authorization EKLEME (demo kapsamı dışı; README'ye "bilinçli kapsam dışı" notu düş).
- Exactly-once iddiası veya Kafka transactions KULLANMA; at-least-once + idempotent consumer modelinde kal.
- Schema Registry / Avro EKLEME; JSON + zarf içi version alanı yeterli.
- Mikroservis sayısını artırma; 3 servis + 1 classlib (+ FlowForge.Outbox) sabittir.
- Tasarım dokümanındaki adlandırmaları "iyileştirme".
