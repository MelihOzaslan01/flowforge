# FlowForge — Distributed Job Orchestration Platform
## Detaylı Tasarım ve Entegrasyon Dokümanı (v1.0)

> **Tek cümlelik özet:** Marubeni'de tek sunucuda SQL-backed queue ile çözdüğüm orkestrasyon problemini; Kafka, Outbox Pattern, Saga, PostgreSQL, Elasticsearch ve Kubernetes ile dağıtık ölçekte sıfırdan yeniden tasarlıyorum.

**Hedef:** Trendyol benzeri ilanlardaki gap'leri tek projede hands-on kanıta dönüştürmek:
Kafka ✓ Outbox ✓ Saga ✓ Kubernetes ✓ PostgreSQL ✓ Elasticsearch ✓ Testing (Testcontainers) ✓ Docker ✓ Observability ✓

**Çalışma ortamı:** Tamamen lokal PC (32 GB RAM, Core Ultra 9). Faz 1-3: Docker Compose. Faz 4: k3d/minikube. Şirket altyapısıyla sıfır temas — kişisel GitHub projesi.

---

## 1. Problem Tanımı ve Vizyon

### 1.1 Çözülen Problem
Dağıtık sistemlerde "zamanlanmış veya tetiklenen işleri güvenilir şekilde çalıştırma" problemi:

- İşler (job) birden fazla worker'a **dağıtılmalı** (tek makineye bağımlılık yok)
- Bir worker çökerse iş **kaybolmamalı**, başka worker devralmalı
- Çok adımlı işlerde bir adım patlarsa önceki adımlar **telafi edilmeli** (compensation)
- Her event **en az bir kez** işlenmeli, **mükerrer işlenme** zararsız olmalı (idempotency)
- Her job'ın uçtan uca **izlenebilirliği** olmalı (trace, log, metrik)

Bu, Hangfire / Temporal / Airflow'un çözdüğü problem ailesinin eğitim amaçlı, sade bir implementasyonudur.

### 1.2 Marubeni Orchestrator ile Fark (Mülakat Hikâyesi)

| Boyut | Mevcut (Marubeni RPA Orchestrator) | FlowForge |
|---|---|---|
| Mesajlaşma | SQL-backed queue (polling) | Kafka (push, partition'lı) |
| Ölçek | Tek control plane, az sayıda sunucu | N adet stateless worker, yatay ölçek |
| Tutarlılık | Tek DB transaction | Outbox Pattern + idempotent consumer |
| Çok adımlı iş | Sıralı, manuel telafi | Choreography Saga + otomatik compensation |
| Failover | Manuel müdahale | Consumer group rebalancing + heartbeat |
| Log | Merkezi Log API (SQL Server) | Elasticsearch + Kibana |
| Deploy | Windows Service | Docker → Kubernetes (HPA) |

Bu tablo README'ye de girecek; "aynı problemi iki ölçekte çözdüm" anlatısının omurgası bu.

---

## 2. Ana Senaryo (Uçtan Uca, Net Tanım)

### 2.1 Domain Senaryosu: "Aylık Rapor Pipeline'ı"

Soyut "job" kavramını somutlaştırmak için demo domain'i şu: bir şirketin operasyon ekibi, çok adımlı veri işleme zincirleri tanımlar ve çalıştırır.

**Örnek Job Chain: `monthly-sales-report`**

| Adım | Step Type | Ne yapar (simülasyon) | Süre | Compensation (telafi) |
|---|---|---|---|---|
| 1 | `ExtractData` | Kaynaktan veri çeker, staging tablosuna yazar | 5 sn | Staging kayıtlarını siler |
| 2 | `TransformData` | Veriyi dönüştürür, rapor tablosu oluşturur | 8 sn | Rapor tablosunu siler |
| 3 | `GenerateReport` | PDF/CSV üretir, dosya deposuna yazar | 4 sn | Dosyayı siler |
| 4 | `NotifyUsers` | Bildirim eventi yayınlar | 1 sn | — (son adım, telafi gerekmez) |

> Adımların "gerçek işi" simülasyondur (Task.Delay + DB yazma + kasıtlı hata enjeksiyonu). Projenin değeri iş mantığında değil, **dağıtık koordinasyon mekaniklerinde**.

### 2.2 Happy Path — Adım Adım Akış

1. Kullanıcı (UI veya REST) `POST /api/jobs/monthly-sales-report/run` çağırır.
2. **Control Plane**, tek bir PostgreSQL transaction'ı içinde:
   - `job_runs` tablosuna `Status=Scheduled` kayıt atar
   - `outbox` tablosuna `JobRunRequested` eventi yazar → **commit**
3. **Outbox Publisher** (background service) outbox'taki kaydı okur, `flowforge.job.events` topic'ine publish eder, kaydı `PublishedAt` ile işaretler.
4. **Worker-1** (consumer group: `flowforge-workers`) eventi alır:
   - `processed_messages` (inbox) tablosunda MessageId kontrolü → daha önce işlenmemiş
   - Step 1'i (`ExtractData`) çalıştırır, kendi DB transaction'ı içinde sonucu + outbox'a `StepCompleted{Step=1}` yazar
5. `StepCompleted` eventi Kafka'ya düşer → herhangi bir worker (belki Worker-2) Step 2'yi alır ve çalıştırır. **Saga choreography**: merkezi yönetici yok, her adımın tamamlanma eventi bir sonraki adımı tetikler.
6. Step 4 tamamlanınca `JobRunCompleted` eventi yayınlanır; Control Plane bu eventi dinleyip `job_runs.Status=Completed` yapar.
7. Tüm adımlar boyunca worker'lar `flowforge.job.logs` topic'ine yapısal log üretir; **Log Indexer** servisi bunları Elasticsearch'e indeksler; Kibana'da `runId` ile uçtan uca arama yapılır.

### 2.3 Failure Path — Saga Compensation (Projenin Kalbi)

**Senaryo:** Step 3 (`GenerateReport`) kasıtlı olarak %30 ihtimalle exception fırlatacak şekilde işaretlenmiş (chaos flag).

1. Worker-3, Step 3'ü dener → exception.
2. Retry policy: 3 deneme, exponential backoff (2s, 4s, 8s). Her deneme `job_step_runs.AttemptCount`'a işlenir.
3. 3 deneme de başarısız → worker, `StepFailed{Step=3}` eventini outbox üzerinden yayınlar; orijinal mesaj **DLQ topic'ine** (`flowforge.job.events.dlq`) kopyalanır.
4. `StepFailed` eventi **compensation zincirini ters yönde** tetikler:
   - `CompensateStep{Step=2}` → Worker rapor tablosunu siler → `StepCompensated{Step=2}`
   - `CompensateStep{Step=1}` → Worker staging'i temizler → `StepCompensated{Step=1}`
5. Tüm telafiler bitince `JobRunFailed` eventi → Control Plane `Status=Failed (Compensated)` yapar.
6. Kibana'da runId araması: deneme sayıları, hata stack trace'i, telafi adımları tek timeline'da görünür.

### 2.4 Worker Crash Path — Failover Demo'su

**Senaryo (K8s fazında canlı demo):** Step 2 çalışırken `kubectl delete pod worker-2` çekilir.

1. Worker-2'nin Kafka session'ı düşer → **consumer group rebalancing** → partition'ları Worker-1 ve Worker-3'e devredilir.
2. Worker-2 Step 2'yi commit etmeden öldüğü için offset ilerlememiştir → event yeniden teslim edilir (**at-least-once**).
3. Devralan worker `processed_messages` kontrolü yapar: adım yarıda kalmış (`Status=Running`, heartbeat eski) → adımı **yeniden çalıştırır**; adım idempotent tasarlandığı için (staging'i önce temizle, sonra yaz) mükerrer yan etki oluşmaz.
4. Job kesintisiz tamamlanır. README'ye bu demonun kısa GIF'i konur — repo'nun vitrin görseli budur.

---

## 3. Sistem Mimarisi

### 3.1 Bileşen Diyagramı

```
                        ┌────────────────────────────────────────────┐
                        │                KULLANICI                    │
                        │   Blazor Dashboard  /  REST (Swagger)       │
                        └───────────────┬────────────────────────────┘
                                        │ HTTP
                        ┌───────────────▼────────────────┐
                        │   CONTROL PLANE API (.NET 9)   │
                        │  - Job tanımları & tetikleme    │
                        │  - Quartz.NET cron scheduler    │
                        │  - Run durum projeksiyonu       │
                        │  - Outbox Publisher (hosted)    │
                        └───────┬──────────────┬─────────┘
                                │              │
                     ┌──────────▼───┐      ┌───▼──────────────────────┐
                     │ PostgreSQL   │      │       KAFKA (KRaft)      │
                     │  control_db  │      │ topics:                  │
                     │ jobs,runs,   │      │  flowforge.job.events    │
                     │ outbox,inbox │      │  flowforge.job.logs      │
                     └──────────────┘      │  flowforge.job.events.dlq│
                                           └───┬───────────┬──────────┘
                                consumer group │           │ consumer group
                              "flowforge-workers"          │ "log-indexer"
                       ┌───────────┬───────────┐           │
                ┌──────▼─────┐┌────▼──────┐┌───▼──────┐┌───▼─────────────┐
                │  WORKER-1  ││ WORKER-2  ││ WORKER-N ││  LOG INDEXER    │
                │  (.NET 9)  ││ (.NET 9)  ││ (stateless)│  (.NET 9)      │
                │ step exec  ││ retry/DLQ ││ HPA ölçek ││ bulk indexing  │
                │ outbox+inbox│└───────────┘└──────────┘└───┬─────────────┘
                └──────┬─────┘                              │
                ┌──────▼─────┐                       ┌──────▼────────┐
                │ PostgreSQL │                       │ Elasticsearch │
                │  worker_db │                       │   + Kibana    │
                └────────────┘                       └───────────────┘

        Observability kesiti: OpenTelemetry SDK (tüm servisler)
        → OTel Collector → Prometheus (metrik) + Grafana (dashboard)
        → trace context Kafka header'ları üzerinden taşınır (W3C traceparent)
```

### 3.2 Servisler ve Sorumlulukları

**1. FlowForge.ControlPlane (ASP.NET Core 9 Web API + Blazor Server dashboard)**
- Job tanımı CRUD'u (`jobs`, `job_steps` tabloları)
- Manuel tetikleme endpoint'i + Quartz.NET ile cron tabanlı otomatik tetikleme
- `JobRunRequested` eventini outbox'a yazar (asla doğrudan Kafka'ya yazmaz!)
- `JobRunCompleted/Failed` eventlerini dinleyip run durumunu projeksiyon olarak günceller
- Outbox Publisher: 500 ms aralıklı polling yapan BackgroundService (Quartz değil — basitlik için PeriodicTimer)

**2. FlowForge.Worker (.NET 9 Worker Service — stateless, N kopya)**
- `flowforge.job.events` topic'ini `flowforge-workers` consumer group'u ile dinler
- Event tipine göre davranır: `StepCompleted(n)` → adım n+1'i çalıştır; `StepFailed` → compensation başlat
- Her adım çalıştırması: inbox kontrolü → iş → sonuç + yeni event'i outbox'a yaz → tek transaction → commit → Kafka offset commit
- Retry (Polly, exponential backoff) ve DLQ yönlendirmesi
- Heartbeat: `job_step_runs.LastHeartbeatAt` her 5 sn güncellenir (zombi adım tespiti için)

**3. FlowForge.LogIndexer (.NET 9 Worker Service)**
- `flowforge.job.logs` topic'ini ayrı consumer group ile dinler
- Elasticsearch'e **bulk** indeksleme (500 kayıt veya 2 sn buffer)
- Index şablonu: `flowforge-logs-yyyy.MM` (aylık rolling)

**4. FlowForge.Contracts (class library)**
- Tüm event kayıtları (record), topic adları, JSON serileştirme ayarları — tek kaynak

### 3.3 Neden Choreography Saga? (Mülakat sorusu garantili)

- **Choreography (seçilen):** Her servis kendi eventini yayınlar, sonraki adım eventi dinleyerek tetiklenir. Az bileşen, merkezi SPOF yok. Dezavantaj: akışın tamamını görmek zor → bunu Elasticsearch timeline'ı ve `job_runs` projeksiyonu ile çözüyoruz.
- **Orchestration (alternatif):** Merkezi saga orchestrator state machine tutar (örn. MassTransit). Avantaj: akış tek yerden okunur. Dezavantaj: orchestrator'ın kendisi kritik bileşen olur.
- README'de bu trade-off açıkça yazılacak; "neden choreography seçtim, hangi ölçekte orchestration'a geçerdim" paragrafı mülakat altınıdır.

---

## 4. Kafka Tasarımı (Tam Entegrasyon Detayı)

### 4.1 Topic'ler

| Topic | Partition | Retention | Key | İçerik |
|---|---|---|---|---|
| `flowforge.job.events` | 6 | 7 gün | `runId` | Saga eventleri (aşağıdaki tüm event tipleri) |
| `flowforge.job.events.dlq` | 3 | 30 gün | `runId` | Retry'ı tükenen zehirli mesajlar + hata metadata'sı |
| `flowforge.job.logs` | 6 | 3 gün | `runId` | Yapısal log kayıtları |

**Kritik tasarım kararı — key = runId:** Aynı run'ın tüm eventleri aynı partition'a düşer → **aynı run içinde sıra garantisi** sağlanır (Kafka sırayı yalnızca partition içinde garanti eder). Farklı run'lar farklı partition'lara dağılır → paralellik. Bu cümle mülakatta partition sorusunun tam cevabıdır.

### 4.2 Event Sözleşmeleri (FlowForge.Contracts)

Tüm eventler ortak zarf (envelope) içinde taşınır:

```json
{
  "messageId": "guid — idempotency anahtarı",
  "eventType": "StepCompleted",
  "occurredAt": "2026-06-10T14:32:11Z",
  "traceParent": "00-abc...-def...-01",
  "payload": { }
}
```

| EventType | Üreten | Tüketen | Payload (özet) |
|---|---|---|---|
| `JobRunRequested` | ControlPlane | Worker | runId, jobId, steps[] |
| `StepStarted` | Worker | (log/projeksiyon) | runId, stepNo, workerId |
| `StepCompleted` | Worker | Worker (sonraki adım) | runId, stepNo, output |
| `StepFailed` | Worker | Worker (compensation) | runId, stepNo, error, attempts |
| `CompensateStep` | Worker | Worker | runId, stepNo |
| `StepCompensated` | Worker | Worker (önceki adım) | runId, stepNo |
| `JobRunCompleted` | Worker | ControlPlane | runId |
| `JobRunFailed` | Worker | ControlPlane | runId, failedStep, reason |

Serileştirme: System.Text.Json, camelCase, version alanı zarf seviyesinde (`v: 1`) — şema evrimi sorusu için hazırlık.

### 4.3 Consumer Yapılandırması (Confluent.Kafka)

```csharp
var config = new ConsumerConfig
{
    BootstrapServers = cfg.Kafka.BootstrapServers,
    GroupId = "flowforge-workers",
    EnableAutoCommit = false,            // offset'i İŞ BİTİNCE elle commit
    AutoOffsetReset = AutoOffsetReset.Earliest,
    SessionTimeoutMs = 10000,            // crash → ~10 sn'de rebalance
    MaxPollIntervalMs = 300000,          // uzun adımlar için 5 dk tavan
    PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
};
```

**EnableAutoCommit=false neden kritik:** Offset, adımın DB transaction'ı commit olduktan SONRA commit edilir. Aksi durumda worker tam ortada ölürse event "işlendi" sayılır ve kaybolur. Bu sıralama at-least-once garantisinin temelidir; mükerrer teslim ihtimali ise inbox tablosuyla zararsızlaştırılır.

### 4.4 Producer Yapılandırması

```csharp
var config = new ProducerConfig
{
    BootstrapServers = cfg.Kafka.BootstrapServers,
    Acks = Acks.All,                 // tüm replikalar onaylasın (tek broker'da da doğru alışkanlık)
    EnableIdempotence = true,        // broker tarafında duplicate engelleme
    MessageSendMaxRetries = 5,
    LingerMs = 10
};
```

---

## 5. Outbox Pattern — Tam Implementasyon

### 5.1 Problem
"DB'ye yaz + Kafka'ya publish et" iki ayrı sistemdir; ikisini tek atomik işlemde yapamazsın (dual-write problemi). Biri başarılı, diğeri başarısız olursa sistem tutarsız kalır.

### 5.2 Çözüm Akışı
1. İş verisi ve yayınlanacak event, **aynı PostgreSQL transaction'ı** içinde yazılır (event → `outbox` tablosuna).
2. Ayrı bir background publisher, `PublishedAt IS NULL` kayıtları sırayla okur, Kafka'ya basar, başarılı olunca işaretler.
3. Publisher Kafka'ya basamazsa → kayıt outbox'ta bekler, sonraki turda yeniden dener. **Event asla kaybolmaz.**
4. Publisher bastı ama işaretleyemeden öldü → aynı event ikinci kez basılır → **at-least-once** → tüketici inbox'u ile çözülür.

### 5.3 Şema

```sql
CREATE TABLE outbox_messages (
    id              UUID PRIMARY KEY,
    aggregate_id    UUID NOT NULL,          -- runId (Kafka key olarak kullanılır)
    event_type      VARCHAR(100) NOT NULL,
    payload         JSONB NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    published_at    TIMESTAMPTZ NULL,
    attempt_count   INT NOT NULL DEFAULT 0
);
CREATE INDEX ix_outbox_unpublished
    ON outbox_messages (occurred_at) WHERE published_at IS NULL;  -- partial index!
```

```sql
CREATE TABLE processed_messages (         -- INBOX (idempotent consumer)
    message_id      UUID PRIMARY KEY,
    consumer        VARCHAR(100) NOT NULL,
    processed_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

### 5.4 Publisher (ControlPlane ve Worker'da aynı bileşen)

```csharp
public sealed class OutboxPublisher(IServiceScopeFactory scopes, IProducer<string,string> producer)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlowForgeDb>();

            var batch = await db.OutboxMessages
                .Where(m => m.PublishedAt == null)
                .OrderBy(m => m.OccurredAt)
                .Take(100)
                .ToListAsync(ct);

            foreach (var msg in batch)
            {
                await producer.ProduceAsync("flowforge.job.events",
                    new Message<string,string> {
                        Key = msg.AggregateId.ToString(),
                        Value = msg.Payload,
                        Headers = OtelHeaders.From(msg)   // traceparent taşı
                    }, ct);
                msg.PublishedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
```

### 5.5 Idempotent Consumer Kalıbı (Worker tarafı)

```csharp
await using var tx = await db.Database.BeginTransactionAsync(ct);

bool seen = await db.ProcessedMessages.AnyAsync(p => p.MessageId == envelope.MessageId, ct);
if (seen) { consumer.Commit(result); return; }     // duplicate → sessizce geç

await stepExecutor.RunAsync(step, ct);             // asıl iş (kendi içinde idempotent yazma)
db.ProcessedMessages.Add(new(envelope.MessageId, "worker"));
db.OutboxMessages.Add(OutboxMessage.From(nextEvent));   // sonraki saga eventi
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);

consumer.Commit(result);                            // EN SON Kafka offset
```

Sıralama ezberi: **iş + inbox + outbox aynı TX → commit → en son Kafka offset commit.**

---

## 6. Veri Modeli (PostgreSQL)

### 6.1 control_db

```sql
CREATE TABLE jobs (
    id           UUID PRIMARY KEY,
    name         VARCHAR(200) NOT NULL UNIQUE,     -- "monthly-sales-report"
    cron         VARCHAR(50) NULL,                 -- null = sadece manuel
    is_enabled   BOOLEAN NOT NULL DEFAULT true,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE job_steps (
    id              UUID PRIMARY KEY,
    job_id          UUID NOT NULL REFERENCES jobs(id),
    step_no         INT  NOT NULL,
    step_type       VARCHAR(100) NOT NULL,         -- ExtractData, TransformData...
    config          JSONB NOT NULL DEFAULT '{}',   -- chaos_fail_rate vb.
    max_retries     INT NOT NULL DEFAULT 3,
    UNIQUE (job_id, step_no)
);

CREATE TABLE job_runs (
    id            UUID PRIMARY KEY,
    job_id        UUID NOT NULL REFERENCES jobs(id),
    status        VARCHAR(30) NOT NULL,  -- Scheduled|Running|Completed|Failed|Compensated
    requested_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    finished_at   TIMESTAMPTZ NULL,
    failed_step   INT NULL
);
```

### 6.2 worker_db

```sql
CREATE TABLE job_step_runs (
    id                 UUID PRIMARY KEY,
    run_id             UUID NOT NULL,
    step_no            INT NOT NULL,
    status             VARCHAR(30) NOT NULL,  -- Running|Completed|Failed|Compensated
    worker_id          VARCHAR(100) NOT NULL,
    attempt_count      INT NOT NULL DEFAULT 1,
    started_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    finished_at        TIMESTAMPTZ NULL,
    last_heartbeat_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    error              TEXT NULL,
    UNIQUE (run_id, step_no, attempt_count)
);
-- + outbox_messages ve processed_messages (bkz. §5.3)
```

> İki ayrı veritabanı bilinçli tercih: "servis başına veritabanı" prensibinin demo'su. Compose'da tek Postgres container'ı içinde iki database olarak koşar (RAM tasarrufu), K8s fazında istenirse ayrılır.

---

## 7. Control Plane REST API

| Method | Endpoint | Açıklama |
|---|---|---|
| POST | `/api/jobs` | Job + adım tanımı oluştur |
| GET | `/api/jobs` | Tanımlı job listesi |
| POST | `/api/jobs/{name}/run` | Manuel tetikle → 202 Accepted + runId |
| GET | `/api/runs/{runId}` | Run durumu + adım timeline'ı (projeksiyon) |
| GET | `/api/runs?status=Failed` | Filtreli run listesi |
| POST | `/api/runs/{runId}/retry` | Failed run'ı baştan tetikle (yeni runId) |
| GET | `/healthz` `/readyz` | K8s liveness / readiness probe'ları |

Blazor dashboard aynı API'yi tüketir: job listesi, canlı run timeline'ı (SignalR ile push), failed run'larda compensation zincirinin görseli.

---

## 8. Elasticsearch Entegrasyonu

### 8.1 Akış
Worker'lar her adım olayında `flowforge.job.logs` topic'ine yapısal kayıt basar → LogIndexer bulk olarak indeksler.

### 8.2 Index Şablonu — `flowforge-logs-*`

```json
{
  "mappings": {
    "properties": {
      "runId":     { "type": "keyword" },
      "jobName":   { "type": "keyword" },
      "stepNo":    { "type": "integer" },
      "stepType":  { "type": "keyword" },
      "level":     { "type": "keyword" },
      "workerId":  { "type": "keyword" },
      "message":   { "type": "text" },
      "error":     { "type": "text" },
      "attempt":   { "type": "integer" },
      "durationMs":{ "type": "long" },
      "timestamp": { "type": "date" }
    }
  }
}
```

### 8.3 Gösterilecek Yetenekler
- `runId` ile uçtan uca timeline sorgusu (Kibana Discover)
- Aggregation: step_type bazında ortalama durationMs, worker bazında hata oranı
- README'ye 1-2 Kibana ekran görüntüsü → "NoSQL/Elasticsearch hands-on" kanıtı

---

## 9. Observability (OpenTelemetry + Prometheus + Grafana)

- **Trace:** `traceparent` header'ı event zarfında taşınır → bir run'ın tüm adımları tek trace altında birleşir (ControlPlane → Kafka → Worker-1 → Kafka → Worker-2...). Exporter: OTLP → Collector.
- **Metrik (Prometheus):** `flowforge_steps_completed_total`, `flowforge_step_duration_seconds` (histogram), `flowforge_outbox_lag` (yayınlanmamış outbox kaydı sayısı — özgün ve mülakatta etkileyici bir metrik), Kafka consumer lag (Collector receiver ile).
- **Grafana dashboard'ları:** (1) Run throughput & hata oranı, (2) Step süre dağılımı p50/p95, (3) Outbox lag + consumer lag, (4) Worker başına aktif adım.
- Sağlık: tüm servislerde `AddHealthChecks()` + Kafka/Postgres/ES bağımlılık check'leri.

---

## 10. Test Stratejisi

| Katman | Araç | Kapsam |
|---|---|---|
| Unit | xUnit + NSubstitute | Saga geçiş mantığı, retry policy, event (de)serileştirme |
| Integration | **Testcontainers** (Kafka + PostgreSQL container'ları testte gerçek ayağa kalkar) | Outbox publish→consume zinciri, idempotency (aynı mesaj 2× → tek işlem), compensation zinciri uçtan uca |
| E2E (smoke) | docker compose + script | `POST run` → 30 sn içinde `Completed` bekle |

Örnek kritik integration test isimleri (README'de listelenecek):
- `Outbox_event_survives_kafka_downtime`
- `Duplicate_message_is_processed_exactly_once`
- `Failed_step_triggers_full_compensation_chain`

Bu üç test ismi tek başına "testing culture" sinyali verir.

---

## 11. Docker Compose (Faz 1-3 Lokal Ortam)

```yaml
services:
  kafka:
    image: apache/kafka:3.9.0            # KRaft mode — Zookeeper YOK
    ports: ["9092:9092"]
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@kafka:9093
      KAFKA_LISTENERS: PLAINTEXT://:9092,CONTROLLER://:9093
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
    deploy: { resources: { limits: { memory: 1g } } }

  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    ports: ["8080:8080"]
    environment:
      KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS: kafka:9092

  postgres:
    image: postgres:17
    ports: ["5432:5432"]
    environment: { POSTGRES_PASSWORD: flowforge }
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./infra/init-dbs.sql:/docker-entrypoint-initdb.d/init.sql  # control_db + worker_db

  elasticsearch:
    image: elasticsearch:8.17.0
    ports: ["9200:9200"]
    environment:
      discovery.type: single-node
      xpack.security.enabled: "false"
      ES_JAVA_OPTS: "-Xms1g -Xmx1g"
    deploy: { resources: { limits: { memory: 2g } } }

  kibana:
    image: kibana:8.17.0
    ports: ["5601:5601"]
    environment: { ELASTICSEARCH_HOSTS: http://elasticsearch:9200 }

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    volumes: [ ./infra/otel-config.yaml:/etc/otelcol-contrib/config.yaml ]

  prometheus:
    image: prom/prometheus:latest
    ports: ["9090:9090"]
    volumes: [ ./infra/prometheus.yml:/etc/prometheus/prometheus.yml ]

  grafana:
    image: grafana/grafana:latest
    ports: ["3000:3000"]

  controlplane:
    build: ./src/FlowForge.ControlPlane
    ports: ["5000:8080"]
    depends_on: [kafka, postgres]

  worker:
    build: ./src/FlowForge.Worker
    deploy: { replicas: 3 }              # 3 worker → rebalancing lokalde bile izlenir
    depends_on: [kafka, postgres]

  logindexer:
    build: ./src/FlowForge.LogIndexer
    depends_on: [kafka, elasticsearch]

volumes:
  pgdata:
```

**Tahmini RAM bütçesi (32 GB makinede):** Kafka ~1 GB, ES ~2 GB, Kibana ~0.7 GB, Postgres ~0.3 GB, Grafana+Prometheus+Collector ~0.7 GB, .NET servisleri ~1 GB, toplam **~6 GB** → bolca pay var. `.wslconfig` ile WSL'e 20 GB tavan koy.

---

## 12. Kubernetes Fazı (Faz 4 — k3d önerilir)

- **Cluster:** `k3d cluster create flowforge --agents 2`
- **Manifest seti (`/k8s`):** Namespace, ConfigMap (bağlantı stringleri), Secret, Deployment×4 (controlplane, worker, logindexer, kafka-ui), StatefulSet (kafka, postgres, elasticsearch — demo amaçlı in-cluster), Service'ler, Ingress (controlplane + kibana + grafana).
- **Probe'lar:** liveness `/healthz`, readiness `/readyz` (Kafka+DB bağlantısı hazır mı).
- **HPA:** Worker deployment'ı CPU %70 hedefli `minReplicas: 2, maxReplicas: 6`. Demo: 50 run'lık yük bas → worker'ların 2→5'e ölçeklenişini `kubectl get hpa -w` ile izle.
- **Chaos demo:** §2.4'teki pod kill senaryosu — kayıt al, README'ye GIF koy.
- (Opsiyonel 5. faz) Helm chart'a çevirme: values.yaml ile replica/kaynak parametreleri.

---

## 13. Repo Yapısı

```
flowforge/
├── README.md                  # vitrin: diyagram, GIF, trade-off yazıları, test listesi
├── docs/
│   ├── architecture.md        # bu dokümanın evrilmiş hali
│   ├── adr/                   # Architecture Decision Records
│   │   ├── 001-choreography-vs-orchestration.md
│   │   ├── 002-outbox-vs-direct-publish.md
│   │   └── 003-sql-queue-vs-kafka.md      # Marubeni kıyası — en değerli ADR
├── src/
│   ├── FlowForge.Contracts/
│   ├── FlowForge.ControlPlane/
│   ├── FlowForge.Worker/
│   └── FlowForge.LogIndexer/
├── tests/
│   ├── FlowForge.UnitTests/
│   └── FlowForge.IntegrationTests/        # Testcontainers
├── infra/                      # init-dbs.sql, otel-config, prometheus.yml, grafana json
├── k8s/                        # Faz 4 manifest'leri
├── docker-compose.yml
├── docker-compose.override.yml # sadece infra (servisleri IDE'den koşmak için)
└── .github/workflows/ci.yml    # build + unit test + (nightly) integration test
```

**ADR klasörü neden önemli:** Trendyol gibi craftsmanship vurgusu yapan şirketlerde "karar verme biçimini" gösterir. 3 kısa ADR, 1000 satır koddan daha çok konuşur.

---

## 14. Faz Planı ve Kabul Kriterleri

| Faz | Süre | Teslimat | Kabul kriteri (demo'lanabilir) |
|---|---|---|---|
| **1** | 2 hafta | ControlPlane + 1 Worker + Kafka + Outbox + Compose | `POST /run` → 4 adım sırayla tamamlanır, outbox lag 0'a iner, Kafka UI'da eventler görünür |
| **2** | 1-2 hafta | 3 worker, retry/DLQ, idempotency, saga compensation | Chaos flag açıkken failed run tam telafi zinciri üretir; duplicate mesaj testi yeşil |
| **3** | 1 hafta | LogIndexer + Elasticsearch + Kibana | runId ile Kibana'da uçtan uca timeline; aggregation sorgusu |
| **4** | 1-2 hafta | k3d deploy + HPA + chaos GIF | Pod kill → job kesintisiz tamamlanır; HPA 2→5 ölçeklenme izlenir |
| **5** | 1 hafta | Testcontainers suite + CI + README cilası | 3 kritik integration test yeşil; CI badge; mimari diyagram + GIF'ler README'de |

Her faz sonu = bağımsız commit/tag (`v0.1`–`v0.5`) → repo hangi noktada bırakılırsa bırakılsın "bitmiş" görünür.

---

## 15. README / Mülakat Anlatı Notları

1. **Açılış cümlesi:** "I built a SQL-queue-based orchestrator in production; FlowForge is my redesign of the same problem for distributed scale." — İngilizce mülakat için bu cümleyi ezberle, hikâyenin tamamı buradan açılır.
2. **Trade-off soruları için hazır cevaplar:** choreography vs orchestration (ADR-001), outbox vs direct publish (ADR-002), SQL queue vs Kafka — hangi ölçekte hangisi (ADR-003), at-least-once + idempotency vs exactly-once iddiası.
3. **Partition sorusu:** "key=runId → run içi sıra garantisi + run'lar arası paralellik" (§4.1).
4. **YouTube serisi eşlemesi (@silbastankodlama):** Faz 1 → "SQL Queue'dan Kafka'ya: Outbox Pattern", Faz 2 → "Saga ile Dağıtık Telafi", Faz 3 → "Kendi Log API'mden Elasticsearch'e", Faz 4 → "Worker'ımı Öldürdüm, Job Hayatta Kaldı (K8s)". Her faz bir video, repo linki açıklamada.
5. **Sınır:** Projede Marubeni'ye ait hiçbir isim, veri, ekran, iş kuralı kullanılmaz; kıyas yalnızca "önceki işimde tek-sunuculu SQL-backed queue tasarlamıştım" genelliğinde kalır.

---

*Doküman sonu — v1.0. Faz 1 başlarken solution iskeleti ve ilk migration'lar bu dokümandan türetilecek.*
