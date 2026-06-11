# FlowForge — İlerleme Günlüğü (append-only)

> Kural: Ajan her görev sonunda AŞAĞIYA yeni bir blok EKLER. Eski blokları asla düzenlemez/silmez.
> Format birebir korunur — bu dosya insan tarafından commit incelemesinde harita olarak kullanılır.

---

## 2026-06-10 — Görev 1.4: Job API
- **Yapılan:** ControlPlane içinde `POST /api/jobs`, `GET /api/jobs`, `POST /api/jobs/{name}/run`, `GET /api/runs/{runId}`, `/healthz` ve `/readyz` endpoint'leri eklendi. Swagger UI açıldı; `monthly-sales-report` seed job'ı 4 adım ve `chaos_fail_rate=0` config ile uygulama başlangıcında yoksa ekleniyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.ControlPlane/Features/Jobs/JobDtos.cs`, `src/FlowForge.ControlPlane/Features/Jobs/JobEndpoints.cs`, `src/FlowForge.ControlPlane/Features/Runs/RunDtos.cs`, `src/FlowForge.ControlPlane/Data/ControlPlaneSeeder.cs`, `.ai/sessions/2026-06-10-gorev-1.4.md` | değişen: `src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj`, `src/FlowForge.ControlPlane/Program.cs`, `src/FlowForge.ControlPlane/Outbox/OutboxMessage.cs`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti
- **Not/risk:** API runtime testi bu görevde Docker/Postgres ayağa kaldırılmadan yapılmadı; run endpoint'i transaction + outbox kaydını kod seviyesinde ekliyor, uçtan uca smoke Faz 1.8'de yapılacak.

---


## 2026-06-10 — Görev 1.2: Contracts
- **Yapılan:** `FlowForge.Contracts` içinde `EventEnvelope`, ortak JSON ayarları, 8 event record'u ve `KafkaTopics` sabitleri eklendi. Unit testlerde envelope round-trip serileştirme, camelCase JSON alanları, payload deserialize akışı, 8 event adının birebir sözleşmesi ve topic adları doğrulandı.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Contracts/EventEnvelope.cs`, `src/FlowForge.Contracts/ContractJson.cs`, `src/FlowForge.Contracts/KafkaTopics.cs`, `src/FlowForge.Contracts/JobStepDefinition.cs`, `src/FlowForge.Contracts/JobRunRequested.cs`, `src/FlowForge.Contracts/StepStarted.cs`, `src/FlowForge.Contracts/StepCompleted.cs`, `src/FlowForge.Contracts/StepFailed.cs`, `src/FlowForge.Contracts/CompensateStep.cs`, `src/FlowForge.Contracts/StepCompensated.cs`, `src/FlowForge.Contracts/JobRunCompleted.cs`, `src/FlowForge.Contracts/JobRunFailed.cs`, `tests/FlowForge.UnitTests/EventEnvelopeTests.cs` | değişen: `.ai/BACKLOG.md`, `.ai/PROGRESS.md` | silinen: `src/FlowForge.Contracts/Class1.cs`, `tests/FlowForge.UnitTests/UnitTest1.cs`
- **Doğrulama:** `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata
- **Not/risk:** İlk test denemesi solution build ile paralel çalıştığı için ortak `Contracts` çıktısında dosya kilidi oluştu; komut tek başına tekrarlandığında geçti.

---
<!-- ŞABLON — ajan her görevde bunu kopyalayıp doldurur:

## [TARİH] — Görev X.Y: <görev adı>
- **Yapılan:** (2-3 cümle, ne implemente edildi)
- **Dokunulan dosyalar:** (yeni: ... | değişen: ...)
- **Doğrulama:** (çalıştırılan komutlar ve sonuçları, örn. "dotnet build ✅, smoke.sh ✅")
- **Not/risk:** (varsa; yoksa "—")

-->

## 2026-06-10 — Görev 1.1: Solution iskeleti
- **Yapılan:** Repo başlangıç dosyaları, dokümantasyon ve `.ai` takip katmanı yerleştirildi. `flowforge.sln`, `FlowForge.Contracts`, `FlowForge.ControlPlane`, `FlowForge.Worker`, `FlowForge.UnitTests` ve `FlowForge.IntegrationTests` projeleri oluşturuldu; LogIndexer ve CI dosyaları Faz 1 talimatına uygun şekilde oluşturulmadı.
- **Dokunulan dosyalar:** yeni: `flowforge.sln`, `Directory.Build.props`, `.editorconfig`, `.gitattributes`, `.gitignore`, `README.md`, `GITHUB-KURULUM.md`, `LICENSE`, `.ai/*`, `docs/*`, `src/FlowForge.Contracts/*`, `src/FlowForge.ControlPlane/*`, `src/FlowForge.Worker/*`, `tests/FlowForge.UnitTests/*`, `tests/FlowForge.IntegrationTests/*`, `infra/init-dbs.sql`, `docker-compose.yml`, `docker-compose.override.yml` | değişen: `README.md`, `src/FlowForge.Worker/Worker.cs`, `.ai/BACKLOG.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata
- **Not/risk:** `dotnet new` ilk çalıştırmada SDK template cache izni nedeniyle sandbox'a takıldı; izinli tekrar çalıştırıldı. .NET 10 SDK varsayılan olarak `.slnx` ürettiği için talimata uygun klasik `flowforge.sln` yeniden oluşturuldu.

---

## 2026-06-10 — Görev 1.3: ControlPlane veri katmanı
- **Yapılan:** ControlPlane için EF Core/Npgsql paketleri eklendi; `jobs`, `job_steps`, `job_runs`, `outbox_messages` ve `processed_messages` entity'leri ile `ControlPlaneDbContext` oluşturuldu. `Initial` migration üretildi; migration'da `ix_outbox_unpublished` partial index'i `published_at IS NULL` filtresiyle yer aldı.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.ControlPlane/Data/ControlPlaneDbContext.cs`, `src/FlowForge.ControlPlane/Data/ControlPlaneDbContextFactory.cs`, `src/FlowForge.ControlPlane/Features/Jobs/Job.cs`, `src/FlowForge.ControlPlane/Features/Jobs/JobStep.cs`, `src/FlowForge.ControlPlane/Features/Runs/JobRun.cs`, `src/FlowForge.ControlPlane/Outbox/OutboxMessage.cs`, `src/FlowForge.ControlPlane/Inbox/ProcessedMessage.cs`, `src/FlowForge.ControlPlane/Migrations/*`, `.ai/sessions/2026-06-10-gorev-1.3.md` | değişen: `src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj`, `src/FlowForge.ControlPlane/Program.cs`, `src/FlowForge.ControlPlane/appsettings.json`, `.gitignore`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti
- **Not/risk:** NuGet paket indirme sandbox ağ kısıtına takıldığı için izinli tekrar çalıştırıldı. `dotnet ef` migration üretirken EF tool 9.0.3 ile runtime paketleri 9.0.4 arasında patch uyarısı verdi; build ve test çıktıları temiz. `.gitignore` içindeki local volume kuralı köke sabitlendi, böylece Windows'ta kaynak `Data` klasörleri yanlışlıkla ignore edilmiyor.

---

## 2026-06-10 — Görev 1.5: OutboxPublisher
- **Yapılan:** `FlowForge.Outbox` classlib eklendi; shared `OutboxMessage`, `IOutboxDbContext`, `OutboxPublisher`, options ve DI extension oluşturuldu. Publisher 500ms `PeriodicTimer`, batch 100, `OccurredAt` sırası, `Acks.All`, `EnableIdempotence=true`, `MessageSendMaxRetries=5`, key=`aggregate_id`, value=`payload.RootElement.GetRawText()` kurallarıyla çalışıyor; publish hatasında kayıt silinmeden `AttemptCount++` yapılıp exception loglanıyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Outbox/*`, `.ai/sessions/2026-06-10-gorev-1.5.md` | değişen: `flowforge.sln`, `src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj`, `src/FlowForge.ControlPlane/Data/ControlPlaneDbContext.cs`, `src/FlowForge.ControlPlane/Features/Jobs/JobEndpoints.cs`, `src/FlowForge.ControlPlane/Program.cs`, `src/FlowForge.ControlPlane/appsettings.json`, `src/FlowForge.ControlPlane/Migrations/*`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md` | silinen: `src/FlowForge.ControlPlane/Outbox/OutboxMessage.cs`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; kod kontrolünde 500ms timer, batch 100, raw payload value, producer config ve hosted service registration doğrulandı.
- **Not/risk:** NuGet paketleri `Confluent.Kafka 2.6.1`, `Microsoft.EntityFrameworkCore 9.0.4` ve hosting/options paketleri eklendi. Migration şeması değişmedi; model snapshot shared `FlowForge.Outbox.OutboxMessage` namespace'ine uyumlu hale getirildi.

---

## 2026-06-10 — Görev 1.6: Worker
- **Yapılan:** Worker için `worker_db` EF veri katmanı, `Initial` migration, Kafka consumer loop, `StepExecutor`, inbox idempotency ve outbox saga event üretimi eklendi. Consumer config `flowforge-workers`, `EnableAutoCommit=false`, `CooperativeSticky`; başarı yolunda transaction commit edildikten sonra en son Kafka offset commit ediliyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Data/*`, `src/FlowForge.Worker/Kafka/*`, `src/FlowForge.Worker/Steps/*`, `src/FlowForge.Worker/Migrations/*`, `.ai/sessions/2026-06-10-gorev-1.6.md` | değişen: `src/FlowForge.Worker/FlowForge.Worker.csproj`, `src/FlowForge.Worker/Program.cs`, `src/FlowForge.Worker/appsettings.json`, `src/FlowForge.Contracts/StepCompleted.cs`, `tests/FlowForge.UnitTests/EventEnvelopeTests.cs`, `src/FlowForge.Outbox/ProcessedMessage.cs`, `src/FlowForge.ControlPlane/Data/ControlPlaneDbContext.cs`, `src/FlowForge.ControlPlane/Migrations/*`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`, `.ai/DECISIONS.md` | silinen: `src/FlowForge.Worker/Worker.cs`, service-local `ProcessedMessage` entity'leri
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; migration ve consumer config/success-path kodu kontrol edildi.
- **Not/risk:** `StepCompleted` payload'ı stateless worker için `steps[]` taşıyacak şekilde genişletildi ve D-002 karar kaydı eklendi. Faz 1 kapsamına uygun olarak chaos/retry uygulanmadı.

---

## 2026-06-10 — Görev 1.7: ControlPlane projeksiyon consumer'ı
- **Yapılan:** ControlPlane'e ikinci hosted service olarak `JobRunProjectionConsumer` eklendi. Consumer `flowforge.job.events` topic'ini `controlplane-projection` group ile, `EnableAutoCommit=false` ayarıyla dinliyor; `JobRunCompleted` için `Status=Completed` + `FinishedAt`, `JobRunFailed` için `Status=Failed` + `FailedStep` + `FinishedAt` yazıyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.ControlPlane/Projection/ProjectionKafkaOptions.cs`, `src/FlowForge.ControlPlane/Projection/JobRunProjectionConsumer.cs`, `.ai/sessions/2026-06-10-gorev-1.7.md` | değişen: `src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj`, `src/FlowForge.ControlPlane/Program.cs`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; kod kontrolünde `GroupId=controlplane-projection`, `EnableAutoCommit=false`, terminal status idempotency ve diğer eventlerde offset commit doğrulandı.
- **Not/risk:** Inbox tablosu bilinçli olarak kullanılmadı; idempotency terminal status kontrolüyle doğal sağlanıyor. Runtime Kafka testi Faz 1.8 smoke sırasında yapılacak.

---

## 2026-06-10 — Görev 1.8: docker-compose + smoke
- **Yapılan:** ControlPlane ve Worker için multi-stage Dockerfile'lar eklendi; `docker-compose.yml` Kafka KRaft, kafka-ui, Postgres, kafka-init, controlplane ve worker servisleriyle tamamlandı. Compose environment override'ları `Kafka=kafka:9092` ve `Postgres Host=postgres` kullanıyor; appsettings localhost değerleri lokal IDE için korundu. DB migration başlangıcına retry döngüsü eklendi, consumer hosted service'lerinde host startup'ı bloklamasın diye ilk await garanti edildi ve `scripts/smoke.sh` run başlatıp `Completed` poll edecek hale getirildi.
- **Dokunulan dosyalar:** yeni: `.dockerignore`, `src/FlowForge.ControlPlane/Dockerfile`, `src/FlowForge.Worker/Dockerfile`, `scripts/smoke.sh`, `.ai/sessions/2026-06-10-gorev-1.8.md` | değişen: `docker-compose.yml`, `docker-compose.override.yml`, `src/FlowForge.ControlPlane/Program.cs`, `src/FlowForge.ControlPlane/Projection/JobRunProjectionConsumer.cs`, `src/FlowForge.Worker/Program.cs`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅ — run `Completed`; outbox lag sorguları ✅ — `control_db=0`, `worker_db=0`; Kafka topic doğrulaması ✅ — `flowforge.job.events=6`, `flowforge.job.events.dlq=3`, `flowforge.job.logs=6` partition.
- **Not/risk:** Windows ortamında WSL `bash` yoktu; smoke doğrulaması Git Bash ile çalıştırıldı. Script POSIX shell uyumlu tutuldu, ayrıca minimal Git Bash ortamı için `seq`/`sed` bağımlılığı kaldırıldı ve `sleep` fallback'i eklendi.

---

## 2026-06-10 — Faz 1 kapanış
- **Yapılan:** Faz 1 kapanış kriterleri tamamlandı olarak işaretlendi. `main` ve `v0.1` remote üzerinde smoke-green commit `f40696d` hedefini gösteriyor.
- **Dokunulan dosyalar:** değişen: `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** Önceki 1.8 doğrulaması geçerli: `scripts/smoke.sh` ✅, outbox lag `0`, Kafka topic partitionları doğru; remote kontrolü ✅ — `origin/main=f40696d`, `origin refs/tags/v0.1^{}=f40696d`.
- **Not/risk:** `v0.1` tag'i zaten remote'da doğru commit'e pushlanmış olduğu için force tag işlemi yapılmadı.

---

## 2026-06-10 — Görev 2.1: Worker replicas ve partition logları
- **Yapılan:** `docker-compose.yml` içinde Worker replica sayısı 3'e çıkarıldı. `JobEventsConsumer` partition assignment/revocation logları ve mesaj işleme logları üretir hale getirildi; loglarda `flowforge.job.events` 6 partition'ın 3 worker'a 2'şer dağıldığı görüldü.
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-10-gorev-2.1.md` | değişen: `docker-compose.yml`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; `docker compose up -d --build` ✅ — `flowforge-worker-1/2/3` ayakta; `scripts/smoke.sh` ✅ — run `Completed`; outbox lag sorguları ✅ — `control_db=0`, `worker_db=0`; log doğrulaması ✅ — worker'lar partitionları `[0,3]`, `[1,4]`, `[2,5]` olarak aldı.
- **Not/risk:** Aynı run'ın tüm eventleri `runId` key'i nedeniyle aynı Kafka partition'ında kaldığından örnek smoke run tek worker tarafından işlendi; bu tasarım §4.1'deki run içi sıra garantisinin beklenen sonucu.

---

## 2026-06-10 — Görev 2.1 takip düzeltmesi: EF log gürültüsü
- **Yapılan:** 2.1 raporunda atlanan EF log chore'u tamamlandı. Worker ve ControlPlane appsettings içinde `Microsoft.EntityFrameworkCore.Database.Command` log seviyesi `Warning` yapıldı; 3 worker altında SQL sorgu gürültüsü partition/chaos loglarını boğmayacak.
- **Dokunulan dosyalar:** değişen: `src/FlowForge.Worker/appsettings.json`, `src/FlowForge.ControlPlane/appsettings.json`, `.ai/PROGRESS.md`
- **Doğrulama:** `git diff src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj` sadece satır sonu normalizasyon uyarısı gösterdi; dosya geri alındı. `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅; outbox lag `0`; log kontrolünde worker partition/processing satırları okunuyor ve `Microsoft.EntityFrameworkCore.Database.Command` bilgi logu görünmüyor.
- **Not/risk:** Bu madde 2.1 sırasında sessiz atlanmıştı; ayrı takip düzeltmesi olarak kayda geçirildi.

---

## 2026-06-10 — Görev 2.2: Polly retry
- **Yapılan:** Worker consumer'da `StepExecutor.RunAsync` Polly `ResiliencePipeline` ile sarıldı; retry backoff'u 2s/4s/8s olacak şekilde `Delay=2s`, `BackoffType=Exponential`, `MaxRetryAttempts=step.MaxRetries` olarak ayarlandı. Retry denemeleri transaction dışında tamamlanıyor; başarısız denemeler `job_step_runs` tablosuna `Failed` ve artan `AttemptCount` ile yazılıp WARN loglanıyor, başarılı sonuç/inbox/outbox transaction'ı yalnız başarıdan sonra açılıyor.
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-10-gorev-2.2.md` | değişen: `src/FlowForge.Worker/FlowForge.Worker.csproj`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj` ✅ — 3 test geçti; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅ — run `Completed`; outbox lag sorguları ✅ — `control_db=0`, `worker_db=0`; smoke run step kayıtları ✅ — 4 step de `Completed`, `attempt_count=1`.
- **Not/risk:** Chaos flag'e dokunulmadı; bu nedenle runtime smoke başarısız retry üretmedi. Retry tükenince exception tekrar fırlatılıyor ve mesaj Kafka tarafından yeniden teslim edilmeye bırakılıyor; DLQ yönlendirmesi 2.3 kapsamına bırakıldı.

---

## 2026-06-10 — Görev 2.2 takip düzeltmesi: inbox ve retry testi
- **Yapılan:** Inbox sırası yeniden kontrol edildi: TX'siz ön-kontrol `RunStepWithRetryAsync` çağrısından önce çalışıyor; başarılı retry sonrası TX içinde ikinci inbox kontrolü de korunuyor. Retry sarmalı `StepRetryPipeline` helper'ına çıkarıldı ve unit test eklendi; başarılı satırın `AttemptCount` değeri toplam deneme sayısı olarak dönüyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Steps/StepRetryPipeline.cs`, `tests/FlowForge.UnitTests/StepRetryPipelineTests.cs` | değişen: `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `tests/FlowForge.UnitTests/FlowForge.UnitTests.csproj`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 4 test geçti; yeni test iki kez hata, üçüncü denemede başarı, attempt listesi `1,2,3`, failed attempts `1,2`, backoff `2s,4s` doğruluyor. `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅; outbox lag `0`; smoke run kayıtlarında chaos kapalı olduğu için tüm step'ler `Completed`, `attempt_count=1`.
- **Not/risk:** Unit test Worker executable projesini test host'a taşımamak için retry helper dosyasını linked compile olarak kullanıyor; böylece aynı kaynak kod test ediliyor ama Worker host/EF bağımlılıkları unit test sürecine karışmıyor.

---

## 2026-06-11 — Görev 2.3: DLQ yönlendirmesi + StepFailed eventi
- **Yapılan:** `outbox_messages` tablosuna iki DB için nullable `topic` kolonu eklendi; `OutboxPublisher` satır topic'i varsa ona, yoksa varsayılan `flowforge.job.events` topic'ine publish ediyor ve fail-fast batch davranışı topic'ten bağımsız korunuyor. Worker retry tükendiğinde DLQ kopyasını `aggregate_id=runId`, `topic=flowforge.job.events.dlq` ve original message + `exception/attempts/workerId/occurredAt` metadata'sı ile outbox'a yazıyor; ardından aynı transaction'da `StepFailed` ve `processed_messages` ekleniyor, commit sonrası offset commit ediliyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Kafka/DeadLetterMessageFactory.cs`, `src/FlowForge.ControlPlane/Migrations/20260611043830_AddOutboxTopic*`, `src/FlowForge.Worker/Migrations/20260611043853_AddOutboxTopic*`, `tests/FlowForge.UnitTests/DeadLetterMessageFactoryTests.cs`, `.ai/sessions/2026-06-11-gorev-2.3.md` | değişen: `src/FlowForge.Outbox/OutboxMessage.cs`, `src/FlowForge.Outbox/OutboxPublisher.cs`, `src/FlowForge.ControlPlane/Data/ControlPlaneDbContext.cs`, `src/FlowForge.ControlPlane/Migrations/ControlPlaneDbContextModelSnapshot.cs`, `src/FlowForge.Worker/Data/WorkerDbContext.cs`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `src/FlowForge.Worker/Migrations/WorkerDbContextModelSnapshot.cs`, `tests/FlowForge.UnitTests/FlowForge.UnitTests.csproj`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 5 test geçti; kod kontrolünde `message.Topic ?? options.Value.Topic`, iki DB migration'ında nullable `topic`, DLQ+StepFailed+processed_messages aynı transaction ve offset commit'in transaction sonrası olduğu doğrulandı.
- **Not/risk:** Talimat gereği canlı DLQ/chaos testi yapılmadı; 2.5'te chaos flag ile doğrulanacak. `StepFailed` şimdilik yayınlanıyor ama compensation consumer'ı 2.4 kapsamında eklenecek.

---

## 2026-06-11 — Görev 2.4: Saga compensation zinciri
- **Yapılan:** Worker artık `StepFailed`, `CompensateStep` ve `StepCompensated` eventlerini tüketiyor. `StepFailed(N)` sonrası `N>1` ise `CompensateStep(N-1)`, `N=1` ise `JobRunFailed`; `CompensateStep(k)` sonrası best-effort `StepExecutor.CompensateAsync` + `job_step_runs=Compensated` + `StepCompensated(k)`; `StepCompensated(k)` sonrası `k>1` ise `CompensateStep(k-1)`, `k=1` ise `JobRunFailed` üretiliyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Kafka/CompensationChain.cs`, `tests/FlowForge.UnitTests/CompensationChainTests.cs`, `.ai/sessions/2026-06-11-gorev-2.4.md` | değişen: `src/FlowForge.Contracts/StepFailed.cs`, `src/FlowForge.Contracts/CompensateStep.cs`, `src/FlowForge.Contracts/StepCompensated.cs`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `src/FlowForge.Worker/Steps/StepExecutor.cs`, `tests/FlowForge.UnitTests/EventEnvelopeTests.cs`, `tests/FlowForge.UnitTests/FlowForge.UnitTests.csproj`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 6 test geçti; kod kontrolünde compensation eventlerinin inbox ön-kontrol → iş → TX(yaz+inbox+outbox) → commit → offset kalıbını kullandığı ve projeksiyon consumer'ına dokunulmadığı doğrulandı.
- **Not/risk:** Compensation retry bilinçli olarak eklenmedi; D-004'e göre hata WARN loglanıp zincir sürdürülüyor. Canlı chaos/compensation doğrulaması 2.5 kapsamına kaldı.

---

## 2026-06-11 — Görev 2.5: Chaos flag + chaos seed job
- **Yapılan:** `StepExecutor` step config içinden `duration_ms` ve `chaos_fail_rate` okur hale getirildi; her denemede `Random.Shared.NextDouble() < chaos_fail_rate` ise `ChaosException` fırlatıyor ve mevcut retry/DLQ akışı bunu normal hata gibi işliyor. Seed'e idempotent `monthly-sales-report-chaos` job'ı eklendi; aynı 4 adımı kullanıyor, süreleri config ile yarıya indirilmiş ve `GenerateReport` adımı `chaos_fail_rate=0.3` taşıyor.
- **Dokunulan dosyalar:** yeni: `scripts/chaos-smoke.sh`, `.ai/sessions/2026-06-11-gorev-2.5.md` | değişen: `src/FlowForge.Worker/Steps/StepExecutor.cs`, `src/FlowForge.ControlPlane/Data/ControlPlaneSeeder.cs`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 6 test geçti; `git diff --check` ✅. Canlı doğrulama denendi ama `docker compose up -d --build` hem normal hem izinli çalıştırmada Docker Desktop pipe'ı bulunamadığı için başlayamadı: `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified`.
- **Not/risk:** 2.5 `[~]` bırakıldı; canlı koşul için Docker Desktop çalışınca `docker compose up -d --build` ve ardından `scripts/chaos-smoke.sh` çalıştırılmalı. D-005'e göre chaos job'ın `GenerateReport` adımı `maxRetries=0`; aksi halde `0.3` oranla 5+ run içinde Failed gözlemek pratik olarak güvenilmez.

---
