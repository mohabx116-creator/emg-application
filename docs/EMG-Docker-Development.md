# EMG Docker Development

## Local PostgreSQL

من جذر المشروع:

```powershell
Copy-Item .env.example .env
docker compose up -d postgres
```

البيانات محفوظة في Volume باسم `emg_postgres_data`.

## API على الجهاز وDatabase داخل Docker

شغّل الـAPI مع متغير الاتصال الموجود في `.env`، أو ضعه في إعدادات بيئة التشغيل باسم:

```text
ConnectionStrings__EmgDb
```

عند ترك Connection String فارغًا، يستخدم النظام Pilot fallback ولا يحاول الاتصال بقاعدة البيانات.

## ملاحظات

- لا نستخدم `docker compose down -v` إلا عند الرغبة في حذف بيانات PostgreSQL نهائيًا.
- Production سيستخدم Secret آمن وكلمة مرور مختلفة وVolume/Backup مناسبين.
- Redis سيُضاف لاحقًا عندما نحتاج الكاش والتنبيهات والـBackground Jobs.
