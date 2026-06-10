# FlowForge — GitHub Kurulum Rehberi

## 1. Dosyaları yerleştir

```
flowforge/
├── .gitignore          ← gitignore.txt'yi bu ADLA kaydet (başında nokta!)
├── .gitattributes      ← gitattributes.txt'yi bu adla kaydet
├── README.md           ← iskelet hazır; Faz 4-5'te GIF + badge eklenecek
├── LICENSE             ← MIT (GitHub'da repo oluştururken "Add license: MIT" seç, en kolayı)
├── .ai/                ← takip katmanı (BACKLOG, PROGRESS, DECISIONS, sessions/)
├── docs/               ← tasarım dokümanı buraya: docs/architecture.md
│   └── adr/
├── src/ tests/ infra/ k8s/ scripts/   ← Görev 1.1'de oluşacak
```

Tasarım dokümanını `docs/architecture.md` olarak, uygulama talimatını `docs/implementation-guide.md` olarak koy — ikisi de repo'da kalsın, README zaten ilkine link veriyor.

## 2. Repo'yu başlat (ilk ve tek seferlik)

```bash
cd flowforge
git init -b main
git add .
git commit -m "chore: project scaffolding — docs, AI protocol, git config"

# GitHub'da boş repo aç (README'siz, .gitignore'suz — hepsi lokalde hazır)
git remote add origin https://github.com/MelihOzaslan01/flowforge.git
git push -u origin main
```

> Repo adı önerisi: `flowforge`. Açıklama: "Distributed job orchestration in .NET 9 — Kafka, Outbox, Sagas, K8s. A redesign of my production SQL-queue orchestrator for distributed scale."
> Topics: `dotnet` `kafka` `outbox-pattern` `saga` `kubernetes` `distributed-systems` `elasticsearch` `testcontainers`

## 3. Commit düzeni (ajana da geçerli kural)

**Conventional Commits** kullan — CI ve okunabilirlik için:

```
feat(worker): implement idempotent consumer loop (task 1.6)
feat(controlplane): job API + seed (task 1.4)
fix(outbox): mark attempt_count on publish failure
test(integration): duplicate message processed exactly once (task 2.7)
docs(adr): ADR-003 sql-queue vs kafka
chore(ai): progress log for task 1.5
```

Kural: **her görev = en az bir commit, mesajında görev numarası geçer.** Uygulama talimatı §0/3 zaten görev-başına-commit diyor; format da bu olsun.

## 4. Branch stratejisi — basit tut

Tek geliştirici + ajan için: **doğrudan `main`'e commit, faz sonlarında tag.**

```bash
git tag -a v0.1 -m "Phase 1: core pipeline — outbox, worker, smoke green"
git push origin v0.1
```

PR/feature-branch tiyatrosu kurma — tek kişilik repo'da bu samimiyetsiz görünür ve seni yavaşlatır. İstisna: Faz 4 gibi riskli büyük işte `phase-4-k8s` branch'i açıp bitince merge edebilirsin.

## 5. Push ÖNCESİ kontrol listesi (her seferinde)

```bash
git status                          # beklenmedik dosya var mı?
git diff --cached --stat            # commit'e ne giriyor?
grep -rn "Password\|ApiKey\|Secret" --include="*.json" src/ | grep -v example
```

- [ ] appsettings.Development.json / .env push'lanmıyor (gitignore'da ama göz at)
- [ ] `.ai/sessions/` dosyalarında şirkete dair isim/veri yok (Marubeni, Komatsu, müşteri adı vs. — kıyas yalnızca "previous production system" genelliğinde)
- [ ] Migration dosyaları dahil mi? (Evet, migrations REPO'YA GİRER)

## 6. Görünürlük ayarları

- Repo: **Public** (amaç portföy)
- Settings → General: Issues açık, Wiki kapalı, Projects kapalı (BACKLOG.md zaten var)
- Profil README'ne ve LinkedIn "Featured" bölümüne Faz 2 bittiğinde ekle — Faz 1'de değil; compensation demo'su olmadan proje hikâyesi yarım görünür.

## 7. Ajan için ek not (talimata §0/9 olarak eklenebilir)

> "Commit mesajları Conventional Commits formatında ve görev numarası içerir.
> `git push` ASLA ajan tarafından yapılmaz — push kararı her zaman insandadır."

Bu son kural önemli: ajan lokalde commit'ler, sen incelersin, push senindir.
