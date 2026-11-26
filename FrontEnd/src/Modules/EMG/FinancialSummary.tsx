import { Grid, Card, CardContent, Stack, Typography } from '@mui/material';
import { IconCash, IconReceipt, IconTool } from '@tabler/icons-react';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;

export type FinancialSummaryValues = {
  totalWorks: number;
  totalPayments: number;
  cashBalance: number;
  cashBalanceStatus: string;
  unresolved?: boolean;
};

export default function FinancialSummary({ totalWorks, totalPayments, cashBalance, cashBalanceStatus, unresolved = false }: FinancialSummaryValues) {
  const display = (value: number) => unresolved ? 'قيد المطابقة' : formatMoney(value);
  const balanceTone = cashBalance > 0 ? 'success.main' : cashBalance < 0 ? 'error.main' : 'text.primary';
  return (
    <Grid container spacing={1.5} aria-label="الملخص المالي">
      <Grid size={{ xs: 12, sm: 4 }}>
        <Card variant="outlined" sx={{ height: '100%', borderRadius: 2.5 }}>
          <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
            <Stack direction="row" spacing={1.25} alignItems="center">
              <IconTool size={20} color="#d32f2f" />
              <Stack spacing={0.25}>
                <Typography variant="caption" color="text.secondary" fontWeight={700}>إجمالي الأعمال</Typography>
                <Typography variant="h4" fontWeight={850}>{display(totalWorks)}</Typography>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      </Grid>
      <Grid size={{ xs: 12, sm: 4 }}>
        <Card variant="outlined" sx={{ height: '100%', borderRadius: 2.5 }}>
          <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
            <Stack direction="row" spacing={1.25} alignItems="center">
              <IconReceipt size={20} color="#2e7d32" />
              <Stack spacing={0.25}>
                <Typography variant="caption" color="text.secondary" fontWeight={700}>إجمالي الدفعات</Typography>
                <Typography variant="h4" fontWeight={850}>{display(totalPayments)}</Typography>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      </Grid>
      <Grid size={{ xs: 12, sm: 4 }}>
        <Card variant="outlined" sx={{ height: '100%', borderRadius: 2.5, borderColor: balanceTone }}>
          <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
            <Stack direction="row" spacing={1.25} alignItems="center">
              <IconCash size={20} color={cashBalance > 0 ? '#2e7d32' : cashBalance < 0 ? '#d32f2f' : '#616161'} />
              <Stack spacing={0.25}>
                <Typography variant="caption" color="text.secondary" fontWeight={700}>الرصيد النقدي</Typography>
                <Typography variant="h4" fontWeight={850} color={balanceTone}>{display(cashBalance)}</Typography>
                <Typography variant="caption" color={balanceTone} fontWeight={700}>{unresolved ? 'قيد المطابقة' : cashBalanceStatus}</Typography>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
      </Grid>
    </Grid>
  );
}
