// Shared EMG formatting, local Cairo date, and canonical category definitions

const arCurrencyFormatter = new Intl.NumberFormat('ar-EG', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

const arCompactFormatter = new Intl.NumberFormat('ar-EG', {
  maximumFractionDigits: 2
});

export function formatEmgMoney(value: number | null | undefined, suffix = 'ج.م'): string {
  const safe = typeof value === 'number' && Number.isFinite(value) ? value : 0;
  return suffix ? `${arCurrencyFormatter.format(safe)} ${suffix}` : arCurrencyFormatter.format(safe);
}

export function formatEmgNumber(value: number | null | undefined): string {
  const safe = typeof value === 'number' && Number.isFinite(value) ? value : 0;
  return arCompactFormatter.format(safe);
}

/**
 * Returns local calendar date in YYYY-MM-DD format (avoids UTC off-by-one bug after midnight Cairo time).
 */
export function getLocalIsoDate(date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export type FinishingCategoryOption = {
  label: string;
  canonicalAccountCategory: string;
  group: string;
};

/**
 * Approved Finishing UI categories bound to backend canonical classification buckets.
 */
export const APPROVED_FINISHING_CATEGORIES: readonly FinishingCategoryOption[] = [
  { label: 'أعمال ومشتريات الموقع', canonicalAccountCategory: 'أعمال ومشتريات الموقع', group: 'عام وموقع' },
  { label: 'أعمال المباني', canonicalAccountCategory: 'المباني', group: 'أعمال إنشائية ومعمارية' },
  { label: 'أعمال تأسيس السباكة', canonicalAccountCategory: 'السباكة', group: 'تأسيسات' },
  { label: 'أعمال تأسيس الكهرباء', canonicalAccountCategory: 'الكهرباء', group: 'تأسيسات' },
  { label: 'أعمال العزل', canonicalAccountCategory: 'العزل', group: 'أعمال إنشائية ومعمارية' },
  { label: 'أعمال الجبسوم بورد', canonicalAccountCategory: 'الجبسوم بورد', group: 'تشطيبات' },
  { label: 'أعمال المحارة', canonicalAccountCategory: 'المحارة', group: 'أعمال إنشائية ومعمارية' },
  { label: 'أعمال السيراميك والبورسلين والأرضيات', canonicalAccountCategory: 'السيراميك والأرضيات', group: 'تشطيبات' },
  { label: 'أعمال النجارة', canonicalAccountCategory: 'النجارة', group: 'تشطيبات' },
  { label: 'أعمال الألوميتال والـ UPVC', canonicalAccountCategory: 'الألوميتال', group: 'تشطيبات' },
  { label: 'أعمال النقاشة', canonicalAccountCategory: 'النقاشة', group: 'تشطيبات' },
  { label: 'أعمال فنش السباكة', canonicalAccountCategory: 'السباكة', group: 'تشطيبات نهائية' },
  { label: 'أعمال فنش الكهرباء', canonicalAccountCategory: 'الكهرباء', group: 'تشطيبات نهائية' }
] as const;

export function getCanonicalFinishingCategory(label: string): string {
  const match = APPROVED_FINISHING_CATEGORIES.find((item) => item.label === label);
  return match ? match.canonicalAccountCategory : label;
}
