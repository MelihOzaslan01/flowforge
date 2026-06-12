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

## 2026-06-11 — Görev 2.5 takip: canlı chaos doğrulaması
- **Yapılan:** Kullanıcı `scripts/chaos-smoke.sh`/canlı testlerin başarıyla geçtiğini bildirdi. 2.5 backlog durumu `[~]` → `[x]` olarak güncellendi.
- **Dokunulan dosyalar:** değişen: `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** Kullanıcı doğrulaması: chaos job 5+ tetikleme ve Failed run için DLQ, ters sıra compensation satırları, `job_runs.status=Failed`/`failed_step` kontrolleri başarılı.
- **Not/risk:** 2.6 Heartbeat + zombi temizleyiciye geçildi.

---

## 2026-06-11 — Görev 2.6: Heartbeat + zombi adım temizleyici
- **Yapılan:** Worker artık step çalıştırmadan önce `job_step_runs` içinde `Running` satırı açıyor; bu satır 5 sn'de bir heartbeat ile `last_heartbeat_at` güncelliyor ve başarı/başarısızlıkta aynı satır terminal duruma çekiliyor. Worker startup'ta `Running` ve heartbeat'i 60 sn'den eski satırları `Failed` yapan `ZombieStepCleaner` eklendi; cleaner aynı transaction'da `StepFailed` outbox kaydı ve kaynak mesaj için `processed_messages` kaydı yazıyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Steps/StepHeartbeat.cs`, `src/FlowForge.Worker/Steps/ZombieStepCleaner.cs`, `src/FlowForge.Worker/Migrations/20260611061029_AddJobStepRunSteps*`, `src/FlowForge.Worker/Migrations/20260611061220_AddJobStepRunSourceMessage*`, `.ai/sessions/2026-06-11-gorev-2.6.md` | değişen: `src/FlowForge.Worker/Steps/JobStepRun.cs`, `src/FlowForge.Worker/Data/WorkerDbContext.cs`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `src/FlowForge.Worker/Program.cs`, `src/FlowForge.Worker/Migrations/WorkerDbContextModelSnapshot.cs`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyarı, 0 hata; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 6 test geçti. Kod kontrolünde heartbeat intervalinin 5 sn, zombi eşiğinin 60 sn, cleaner transaction'ında `Failed` + `StepFailed` outbox + `processed_messages` yazıldığı doğrulandı.
- **Not/risk:** Zombi senaryosu canlı crash/pod-kill ile doğrulanmadı; ileride 4.3 chaos-pod-kill veya integration test kapsamına alınabilir.

---

## 2026-06-11 — Görev 2.6 takip düzeltmesi: periyodik zombi temizleyici ve commit kuralı
- **Yapılan:** `ZombieStepCleaner` yalnız startup'ta değil, startup sonrası 30 sn'de bir periyodik çalışacak hale getirildi. Redelivery kör noktası kapatıldı: aynı kaynak mesaj için aktif `Running` satır varsa devralan worker zombi eşiğine kadar bekliyor, stale ise aynı messageId'yi inbox'a yazıp `StepFailed` üreterek mesajı yeniden çalıştırmadan kapatıyor. Commit mesaj formatı kalıcı kural olarak `docs/implementation-guide.md` §0'a eklendi.
- **Dokunulan dosyalar:** değişen: `src/FlowForge.Worker/Steps/ZombieStepCleaner.cs`, `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `docs/implementation-guide.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅; `docker compose up -d --build` ✅; canlı zombi testi ✅ — step 2 worker'ı öldürüldü, `job_runs=Failed|2`, worker rows: `1|Completed`, `2|Failed|...Zombie step detected during redelivery...`, `1|Compensated`.
- **Not/risk:** İlk canlı kill step 1'e denk geldiği için compensation beklenmedi; ikinci test step 2 üzerinde compensation yolunu doğruladı.

---

## 2026-06-11 — Görev 2.7: Testcontainers integration testleri
- **Yapılan:** `FlowForge.IntegrationTests` projesi Testcontainers tabanli gercek PostgreSQL + Kafka fixture'iyle dolduruldu; collection fixture tek container setini paylasiyor, her test `MigrateAsync` + truncate + topic reset ile temiz basliyor. Tasarim §10'daki uc test birebir eklendi: `Outbox_event_survives_kafka_downtime`, `Duplicate_message_is_processed_exactly_once`, `Failed_step_triggers_full_compensation_chain`; testler gercek `OutboxPublisher`, `JobEventsConsumer`, `ZombieStepCleaner` ve `StepExecutor` kodunu host DI uzerinden calistiriyor.
- **Dokunulan dosyalar:** yeni: `tests/FlowForge.IntegrationTests/FlowForgeFixture.cs`, `tests/FlowForge.IntegrationTests/IntegrationTestHost.cs`, `tests/FlowForge.IntegrationTests/FlowForgeIntegrationTests.cs`, `.ai/sessions/2026-06-11-gorev-2.7.md` | degisen: `tests/FlowForge.IntegrationTests/FlowForge.IntegrationTests.csproj`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md` | silinen: `tests/FlowForge.IntegrationTests/UnitTest1.cs`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln` ✅ — 6 unit + 3 integration, toplam 9 test gecti; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅ — run `Completed`. Ara dogrulamada outbox downtime testi tek basina da ✅ gecti.
- **Not/risk:** Testcontainers.Kafka builder'in `GetBootstrapAddress()` yolu bu ortamda 9092 mapping'i uretmedigi icin Kafka container'i Testcontainers generic `ContainerBuilder` ile compose'taki bilinen Apache Kafka KRaft ayarlarina denk kuruldu; bu karar D-007'ye islendi ve kullanilmayan `Testcontainers.Kafka` paketi kaldirildi. Dis test listener'i sabit `localhost:19093`; lokal makinede bu port baska surec tarafindan kullanilirsa test fixture'i port cakismasi verebilir.

---

## 2026-06-11 — Görev 2.8: MudBlazor dashboard
- **Yapılan:** ControlPlane icine MudBlazor 7.15.0 ve Blazor Server dashboard eklendi; ayri proje acilmadi. `/` Jobs sayfasi job adi, adim sayisi, son 5 run durum chip'i ve Run Now butonunu gosteriyor; `/runs/{runId}` RunDetail sayfasi run status/requested/finished/failedStep bilgilerini ve `worker_db.job_step_runs` timeline'ini 5 sn polling ile yeniliyor, `Compensated` satirlari farkli renk/ikon/stil ile ayrisiyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.ControlPlane/Components/*`, `src/FlowForge.ControlPlane/Dashboard/DashboardQueries.cs`, `src/FlowForge.ControlPlane/Data/WorkerReadDbContext.cs`, `src/FlowForge.ControlPlane/Data/WorkerStepRunReadModel.cs`, `src/FlowForge.ControlPlane/Features/Jobs/JobRunStarter.cs`, `src/FlowForge.ControlPlane/wwwroot/app.css`, `.ai/sessions/2026-06-11-gorev-2.8.md` | degisen: `src/FlowForge.ControlPlane/FlowForge.ControlPlane.csproj`, `src/FlowForge.ControlPlane/Program.cs`, `src/FlowForge.ControlPlane/Features/Jobs/JobEndpoints.cs`, `src/FlowForge.ControlPlane/appsettings.json`, `docker-compose.yml`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\flowforge.sln --no-build` ✅ — 6 unit + 3 integration; `docker compose up -d --build` ✅; `http://localhost:5000/` ✅ — 200; `http://localhost:5000/runs/9204d40b-2e0a-447a-b682-d21e4689a075` ✅ — 200; `scripts/smoke.sh` ✅ — run `Completed`; worker_db timeline sorgusu ✅ — failed chaos run icin step 2 ve 1 `Compensated` satirlari gorundu.
- **Not/risk:** In-app browser `iab` ve local Playwright/Chrome/Edge bulunamadigi icin screenshot alinmadi; HTTP 200 + DB timeline + smoke ile canli dogrulama yapildi. Canli DB'de eski idempotent seed nedeniyle chaos job step 3 `max_retries=3` gorunuyor; mevcut failed chaos run uzerinden compensation timeline dogrulandi, seed verisine 2.8 kapsaminda dokunulmadi.

---

## 2026-06-11 — Faz 2 kapanış
- **Yapılan:** Faz 2 kapanis kriterleri tamamlandi olarak isaretlendi. Tum 2.1-2.8 maddeleri `[x]`; canli chaos failed run compensation timeline'i ve outbox lag kontrolleri yeniden dogrulandi.
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-11-faz-2-kapanis.md` | degisen: `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\flowforge.sln --no-build` ✅ — 6 unit + 3 integration; `docker compose ps` ✅ — controlplane, 3 worker, kafka, postgres, kafka-ui ayakta; `http://localhost:5000/` ✅ — 200; failed chaos run `9204d40b-2e0a-447a-b682-d21e4689a075` ✅ — `failed_step=3`, worker timeline `1 Completed`, `2 Completed`, `3 Failed x4`, `2 Compensated`, `1 Compensated`; outbox lag ✅ — `control_db=0`, `worker_db=0`; `scripts/smoke.sh` ✅ — run `Completed`.
- **Not/risk:** `v0.2` tag'i kapanis commit'ine lokal olarak eklenecek; push kullanici onayi gerektirir ve yapilmadi.

---

## 2026-06-11 — Görev 3.1: Worker yapısal log üretimi
- **Yapılan:** Worker step baslangic, basari, hata, zombi kapanisi ve compensation akislari icin `flowforge.job.logs` topic'ine outbox uzerinden `WorkerStepLog` kaydi uretir hale getirildi. Log payload'i tasarim §8.2 alanlarini tasiyor: `runId`, `jobName`, `stepNo`, `stepType`, `level`, `workerId`, `message`, `error`, `attempt`, `durationMs`, `timestamp`.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Kafka/WorkerStepLogFactory.cs`, `tests/FlowForge.UnitTests/WorkerStepLogFactoryTests.cs`, `.ai/sessions/2026-06-11-gorev-3.1.md` | degisen: `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `tests/FlowForge.UnitTests/FlowForge.UnitTests.csproj`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅ — run `180babcd-0d28-4078-bbbe-96281df25a6a` `Completed`; `worker_db.outbox_messages` ✅ — `flowforge.job.logs` kayitlari publish edilmis; Kafka `flowforge.job.logs` consumer ✅ — structured log mesajlari okundu.
- **Not/risk:** Worker eventleri job adini tasimadigi icin `jobName` su an `null`; index mapping bunu kabul eder. Gerekirse ileride event payload'i veya worker read modeliyle zenginlestirilebilir.

---

## 2026-06-11 — Görev 3.1 düzeltmesi: job logları outbox dışı
- **Yapılan:** 3.1'deki `WorkerStepLog` outbox kayitlari kaldirildi; Worker artik `JobLogPublisher` ile mevcut singleton Kafka producer'i paylasarak `flowforge.job.logs` topic'ine dogrudan fire-and-forget `Produce` ediyor. Key `runId`; delivery handler ve synchronous produce hatalari sadece WARN logluyor, publish await edilmiyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.Worker/Kafka/JobLogPublisher.cs`, `.ai/sessions/2026-06-11-gorev-3.1-duzeltme.md` | degisen: `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `src/FlowForge.Worker/Kafka/WorkerStepLogFactory.cs`, `src/FlowForge.Worker/Program.cs`, `tests/FlowForge.IntegrationTests/IntegrationTestHost.cs`, `tests/FlowForge.UnitTests/WorkerStepLogFactoryTests.cs`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `docker compose up -d --build` ✅; `scripts/smoke.sh` ✅ — run `ba325e73-16c1-435a-a48a-32d2139d074f` `Completed`; `worker_db.outbox_messages` ✅ — bu run icin `topic='flowforge.job.logs'` sayisi `0`; Kafka `flowforge.job.logs` ✅ — ayni run icin 8 structured start/completed log mesaji okundu.
- **Not/risk:** Ilk uygulamada DLQ icin kurulan nullable outbox `topic` desenini loglara da genelleyip ayni transaction guvencesi vermeyi secmistim; bu talimat sapmasiydi. Dogru karar D-009'a islendi: telemetri ve is/saga verisi farkli garanti siniflaridir; log outbox'u buyurse D-001 fail-fast davranisi nedeniyle saga event yayinini geciktirebilir.

---

## 2026-06-11 — Görev 3.2: FlowForge.LogIndexer
- **Yapılan:** `FlowForge.LogIndexer` .NET 9 Worker projesi eklendi ve solution'a dahil edildi. Servis `flowforge.job.logs` topic'ini `log-indexer` group ile `EnableAutoCommit=false` okuyup 500 kayit veya 2 sn dolunca tek ES bulk request'iyle `flowforge-logs-{yyyy.MM}` indexlerine yaziyor; bulk basarili olunca partition basina son offset commit ediliyor, hata olursa commit atilmadan batch retry ediliyor.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.LogIndexer/*`, `.ai/sessions/2026-06-11-gorev-3.2.md` | degisen: `flowforge.sln`, `docker-compose.yml`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `docker compose up -d --build` ✅ — Elasticsearch, Kibana ve LogIndexer ayakta; `scripts/smoke.sh` ✅ — run `014b84f8-8b30-484d-b1d5-ca131f7b4e41` `Completed`; `curl http://localhost:9200/flowforge-logs-*/_count` ✅ — `count=24`; `curl http://localhost:5601/api/status` ✅ — `200`; LogIndexer loglari ✅ — template hazirlandi, bulk batchler indexlendi ve offsetler commit edildi.
- **Not/risk:** 3.2 talimati compose'a ES/Kibana eklemeyi de kapsadigi icin compose bu gorevde degisti; ancak "SADECE 3.2" siniri nedeniyle backlog'daki 3.3 satiri isaretlenmedi. LogIndexer DB kullanmiyor; idempotency deterministik `_id` hash'i ile saglaniyor.

---

## 2026-06-11 — Görev 3.3: Kibana arama dokümantasyonu ve template doğrulama
- **Yapılan:** LogIndexer startup index template kodu ve canli Elasticsearch template'i kontrol edildi; tasarim §8.2 mapping'i birebir mevcut. README'ye `Searching logs in Kibana` mini bolumu eklendi: `flowforge-logs-*` data view kurulumu ve iki ornek KQL sorgusu.
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-11-gorev-3.3.md` | degisen: `README.md`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `curl http://localhost:9200/_index_template/flowforge-logs-template` ✅ — template mevcut; `runId`, `jobName`, `stepType`, `level`, `workerId` keyword; `message`, `error` text; `stepNo`, `attempt` integer; `durationMs` long; `timestamp` date. `git diff --check` ✅.
- **Not/risk:** Kod degisikligi gerekmedi; 3.2'deki template implementasyonu dogru oldugu icin 3.3 dokumantasyon ve canli dogrulama ile kapandi.

---

## 2026-06-11 — Görev 3.3 düzeltmesi: Worker log severity semantiği
- **Yapılan:** Retry attempt basarisizlik loglari `Error` yerine `Warning` seviyesine cekildi; zombi step logu da `Warning` seviyesinde kalacak sekilde duzeltildi. Retry tukenip mesaj DLQ'ya devredildikten sonra tek `Error` seviyeli structured log eklendi: `Step N exhausted after M attempts, moved to DLQ.`
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-11-gorev-3.3-log-level-duzeltme.md` | degisen: `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `docker compose up -d --build` ✅; chaos run `5b3aca2c-e8de-4ed9-8454-a1178acfd0c7` ✅ — `job_runs=Failed:3`, worker timeline `3 Failed` attempt `1..4` + compensation; ES aggregation ✅ — `Information=10`, `Warning=4`, `Error=1`; ES detay ✅ — 4 Warning attempt hatasi ve tek Error DLQ devri.
- **Not/risk:** `scripts/chaos-smoke.sh` Failed run urettikten sonra Git Bash/Docker exec kabuk uyumsuzlugu nedeniyle DB kontrol adiminda `/usr/bin/env: 'sh': No such file or directory` ile cikti; ayni run icin DB ve ES kontrolleri manuel komutlarla tamamlandi.

---

## 2026-06-11 — Görev 4.1: k3d manifest seti
- **Yapılan:** `/k8s` klasorune k3d hedefli manifest seti eklendi: `flowforge` namespace, ConfigMap/Secret, Kafka KRaft tek replica StatefulSet + headless/regular Service, Postgres StatefulSet + 1Gi PVC + init-dbs ConfigMap, Elasticsearch tek node, Kibana Deployment, kafka-init Job, ControlPlane Deployment/Service/Ingress, Worker `replicas=3`, LogIndexer Deployment. Probe ve HPA eklenmedi.
- **Dokunulan dosyalar:** yeni: `k8s/00-namespace.yaml`, `k8s/01-config.yaml`, `k8s/10-kafka.yaml`, `k8s/11-postgres.yaml`, `k8s/12-elasticsearch.yaml`, `k8s/13-kibana.yaml`, `k8s/20-kafka-init-job.yaml`, `k8s/30-controlplane.yaml`, `k8s/31-worker.yaml`, `k8s/32-logindexer.yaml`, `k8s/README-k8s.md`, `.ai/sessions/2026-06-11-gorev-4.1.md` | degisen: `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** k3d cluster container'lari mevcut ve `kubectl --kubeconfig C:\tmp\flowforge-kubeconfig.yaml get nodes` ✅ — server + 2 agent `Ready`; app image'lari 3 node'a import edildi ✅; `kubectl -n flowforge get pods` ✅ — runtime podlari `Running`, `kafka-init` `Completed`; `curl http://localhost:5001/api/jobs` ✅ — JSON dondu; run `4715e1e5-c73c-492d-8d29-426ca8edf0cd` ✅ — `Completed`.
- **Not/risk:** Lokal `k3d.exe` Windows'ta `Erisim engellendi` verdigi ve kube context bos oldugu icin README'deki `k3d image import` komutu birebir calistirilamadi. Dogrulama icin kubeconfig cluster container'indan gecici alindi, host portu `50307` yapildi; image import `docker save` + node `ctr -n k8s.io images import` ile esdeger sekilde tamamlandi.

---

## 2026-06-11 — Görev 4.2: Probe bağlantıları ve Worker HPA
- **Yapılan:** ControlPlane'e `/healthz` liveness ve `/readyz` readiness probe'lari eklendi; `/readyz` `control_db` baglantisini kontrol eden `ControlDbHealthCheck` ile gercek DB readiness oldu. Worker ve LogIndexer HTTP eklemeden `exec` liveness probe kullaniyor; Worker'a `requests: 100m/256Mi`, `limits: 500m/512Mi` ve HPA `cpu %70`, `min=2`, `max=6` eklendi.
- **Dokunulan dosyalar:** yeni: `src/FlowForge.ControlPlane/Data/ControlDbHealthCheck.cs`, `k8s/33-worker-hpa.yaml`, `.ai/sessions/2026-06-11-gorev-4.2.md` | degisen: `src/FlowForge.ControlPlane/Program.cs`, `k8s/30-controlplane.yaml`, `k8s/31-worker.yaml`, `k8s/32-logindexer.yaml`, `k8s/README-k8s.md`, `.ai/BACKLOG.md`, `.ai/DECISIONS.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `kubectl apply --dry-run=client -f k8s` ✅; k3d rollout ✅ — controlplane/worker/logindexer; `kubectl top pods` ✅; `kubectl get hpa worker` ✅ — `cpu: 7%/70%`, unknown degil; readiness kaniti ✅ — Postgres `scale=0` sonrasi `/readyz=503` ve controlplane `ready=false`, `scale=1` sonrasi `/readyz=200` ve `ready=true`.
- **Not/risk:** Worker/LogIndexer liveness probe secimi D-010'a islendi. ControlPlane image'i yeniden build edilip k3d node'larina import edildi; lokal `k3d.exe` erisim engelli oldugu icin gecici kubeconfig ile dogrulama yapildi.

---

## 2026-06-12 — Görev 4.3: Pod kill failover scriptleri
- **Yapılan:** `scripts/chaos-pod-kill.sh` eklendi; normal job'i tetikliyor, step 2 `Running` iken ilgili worker pod'unu `worker_db.job_step_runs.worker_id` uzerinden bulup siliyor, run terminal olunca `job_step_runs` timeline'ini basiyor ve `Completed` veya zombi yolu `Failed` + compensation durumuna gore exit code veriyor. `scripts/hpa-load.sh` eklendi; chaos job'ini varsayilan 30 kez hizli aralikla tetikleyip HPA watcher'a uygun yuk uretiyor. `k8s/README-k8s.md` icine Failover Demo, Watching HPA Scale ve Windows Bash/LF notlari eklendi.
- **Dokunulan dosyalar:** yeni: `scripts/chaos-pod-kill.sh`, `scripts/hpa-load.sh`, `.ai/sessions/2026-06-12-gorev-4.3.md` | degisen: `k8s/README-k8s.md`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** `dotnet build .\flowforge.sln -warnaserror` ✅ — 0 uyari, 0 hata; `dotnet test .\flowforge.sln --no-build` ✅ — 7 unit + 3 integration, toplam 10 test; `git diff --check` ✅; scriptler LF (`CRLF=0`) ✅; Git Bash `bash -n` parse kontrolu ✅; shellcheck-vari manuel goz kontrolu ✅. Canli k3d demo insan tarafindan kosulacak.
- **Not/risk:** Failover script'i zamanlamayi sadece uykuya birakmiyor; 8 sn bekledikten sonra step 2 `Running` satirini poll ediyor. Zombi yolu bugunku tasarim geregi yaklasik 60 sn heartbeat stale esigini bekleyebilir.

---

## 2026-06-12 — Görev 4.3 takip düzeltmesi: Failover poll timeout
- **Yapılan:** `scripts/chaos-pod-kill.sh` poll timeout varsayilani 150 sn'den 180 sn'ye cikarildi. Run status poll satiri UTC zaman damgasi basacak hale getirildi; GIF/demo akisi icin her durum satiri zamanla okunabilir.
- **Dokunulan dosyalar:** yeni: `.ai/sessions/2026-06-12-gorev-4.3-timeout-duzeltme.md` | degisen: `scripts/chaos-pod-kill.sh`, `.ai/PROGRESS.md`
- **Doğrulama:** Git Bash `bash -n scripts/chaos-pod-kill.sh` ✅; `git diff --check` ✅; script LF (`CRLF=0`) ✅.
- **Not/risk:** Canli pod kill dogrulamasi Faz 4 kapanisinda yapilacak.

---

## 2026-06-12 — Görev 5.1: GitHub Actions CI
- **Yapılan:** `.github/workflows/ci.yml` eklendi; push/PR eventlerinde `build-and-unit`, nightly schedule ve `workflow_dispatch` eventlerinde `integration` job'i kosacak. Her iki job .NET 9 setup ve NuGet cache kullanir; integration job 20 dk timeout ile Testcontainers'i GitHub hosted runner Docker'ina birakir. README'ye `build-and-unit` badge'i eklendi.
- **Dokunulan dosyalar:** yeni: `.github/workflows/ci.yml`, `.ai/sessions/2026-06-12-gorev-5.1.md` | degisen: `README.md`, `.ai/BACKLOG.md`, `.ai/PROGRESS.md`
- **Doğrulama:** YAML lint ✅ — PyYAML parse, whitespace kontrolu ve `git diff --check`; local `dotnet build .\flowforge.sln -warnaserror` ✅; local `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 7 test; push sonrasi GitHub Actions `build-and-unit` run `27410773714` ✅ — `success`.
- **Not/risk:** `workflow_dispatch` endpoint'i mevcut, ancak bu ortamda `gh` yok ve GitHub API dispatch auth gerektirdigi icin integration run ajan tarafindan tetiklenemedi (`401 Requires authentication`). 5.1 bu yuzden `[~]` birakildi; kullanici Actions UI'dan `Run workflow` ile integration'i tetikleyince sonuc kayda alinacak. CI integration Kafka startup timeout'u olursa fixture beklemeleri ortam degiskeniyle esnetilecek; ilk committe koda sabit yeni bekleme suresi eklenmedi.

---

## 2026-06-12 — Görev 5.1 düzeltmesi: Integration workflow ayrımı
- **Yapılan:** Integration job'i `ci.yml` icinden ayrilip `.github/workflows/integration.yml` dosyasina tasindi. `ci.yml` artik yalniz push/PR tetiklemeli `build-and-unit` workflow'u; `integration.yml` ise nightly cron ve `workflow_dispatch` ile ayri GitHub Actions menusu ve `Run workflow` butonu uretir.
- **Dokunulan dosyalar:** yeni: `.github/workflows/integration.yml` | degisen: `.github/workflows/ci.yml`, `.ai/PROGRESS.md`, `.ai/sessions/2026-06-12-gorev-5.1.md`
- **Doğrulama:** YAML lint ✅ — PyYAML parse, whitespace kontrolu ve `git diff --check`; local `dotnet build .\flowforge.sln -warnaserror` ✅; local `dotnet test .\tests\FlowForge.UnitTests\FlowForge.UnitTests.csproj --no-build` ✅ — 7 test; push sonrasi GitHub workflow listesi ✅ — `build-and-unit` ve `integration` ayri aktif workflow; son `build-and-unit` run `27412473136` ✅ — `success`.
- **Not/risk:** Integration manual run sonucu hala kullanici tarafindan Actions UI'da tetiklenip raporlanacak.

---
