import { useEffect, useState } from 'react';
import { Alert, LinearProgress, Typography } from '@mui/material';
import { getHistoricalAccount } from 'api/historicalWork';
import HistoricalWorkTable from './HistoricalWorkTable';

type Props = {
  type: 'suppliers' | 'contractors';
  id: number;
  projectId?: number;
  kind: 'works' | 'settlements';
  emptyText?: string;
};

export default function HistoricalAccountDetails({ type, id, projectId, kind, emptyText }: Props) {
  type Account = Awaited<ReturnType<typeof getHistoricalAccount>>;
  const [result, setResult] = useState<{ key: string; account?: Account; failed?: boolean }>();
  const key = `${type}/${id}`;
  useEffect(() => {
    const controller = new AbortController();
    getHistoricalAccount(type, id, controller.signal)
      .then(account => { if (!controller.signal.aborted) setResult({ key, account }); })
      .catch(() => { if (!controller.signal.aborted) setResult({ key, failed: true }); });
    return () => controller.abort();
  }, [type, id, key]);
  if (result?.key !== key) return <LinearProgress aria-label="تحميل تفاصيل الحساب التاريخي" />;
  if (result.failed || !result.account) return <Alert severity="error">تعذر تحميل تفاصيل الحساب التاريخي.</Alert>;
  const rows = result.account[kind].filter(row => !projectId || row.projectId === projectId);
  if (!rows.length) return emptyText ? <Typography color="text.secondary">{emptyText}</Typography> : null;
  return <HistoricalWorkTable rows={rows} />;
}