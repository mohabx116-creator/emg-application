# EMG Repository Structure v0.1

تم اعتماد هيكل قريب من مشروع V19 المرجعي مع الحفاظ على Berry كمصدر الواجهة:

```text
EMG-application/
├── BackEnd/
│   ├── EMG.slnx
│   └── src/
│       ├── EMG.Domain/
│       ├── EMG.Infrastructure/
│       ├── EMG.Shared/
│       └── EMG.WebApi/
├── FrontEnd/
│   └── src/
│       ├── Modules/EMG/<Feature>/
│       ├── shared/
│       ├── api/
│       ├── routes/
│       └── layout/
├── docs/
├── seed/
└── tabler-icons-main/
```

كل Feature جديدة تُبنى داخل `FrontEnd/src/Modules/EMG`، بينما منطقها الخلفي يُوزع على طبقات `Domain` و`Infrastructure` و`Shared` و`WebApi` حسب المسؤولية.

تم تجهيز `EMG.Infrastructure` بـEF Core وNpgsql، وإضافة `EmgDbContext` وكيان `Project`. الاتصال لا يُفعّل إلا عند وضع `ConnectionStrings:EmgDb`، لذلك تظل نسخة الـPilot قابلة للتشغيل بدون PostgreSQL أثناء التطوير.

الهدف هو الحفاظ على فصل واضح بين الواجهة والـBackend، مع سهولة تشغيل كل جزء مستقلًا، وإمكانية إضافة تطبيق موبايل لاحقًا على نفس الـAPI.
