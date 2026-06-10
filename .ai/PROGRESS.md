# FlowForge — İlerleme Günlüğü (append-only)

> Kural: Ajan her görev sonunda AŞAĞIYA yeni bir blok EKLER. Eski blokları asla düzenlemez/silmez.
> Format birebir korunur — bu dosya insan tarafından commit incelemesinde harita olarak kullanılır.

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
