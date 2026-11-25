export const REPORT_CATEGORY_LABELS: Record<string, string> = {
  SiteExpense: 'مصروفات الموقع',
  Excavation: 'الحفر',
  ContractorWorkmanship: 'أعمال المقاول',
  ConcreteStation: 'خرسانة المحطة',
  Concrete: 'الخرسانة',
  Steel: 'الحديد',
  Buildings: 'المباني',
  Plaster: 'المحارة',
  Insulation: 'العزل',
  Electrical: 'الكهرباء',
  Plumbing: 'السباكة',
  Finishing: 'التشطيبات',
  OfficePayment: 'مدفوعات المكتب',
  OfficeAccrual: 'استحقاق المكتب',
  OwnerFunding: 'تمويل المالك',
  FundingAllocation: 'توزيعات الدفعات',
  AdvancePayment: 'دفعة تحت الحساب',
  AdvanceAllocation: 'توزيع دفعة',
  SupplierSupply: 'توريدات الموردين',
  SupplierPayment: 'مدفوعات الموردين',
  Unresolved: 'غير محسوم'
};

export const REPORT_DIMENSION_LABELS: Record<string, string> = {
  'operational-cost': 'تكلفة تشغيلية',
  'supplier-supply': 'توريدات الموردين',
  'supplier-payment': 'مدفوعات الموردين',
  'owner-funding': 'تمويل المالك',
  'advance-allocation': 'توزيعات الدفعات',
  'office-payment': 'مدفوعات المكتب',
  unresolved: 'غير محسوم'
};

export const REPORT_ENTITY_LABELS: Record<string, string> = {
  SiteMovement: 'حركة موقع',
  AdvancePayment: 'دفعة تحت الحساب',
  AdvanceAllocation: 'توزيع دفعة',
  SupplierSupply: 'توريد مورد',
  SupplierPayment: 'دفعة مورد',
  ExcavationRecord: 'سجل حفر',
  ConcreteRecord: 'سجل خرسانة',
  SteelRecord: 'سجل حديد',
  FinishingRecord: 'سجل تشطيبات',
  ContractorStatement: 'كشف مقاول',
  HistoricalRecord: 'سجل تاريخي'
};

const HAS_ARABIC = /[\u0600-\u06FF]/;

export function localizeCategory(value?: string | null): string {
  if (!value) return 'غير مصنّف';
  return REPORT_CATEGORY_LABELS[value] || REPORT_DIMENSION_LABELS[value] || value;
}

export function localizeEntityType(value?: string | null): string {
  if (!value) return 'سجل';
  return REPORT_ENTITY_LABELS[value] || REPORT_CATEGORY_LABELS[value] || value;
}

export function localizeCurrency(value?: string | null): string {
  if (!value) return 'ج.م';
  const upper = value.trim().toUpperCase();
  if (upper === 'EGP' || upper === 'LE' || upper === 'L.E') return 'ج.م';
  if (upper === 'USD') return 'دولار';
  if (upper === 'SAR') return 'ريال';
  return value;
}

export function localizeGroupLabel(label?: string | null): string {
  if (!label) return '—';
  let result = label;
  for (const [key, ar] of Object.entries({ ...REPORT_CATEGORY_LABELS, ...REPORT_DIMENSION_LABELS })) {
    if (result.includes(key)) {
      result = result.replaceAll(key, ar);
    }
  }
  return result;
}

export function localizeDiagnosticText(text?: string | null, fallback = 'بيان تشخيصي للمراجعة المحاسبية.'): string {
  if (!text || !text.trim()) return fallback;
  const trimmed = text.trim();
  if (HAS_ARABIC.test(trimmed)) return trimmed;

  const lower = trimmed.toLowerCase();

  if (lower.includes('read-only projection') || lower.includes('importedamount')) {
    return 'عرض للقراءة فقط: القيم الحالية تشمل كافة السجلات المعتمدة والمرحّلة، والأساس المستورد يمثل الجزء التاريخي منها، بينما عمودا H و T تشخيصيان فقط.';
  }
  if (lower.includes('contractual office-fee') || lower.includes('office-fee base')) {
    return 'قيمة تشخيصية مشتقة فقط؛ أساس نسبة المكتب التعاقدية غير معتمد بعد، وتم استبعاد السجلات التشغيلية غير المحسومة من الحساب.';
  }
  if (lower.includes('overlap')) {
    return 'عمود تشخيصي متداخل مع بنود أخرى ولا يُجمع ضمن الإجمالي المعتمد.';
  }
  if (lower.includes('unapproved') && lower.includes('diagnostic')) {
    return 'قيمة تشخيصية غير معتمدة ولا تمثل رصيدًا رسميًا.';
  }
  if (lower.includes('unresolved') || lower.includes('unclassified') || lower.includes('could not classify')) {
    return 'سجل غير محسوم التصنيف ويحتاج مراجعة محاسبية لتحديد البند المناسب.';
  }
  if (lower.includes('operational') && lower.includes('cost')) {
    return 'التكاليف التشغيلية المعتمدة والمرحّلة للمشروع.';
  }
  if (lower.includes('supplier') && lower.includes('suppl')) {
    return 'إجمالي توريدات الموردين المعتمدة للمشروع.';
  }
  if (lower.includes('supplier') && lower.includes('payment')) {
    return 'إجمالي مدفوعات الموردين المعتمدة للمشروع.';
  }
  if (lower.includes('owner') && lower.includes('fund')) {
    return 'إجمالي تمويل ودفعات المالك المستلمة للمشروع.';
  }
  if (lower.includes('advance') || lower.includes('allocation')) {
    return 'توزيعات الدفعات تحت الحساب على بنود ومصروفات المشروع.';
  }
  if (lower.includes('office') && lower.includes('payment')) {
    return 'مدفوعات المكتب المعتمدة والمخصومة على المشروع.';
  }
  if (lower.includes('incomplete') || lower.includes('unverified')) {
    return 'غير مكتمل التحقق المحاسبي ويحتاج مراجعة.';
  }

  return fallback;
}
