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
