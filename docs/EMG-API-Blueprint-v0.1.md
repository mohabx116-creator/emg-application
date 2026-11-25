# EMG API Blueprint v0.1

هذه خريطة مبدئية للـAPI وليست تنفيذًا نهائيًا.

## 1. Authentication

```text
POST /api/auth/login
POST /api/auth/logout
POST /api/auth/refresh
POST /api/auth/forgot-password
POST /api/auth/reset-password
GET  /api/auth/me
```

## 2. Profile

```text
GET   /api/profile
PATCH /api/profile
POST  /api/profile/avatar
POST  /api/profile/change-password
```

اسم المستخدم لا يتغير، بينما الاسم الظاهر والصورة وكلمة المرور قابلة للتعديل حسب الصلاحية.

## 3. Users, roles and assignments

```text
GET  /api/users
POST /api/users
GET  /api/users/{id}
PATCH /api/users/{id}
GET  /api/roles
POST /api/users/{id}/roles
DELETE /api/users/{id}/roles/{roleId}
GET  /api/users/{id}/projects
POST /api/projects/{projectId}/assignments
PATCH /api/assignments/{id}
```

## 4. Projects

```text
GET  /api/projects
POST /api/projects
GET  /api/projects/{id}
PATCH /api/projects/{id}
POST /api/projects/{id}/activate
POST /api/projects/{id}/close
GET  /api/projects/{id}/sections
```

كل نتيجة تتأثر بنطاق المستخدم وصلاحياته.

## 5. الموردون والأطراف

```text
GET  /api/parties
POST /api/parties
GET  /api/parties/{id}
PATCH /api/parties/{id}
GET  /api/parties/{id}/movements
GET  /api/parties/{id}/statement
GET  /api/parties/{id}/projects
```

يدعم المورد أكثر من نوع توريد، والحساب يمكن تصفيته بالمشروع والقسم والفترة.

## 6. الحركات

```text
GET  /api/movements
POST /api/movements
GET  /api/movements/{id}
PATCH /api/movements/{id}
POST /api/movements/{id}/submit
POST /api/movements/{id}/approve
POST /api/movements/{id}/reject
POST /api/movements/{id}/request-changes
POST /api/movements/{id}/adjust
```

تدعم الفلاتر المشروع والقسم والمورد ونوع الحركة والحالة والفترة.

## 7. الدفعات والتسويات

```text
GET  /api/payments
POST /api/payments
GET  /api/payments/{id}
PATCH /api/payments/{id}
POST /api/payments/{id}/allocate
POST /api/payments/{id}/approve
GET  /api/projects/{projectId}/payments
GET  /api/projects/{projectId}/advances
POST /api/projects/{projectId}/advances
POST /api/advances/{id}/allocate
```

يمنع الـBackend توزيع مبلغ أكبر من قيمة الدفعة، ويحسب غير الموزع والمتبقي.

## 8. الأقسام والتفاصيل المتخصصة

```text
GET  /api/projects/{id}/site-work
GET  /api/projects/{id}/excavation
GET  /api/projects/{id}/concrete
GET  /api/projects/{id}/iron
GET  /api/projects/{id}/building
GET  /api/projects/{id}/plaster
GET  /api/projects/{id}/insulation
GET  /api/projects/{id}/electrical
GET  /api/projects/{id}/plumbing
```

يمكن أن تستخدم هذه المسارات نفس مورد الحركات داخليًا، مع نماذج تفاصيل مختلفة لكل قسم.

## 9. التقارير وكشف الحساب النهائي

```text
GET /api/projects/{id}/final-account
POST /api/projects/{id}/final-account/prepare
POST /api/projects/{id}/final-account/approve
GET /api/reports/project-cost
GET /api/reports/party-statement
GET /api/reports/advance-payments
GET /api/reports/daily-site-review
POST /api/reports/export
```

التقارير تقرأ من الحركات المعتمدة وتعيد مصدر كل رقم.

## 10. البحث والإشعارات

```text
GET /api/search?q=&projectId=&section=&status=&from=&to=
GET /api/notifications
POST /api/notifications/{id}/read
POST /api/notifications/read-all
PATCH /api/notification-preferences
```

## 11. المرفقات والحضور

```text
POST /api/files/presign
POST /api/files/complete
POST /api/files/{id}/link
GET  /api/files/{id}
POST /api/attendance
GET  /api/projects/{id}/attendance
POST /api/daily-site-reviews
GET  /api/projects/{id}/daily-site-reviews
```

## 12. قواعد API العامة

- Pagination في القوائم.
- Sorting وFiltering من الـBackend.
- استجابة موحدة للأخطاء بالعربية والإنجليزية عند الحاجة.
- إرجاع رقم الحركة وحالتها بعد الحفظ.
- Idempotency للعمليات المالية الحساسة.
- معاملات قاعدة بيانات عند حفظ الحركة مع التوزيعات أو المرفقات المرتبطة.
- عدم السماح للواجهة بتجاوز الصلاحيات.
