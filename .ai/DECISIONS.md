# FlowForge — Talimat Dışı Kararlar

> Kural: Ajan, uygulama talimatında belirtilmeyen bir karar vermek veya talimattan
> sapmak ZORUNDA kalırsa, kodu yazmadan ÖNCE buraya kayıt düşer.
> Buraya yazılmamış her sapma kural ihlalidir.
> İnsan (Melih) her kaydı inceler ve Durum alanını günceller.

---
<!-- ŞABLON:

## D-001 — [TARİH] — <kısa başlık>
- **Bağlam:** Hangi görevde, hangi belirsizlik/engel çıktı?
- **Karar:** Ne yapıldı?
- **Alternatifler:** Neden diğer yollar seçilmedi? (1-2 cümle)
- **Etki:** Hangi dosyalar/şemalar etkilendi? Tasarım dokümanıyla çelişiyor mu?
- **Durum:** ⏳ İnceleme bekliyor / ✅ Onaylandı / ❌ Geri alınacak

-->
---

## D-001 — 2026-06-10 — Outbox batch fail-fast ordering
- **Bağlam:** Görev 1.5 OutboxPublisher davranışı incelenirken batch ortasında bir publish hatası olduğunda sonraki kayıtların publish edilmeye devam ettiği görüldü. Bu, aynı `aggregate_id`/run içindeki event ordering varsayımını bozabilir.
- **Karar:** Batch döngüsü ilk publish başarısızlığında fail-fast davranacak ve `break` ile duracak. Değişiklikler batch sonunda tek `SaveChangesAsync` çağrısıyla kaydedilecek; başarısız kayıt silinmeyecek, `attempt_count++` ile sonraki 500ms turunda yeniden denenecek.
- **Alternatifler:** Aggregate bazlı atlama düşünüldü; fakat aynı batch içinde aggregate gruplama/atlama karmaşıklığı Faz 1 kapsamı için değmez. Basit fail-fast davranış ordering varsayımını daha açık korur.
- **Etki:** `src/FlowForge.Outbox/OutboxPublisher.cs` davranışı değişir. Tasarım §5.4 ile uyumludur; hatta `SaveChangesAsync` çağrısını batch sonuna taşıyarak örnek kalıba daha çok yaklaşır.
- **Durum:** ⏳ İnceleme bekliyor

## D-002 — 2026-06-10 — StepCompleted carries step definitions
- **Bağlam:** Görev 1.6 Worker, sonraki step'i `StepCompleted` eventinden çalıştıracak ve worker stateless kalacak. Worker_db şeması yalnızca `job_step_runs`, `outbox_messages` ve `processed_messages` içerdiği için worker'ın sonraki step tanımına erişmesi event payload'ına bağlı.
- **Karar:** `StepCompleted` payload'ı `runId`, `stepNo`, `output` alanlarına ek olarak `steps[]` taşıyacak. Böylece `JobRunRequested` ile gelen step listesi saga zinciri boyunca korunur.
- **Alternatifler:** Worker_db'ye job definition tablosu eklemek reddedildi; tasarım §6.2 worker_db şemasını genişletirdi. ControlPlane'den job tanımı çekmek reddedildi; worker'ı ControlPlane'e runtime bağımlı yapardı.
- **Etki:** `src/FlowForge.Contracts/StepCompleted.cs`, ilgili unit test ve Worker event üretimi etkilenir. Event adı değişmez; yalnızca payload genişler.
- **Durum:** ⏳ İnceleme bekliyor

## D-003 — 2026-06-11 — Outbox topic column for DLQ publishing
- **Bağlam:** Görev 2.3 retry tükendiğinde orijinal mesajın DLQ topic'ine kayıpsız aktarılması ve `StepFailed` eventinin aynı güvenceyle yayınlanması gerekiyor. Mevcut outbox yalnızca varsayılan `flowforge.job.events` topic'ine publish ediyordu.
- **Karar:** `outbox_messages` tablosuna nullable `topic` kolonu eklendi. `null` değeri varsayılan `flowforge.job.events` anlamına gelir; DLQ kopyası `topic=flowforge.job.events.dlq` ve `aggregate_id=runId` ile, `StepFailed` ise varsayılan topic ile aynı transaction'da outbox'a yazılır. OutboxPublisher fail-fast davranışı topic'ten bağımsız korunur.
- **Alternatifler:** DLQ'ya worker içinden direkt Kafka produce etmek reddedildi; produce ve DB transaction arasında crash olursa DLQ kaydı ya da `StepFailed` kaydı kaybolabilir/ayrışabilirdi.
- **Etki:** İki DB migration'ı, shared outbox entity/publisher ve Worker retry-exhausted yolu etkilenir. Varsayılan topic davranışı `topic IS NULL` ile geriye dönük uyumludur.
- **Durum:** ⏳ İnceleme bekliyor

## D-004 — 2026-06-11 — Compensation retry is out of scope
- **Bağlam:** Görev 2.4 compensation zincirinde `CompensateStep` işlemi başarısız olabilir. Faz 2 hedefi ters sıra saga akışını kurmak; compensation retry politikası ayrıca tasarlanmadı.
- **Karar:** Compensation adımlarında retry yapılmayacak. `StepExecutor.CompensateAsync` hata verirse WARN loglanır, `job_step_runs` satırına `Compensated` status'ü ve hata metni yazılır, zincir `StepCompensated` ile devam eder.
- **Alternatifler:** Compensation için Polly retry eklemek reddedildi; retry sayısı/backoff/idempotency semantiği Faz 2 kapsamı dışı ve ana retry davranışını karmaşıklaştırırdı. Zinciri durdurmak reddedildi; başarısız iş akışının terminal `JobRunFailed` projeksiyonuna ulaşmasını engellerdi.
- **Etki:** `src/FlowForge.Worker/Steps/StepExecutor.cs` ve `src/FlowForge.Worker/Kafka/JobEventsConsumer.cs` davranışı etkilenir. Bu karar compensation güvenilirliğini değil, saga kapanışını önceliklendirir.
- **Durum:** ⏳ İnceleme bekliyor

## D-005 — 2026-06-11 — Chaos seed uses no retry on failing step
- **Bağlam:** Görev 2.5 canlı doğrulamada `monthly-sales-report-chaos` job'ının 5+ tetiklemede en az bir `Failed` run üretmesi bekleniyor. `chaos_fail_rate=0.3` ve varsayılan `maxRetries=3` birlikte kalırsa bir run'ın gerçekten tükenme olasılığı yaklaşık `0.3^4`, yani yüzde 0.8 olur.
- **Karar:** Chaos seed job'ında yalnız `GenerateReport` adımı `chaos_fail_rate=0.3`, `duration_ms=2000` ve `maxRetries=0` ile oluşturuldu. Böylece chaos exception retry mekanizmasına normal hata gibi girer, ama canlı smoke kısa sürede DLQ/compensation yolunu görebilir. Mevcut `monthly-sales-report` job'ına dokunulmadı.
- **Alternatifler:** Varsayılan `maxRetries=3` ile bırakmak reddedildi; 5+ run şartında Failed gözlemek pratik olarak güvenilmezdi. `chaos_fail_rate` değerini yükseltmek reddedildi; görev tanımı açıkça `0.3` istedi.
- **Etki:** `src/FlowForge.ControlPlane/Data/ControlPlaneSeeder.cs` seed verisi etkilenir. Runtime sözleşme değişmez; yalnız demo/test job'ının retry sayısı farklıdır.
- **Durum:** ⏳ İnceleme bekliyor

## D-006 — 2026-06-11 — Running step rows carry recovery context
- **Bağlam:** Görev 2.6 zombi temizleyici `Running + heartbeat > 60 sn eski` adımları `Failed` yapıp `StepFailed` yayınlamalı. `StepFailed` payload'ı D-002/D-004 çizgisinde `steps[]` taşımak zorunda; ayrıca orijinal Kafka mesajı yeniden teslim edilirse aynı adım yeniden çalışmamalı.
- **Karar:** `job_step_runs` tablosuna nullable `steps jsonb` ve `source_message_id uuid` kolonları eklendi. Worker adımı çalıştırmadan önce `Running` satırı yazar; bu satır step listesi ve kaynak envelope `messageId` değerini taşır. Zombi temizleyici aynı transaction'da satırı `Failed` yapar, `StepFailed` outbox'a ekler ve `source_message_id` için `processed_messages` kaydı yazar.
- **Alternatifler:** Step listesini temizleyicide boş bırakmak reddedildi; compensation zinciri gerçek step tanımlarını kaybederdi. Orijinal mesaj redelivery'sini Kafka offset'e bırakmak reddedildi; crash sonrası aynı iş hem zombi `StepFailed` hem yeniden çalışma yoluna girebilirdi.
- **Etki:** Worker DB migration'ları, `JobStepRun`, `JobEventsConsumer`, `StepHeartbeat` ve `ZombieStepCleaner` etkilenir. ControlPlane şeması değişmez.
- **Durum:** ⏳ İnceleme bekliyor
