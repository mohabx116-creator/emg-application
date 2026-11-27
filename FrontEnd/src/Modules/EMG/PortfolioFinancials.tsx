import { Typography } from '@mui/material';
import type { EmgFinalAccount, EmgProject } from 'api/emg';
import FinancialSummary from './FinancialSummary';

export function portfolioFunding(projects: EmgProject[]) {
  const complete = projects.filter(project => !project.historicalFundingUnknown && project.totalPayments != null).length;
  const known = projects.flatMap(project => project.totalPayments == null ? [] : [project.totalPayments]);
  const partial = complete < projects.length;
  return {
    partial,
    amount: known.length || !projects.length ? known.reduce((sum, amount) => sum + amount, 0) : null,
    caption: partial
      ? `الدفعات المثبتة فقط بالجنيه المصري · التمويل مكتمل في ${complete} من ${projects.length} مشروع`
      : 'إجمالي الدفعات المثبتة بالجنيه المصري'
  };
}

export function ForeignCurrencySummaries({ accounts }: { accounts: EmgFinalAccount['currencies'] }) {
  return <>{accounts?.filter(row => row.currency !== 'EGP').map(row => (
    <div key={row.currency}>
      <FinancialSummary layout="compact" currency={row.currency} totalWorks={row.works} totalPayments={row.payments} cashBalance={row.cashBalance} />
      {row.referenceTransfers !== 0 && <Typography variant="caption" color="text.secondary">
        تفاصيل تحويلات مرجعية · {row.referenceTransfers.toLocaleString('en-US')} {row.currency}
      </Typography>}
    </div>
  ))}</>;
}
