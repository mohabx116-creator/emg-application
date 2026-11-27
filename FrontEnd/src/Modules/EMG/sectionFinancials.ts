import { EmgFinalAccount } from 'api/emg';
import { resolveCashBalanceStatus } from './FinancialSummary';

export type WorkSectionKey = 'movements' | 'excavation' | 'concrete' | 'steel' | 'finishing';

export type SectionFinancialSummary = {
  sectionKey: WorkSectionKey;
  totalWorks: number | null;
  totalPayments: number | null;
  cashBalance: number | null;
  cashBalanceStatus: string;
  unresolved: boolean;
  source: 'backend-section' | 'final-account-categories' | 'unavailable';
};

const SECTION_CATEGORY_KEYS: Record<WorkSectionKey, string[]> = {
  movements: ['الموقع', 'أعمال الموقع'],
  excavation: ['العرب', 'الحفر', 'أعمال الحفر'],
  concrete: ['المحطة', 'المقاول', 'الخرسانات'],
  steel: ['الحديد'],
  finishing: ['المباني', 'المحارة', 'العزل', 'الكهرباء', 'السباكة', 'التشطيبات', 'باقي المصنعيات', 'غير محدد']
};

export function extractSectionFinancialSummary(
  account: Pick<EmgFinalAccount, 'sections'> | null | undefined,
  sectionKey: WorkSectionKey
): SectionFinancialSummary {
  if (!account?.sections) {
    return {
      sectionKey,
      totalWorks: null,
      totalPayments: null,
      cashBalance: null,
      cashBalanceStatus: 'قيد المطابقة',
      unresolved: true,
      source: 'unavailable'
    };
  }

  const keys = SECTION_CATEGORY_KEYS[sectionKey];
  const rows = account.sections.filter(row => row.currency === 'EGP' && (row.sectionKey ? row.sectionKey === sectionKey : keys.includes(row.category)));
  if (!rows.length) return extractSectionFinancialSummary(null, sectionKey);
  const totalWorks = rows.reduce((sum, row) => sum + row.works, 0);
  const totalPayments = rows.reduce((sum, row) => sum + row.allocatedFunding, 0);
  const cashBalance = totalPayments - totalWorks;

  return {
    sectionKey,
    totalWorks,
    totalPayments: rows.some(row => row.fundingUnknown) ? null : totalPayments,
    cashBalance: rows.some(row => row.fundingUnknown) ? null : cashBalance,
    cashBalanceStatus: resolveCashBalanceStatus(cashBalance),
    unresolved: false,
    source: 'backend-section'
  };
}
