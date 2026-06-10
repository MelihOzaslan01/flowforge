# FlowForge — İlerleme Günlüğü (append-only)

> Kural: Ajan her görev sonunda AŞAĞIYA yeni bir blok EKLER. Eski blokları asla düzenlemez/silmez.
> Format birebir korunur — bu dosya insan tarafından commit incelemesinde harita olarak kullanılır.

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
