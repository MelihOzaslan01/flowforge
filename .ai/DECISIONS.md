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
