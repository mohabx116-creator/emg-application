# EMG Documentation Index

هذه الوثائق هي مرجع مرحلة التحليل والتصميم قبل التنفيذ.

## Business and data

- [EMG-Business-Concept-v0.1.md](./EMG-Business-Concept-v0.1.md) — مفهوم النظام والعمليات.
- [EMG-Logical-Data-Model-v0.1.md](./EMG-Logical-Data-Model-v0.1.md) — خريطة الكيانات والعلاقات.
- [EMG-Form-Specifications-v0.1.md](./EMG-Form-Specifications-v0.1.md) — حقول نماذج الإدخال.
- [EMG-Automation-Rules-v0.1.md](./EMG-Automation-Rules-v0.1.md) — الحسابات والإجراءات التلقائية.
- [EMG-Migration-and-Data-Quality-v0.1.md](./EMG-Migration-and-Data-Quality-v0.1.md) — ترحيل Excel وجودة البيانات.

## Workflow and experience

- [EMG-Permissions-and-Approval-Workflow-v0.1.md](./EMG-Permissions-and-Approval-Workflow-v0.1.md) — الأدوار والصلاحيات والاعتمادات.
- [EMG-Project-Lifecycle-v0.1.md](./EMG-Project-Lifecycle-v0.1.md) — دورة حياة المشروع.
- [EMG-UX-and-Navigation-v0.1.md](./EMG-UX-and-Navigation-v0.1.md) — خريطة الواجهات والتنقل.
- [EMG-MVP-Vertical-Slice-v0.1.md](./EMG-MVP-Vertical-Slice-v0.1.md) — أول دورة تشغيل كاملة للاختبار.
- [EMG-Pilot-and-Validation-Plan-v0.1.md](./EMG-Pilot-and-Validation-Plan-v0.1.md) — خطة الـPilot والتحقق.

## Technical and operations

- [EMG-Technical-Architecture-v0.1.md](./EMG-Technical-Architecture-v0.1.md) — البنية التقنية.
- [EMG-Database-Schema-Blueprint-v0.1.md](./EMG-Database-Schema-Blueprint-v0.1.md) — مخطط قاعدة البيانات.
- [EMG-API-Blueprint-v0.1.md](./EMG-API-Blueprint-v0.1.md) — خريطة الـAPI.
- [EMG-Testing-and-Quality-Plan-v0.1.md](./EMG-Testing-and-Quality-Plan-v0.1.md) — خطة الاختبارات والجودة.
- [EMG-Deployment-and-Operations-v0.1.md](./EMG-Deployment-and-Operations-v0.1.md) — الاستضافة والتشغيل والنسخ الاحتياطي.
- [EMG-Implementation-Roadmap-v0.1.md](./EMG-Implementation-Roadmap-v0.1.md) — ترتيب التنفيذ.

## Decisions

- [EMG-Open-Decisions-v0.1.md](./EMG-Open-Decisions-v0.1.md) — القرارات المعتمدة والنقاط المتبقية.

## Current status

- مرحلة الفهم والتحليل: مكتملة مبدئيًا.
- مفهوم الأعمال: موثق.
- خريطة البيانات: موثقة.
- الصلاحيات والاعتماد: موثقة.
- البنية التقنية: موثقة.
- التنفيذ البرمجي: بدأ بالـVertical Slice الأول.
- Backend: تم تجهيز `backend` كـ.NET Web API مع `/api/health` و`/api/projects` ببيانات Pilot مؤقتة.
- Frontend: تمت إضافة شاشة `المشاريع` إلى Berry داخل `FrontEnd` وربطها بالـBackend.
- هيكل المستودع: تم توحيده على نمط المشروع المرجعي: `BackEnd/src` بطبقات `EMG.Domain` و`EMG.Infrastructure` و`EMG.Shared` و`EMG.WebApi`، و`FrontEnd/src/Modules` للتطوير Feature-Based.
- القرار التالي: استبدال بيانات الـPilot المؤقتة بقاعدة PostgreSQL وإضافة Authentication وتكليف المشاريع.
