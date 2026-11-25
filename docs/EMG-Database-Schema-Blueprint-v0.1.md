# EMG Database Schema Blueprint v0.1

هذه خريطة مبدئية للجداول والعلاقات، وليست SQL نهائية.

## 1. الشركة والحسابات

### companies

- id
- name
- status
- created_at

### users

- id
- username ثابت
- display_name قابل للتعديل
- email أو وسيلة الاسترداد
- password_hash
- avatar_file_id
- status
- last_login_at

### roles / permissions / user_roles

تدعم أكثر من دور للمستخدم، مع صلاحيات للإجراءات والنطاق.

### user_project_assignments

- user_id
- project_id
- role داخل المشروع
- is_primary
- starts_at
- ends_at
- status

## 2. المشاريع

### projects

- id
- name الأصلي
- code أو رقم القطعة
- client_party_id
- location
- starts_at
- status
- office_share_rate
- source_file_reference

### project_sections

تحتفظ بمسميات الأقسام الحالية مثل خاص بأعمال الموقع وأعمال الحفر وأعمال الخرسانات.

### project_settings

إعدادات المشروع مثل النسب وقواعد الاعتماد والوحدات المسموحة.

## 3. الأطراف والموردون

### parties

- id
- display_name
- original_name
- phone
- address
- tax_or_identity_reference
- notes
- status

### party_types

مثل مورد، مقاول، توريد عمالة، صاحب معدة، محطة خرسانة، ناقل، مقدم خدمة، عميل أو مالك.

### party_type_links

لأن الطرف الواحد يمكن أن يكون له أكثر من نوع.

### party_aliases

لربط الأسماء المتشابهة بعد مراجعة، دون تغيير الاسم الأصلي للحركة.

## 4. الحركات

### movements

- id
- project_id
- project_section_id
- movement_type
- original_section_name
- original_description
- party_id
- movement_date
- status
- created_by
- approved_by
- notes
- source_file_reference
- source_sheet_reference
- source_row_reference

### movement_lines

- movement_id
- original_item_number
- unit_original
- unit_normalized
- previous_quantity
- period_quantity
- total_quantity
- unit_price
- total_amount
- calculation_method

### movement_details

تفاصيل متخصصة عند الحاجة، مثل أبعاد أعمال الحفر أو مقاس الحديد أو محطة الخرسانة.

## 5. الدفعات والمستحقات

### payment_obligations

المبلغ المستحق على المشروع للطرف بعد اعتماد التوريد أو العمل.

### payments

- payer_party_id عند وجوده
- receiver_party_id
- project_id
- payment_date
- amount
- currency
- payment_method
- source_type
- reference_number
- status
- notes

### payment_allocations

تربط الدفع بالحركة أو المستحق أو المشروع أو البند.

### adjustments

للخصومات والتسويات والحركات العكسية، مع السبب والمستخدم.

## 6. دفعات تحت الحساب

يمكن تمثيلها كحركة مالية، مع جداول توزيع عند الحاجة:

### advance_transactions

تحتوي على بيانات الدفعة الأصلية ومصدرها ومستلمها والمرفق.

### advance_allocations

توزع الدفعة على مشروع أو قسم أو مورد أو المكتب، وتمنع تجاوز إجمالي الدفعة.

## 7. كشف حساب نهائي

### final_accounts

- project_id
- status
- as_of_date
- office_share_rate
- total_issued
- total_received
- office_payments
- office_net_due
- approved_by
- closed_at

### final_account_lines

تربط كل رقم في كشف الحساب بالقسم والحركة أو المصدر الذي كوّنه.

## 8. المرفقات

### files

- storage_key
- original_name
- mime_type
- size
- uploaded_by
- uploaded_at

### file_links

تربط الملف بالمشروع أو الحركة أو المورد أو كشف الحساب.

## 9. الحضور والمراجعة اليومية

### attendance_records

تسجل المستخدم والمشروع والتاريخ ووقت الحضور والانصراف والملاحظات.

### daily_site_reviews

لـمراجعة يومية الموقع.

### daily_site_review_items

تربط المراجعة بالحركات أو الصور أو المستجدات.

## 10. التنبيهات والتدقيق

### notifications

تربط الإشعار بالمستخدم والمشروع والحركة، وتحتوي النوع والأولوية وحالة القراءة.

### audit_logs

تسجل الإنشاء والتعديل والإرسال والمراجعة والاعتماد والرفض والتسويات وتغييرات الصلاحيات.

## 11. القيود الأساسية

- لا توجد حركة معتمدة بلا مشروع وقسم وتاريخ ومستخدم.
- لا توجد دفعة موزعة بأكثر من قيمتها الأصلية.
- لا يوجد دفع معتمد بلا مصدر دفع أو سبب واضح.
- لا تحذف حركة معتمدة حذفًا نهائيًا.
- كل كشف نهائي قابل للتتبع إلى حركاته.
- كل مبلغ مالي يستخدم numeric دقيق.
- الاسم الأصلي والوصف الأصلي لا يتم استبدالهما أثناء التطبيع.
