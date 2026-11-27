import { Box, Card, CardContent, Grid, Stack, Typography, useTheme } from '@mui/material';
import { IconCash, IconReceipt, IconWallet } from '@tabler/icons-react';

const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });

export function formatFinancialAmount(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return 'غير محدد';
  const num = value;
  if (num < 0) return `${money.format(Math.abs(num))}-`;
  return money.format(num);
}

export function resolveCashBalanceStatus(cashBalance: number, provided?: string | null): string {
  if (provided && provided.trim()) return provided.trim();
  if (cashBalance > 0) return 'العميل له فلوس';
  if (cashBalance < 0) return 'لنا مستحقات لدى العميل';
  return 'الحساب متوازن';
}

export type FinancialSummaryValues = {
  totalWorks: number | null | undefined;
  totalPayments: number | null | undefined;
  cashBalance: number | null | undefined;
  cashBalanceStatus?: string | null;
  unresolved?: boolean;
  /** 'grid' = 3-column horizontal cards (Dashboard / Project Overview), 'stacked' = vertical wide cards (Section view), 'compact' = inline 3-column strip (Project / Section cards) */
  layout?: 'grid' | 'stacked' | 'compact';
  title?: string;
  subtitle?: string;
};

export default function FinancialSummary({
  totalWorks,
  totalPayments,
  cashBalance,
  cashBalanceStatus,
  layout = 'grid',
  title,
  subtitle
}: FinancialSummaryValues) {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';

  const hasMissing = cashBalance == null || !Number.isFinite(cashBalance);
  const safeBalance = cashBalance ?? 0;
  const statusLabel = hasMissing ? 'الرصيد غير محدد' : resolveCashBalanceStatus(safeBalance, cashBalanceStatus);

  const balanceColors = hasMissing
    ? {
      bg: isDark ? '#263042' : '#475569',
      gradient: isDark ? 'linear-gradient(135deg, #253041 0%, #334155 100%)' : 'linear-gradient(135deg, #475569 0%, #64748b 100%)',
      text: isDark ? '#cbd5e1' : '#475569',
      cardText: '#ffffff',
      subText: 'rgba(255,255,255,0.82)'
    }
    : safeBalance > 0
      ? {
        bg: '#1b7a43',
        gradient: 'linear-gradient(135deg, #14532d 0%, #16a34a 100%)',
        text: isDark ? '#4ade80' : '#15803d',
        cardText: '#ffffff',
        subText: 'rgba(255,255,255,0.88)'
      }
      : safeBalance < 0
        ? {
          bg: '#9e3238',
          gradient: isDark
            ? 'linear-gradient(135deg, #5c1f24 0%, #992d34 100%)'
            : 'linear-gradient(135deg, #991b1b 0%, #dc2626 100%)',
          text: isDark ? '#f87171' : '#dc2626',
          cardText: '#ffffff',
          subText: 'rgba(255,255,255,0.88)'
        }
        : {
          bg: isDark ? '#2b3445' : '#475569',
          gradient: isDark ? 'linear-gradient(135deg, #263041 0%, #374151 100%)' : 'linear-gradient(135deg, #475569 0%, #64748b 100%)',
          text: isDark ? '#cbd5e1' : '#475569',
          cardText: '#ffffff',
          subText: 'rgba(255,255,255,0.82)'
        };

  // Compact inline 3-column financial strip (for My Projects & Project Section cards)
  if (layout === 'compact') {
    const items = [
      {
        label: 'إجمالي الأعمال',
        value: formatFinancialAmount(totalWorks),
        color: isDark ? '#f8fafc' : '#0f172a'
      },
      {
        label: 'إجمالي الدفعات',
        value: formatFinancialAmount(totalPayments),
        color: isDark ? '#f8fafc' : '#0f172a'
      },
      {
        label: 'الرصيد النقدي',
        value: formatFinancialAmount(cashBalance),
        color: balanceColors.text
      }
    ];

    return (
      <Grid container spacing={1.25} aria-label="الملخص المالي المختصر">
        {items.map((item) => (
          <Grid key={item.label} size={4}>
            <Box sx={{ minWidth: 0 }}>
              <Typography
                variant="caption"
                color="text.secondary"
                sx={{ display: 'block', fontWeight: 600, fontSize: { xs: '0.7rem', sm: '0.75rem' }, whiteSpace: 'nowrap' }}
              >
                {item.label}
              </Typography>
              <Typography
                variant="subtitle1"
                sx={{
                  fontWeight: 800,
                  color: item.color,
                  fontVariantNumeric: 'tabular-nums',
                  fontSize: { xs: '0.88rem', sm: '0.98rem' },
                  lineHeight: 1.35,
                  mt: 0.2,
                  overflowWrap: 'anywhere'
                }}
              >
                {item.value}
              </Typography>
            </Box>
          </Grid>
        ))}
      </Grid>
    );
  }

  const cards = [
    {
      key: 'works',
      label: 'إجمالي الأعمال',
      value: formatFinancialAmount(totalWorks),
      showCurrency: totalWorks != null,
      subtitle: undefined as string | undefined,
      gradient: isDark
        ? 'linear-gradient(135deg, #134688 0%, #1d5eb5 100%)'
        : 'linear-gradient(135deg, #1558b0 0%, #2563eb 100%)',
      icon: <IconReceipt size={22} stroke={1.9} />
    },
    {
      key: 'payments',
      label: 'إجمالي الدفعات',
      value: formatFinancialAmount(totalPayments),
      showCurrency: totalPayments != null,
      subtitle: undefined as string | undefined,
      gradient: isDark
        ? 'linear-gradient(135deg, #432889 0%, #5e35b1 100%)'
        : 'linear-gradient(135deg, #512da8 0%, #7c3aed 100%)',
      icon: <IconWallet size={22} stroke={1.9} />
    },
    {
      key: 'balance',
      label: 'الرصيد النقدي',
      value: formatFinancialAmount(cashBalance),
      showCurrency: !hasMissing,
      subtitle: statusLabel,
      gradient: balanceColors.gradient,
      icon: <IconCash size={22} stroke={1.9} />
    }
  ];

  const cardsGrid = (
    <Grid container spacing={1.5} aria-label="الملخص المالي">
      {cards.map((card) => (
        <Grid key={card.key} size={layout === 'stacked' ? { xs: 12 } : { xs: 12, sm: 4 }}>
          <Card
            elevation={0}
            sx={{
              height: '100%',
              borderRadius: 2.5,
              background: card.gradient,
              color: '#ffffff',
              boxShadow: isDark ? '0 6px 18px rgba(0, 0, 0, 0.28)' : '0 8px 20px rgba(15, 23, 42, 0.12)',
              transition: 'transform 160ms ease, box-shadow 160ms ease',
              '&:hover': {
                transform: 'translateY(-1px)'
              }
            }}
          >
            <CardContent
              sx={{
                p: { xs: 1.75, sm: 2 },
                '&:last-child': { pb: { xs: 1.75, sm: 2 } },
                height: '100%',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 1.5
              }}
            >
              <Box sx={{ minWidth: 0, flex: 1 }}>
                <Typography
                  variant="body2"
                  sx={{
                    color: 'rgba(255,255,255,0.88)',
                    fontWeight: 700,
                    fontSize: { xs: '0.8rem', sm: '0.85rem' }
                  }}
                >
                  {card.label}
                </Typography>
                <Stack direction="row" spacing={0.75} alignItems="baseline" flexWrap="wrap" useFlexGap sx={{ mt: 0.5 }}>
                  <Typography
                    variant="h3"
                    sx={{
                      color: '#ffffff',
                      fontWeight: 900,
                      fontVariantNumeric: 'tabular-nums',
                      fontSize: { xs: '1.25rem', sm: '1.4rem', md: '1.55rem' },
                      lineHeight: 1.2,
                      letterSpacing: '-0.01em'
                    }}
                  >
                    {card.value}
                  </Typography>
                  {card.showCurrency && (
                    <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.85)', fontWeight: 700, fontSize: '0.8rem' }}>
                      ج.م
                    </Typography>
                  )}
                </Stack>
                {card.subtitle && (
                  <Typography
                    variant="caption"
                    sx={{
                      display: 'block',
                      mt: 0.45,
                      color: 'rgba(255,255,255,0.9)',
                      fontWeight: 700,
                      fontSize: '0.76rem'
                    }}
                  >
                    {card.subtitle}
                  </Typography>
                )}
              </Box>
              <Box
                sx={{
                  width: 42,
                  height: 42,
                  borderRadius: 2,
                  display: 'grid',
                  placeItems: 'center',
                  bgcolor: 'rgba(255,255,255,0.16)',
                  color: '#ffffff',
                  flexShrink: 0
                }}
              >
                {card.icon}
              </Box>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  );

  if (!title && !subtitle) return cardsGrid;

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 3,
        borderColor: 'divider',
        bgcolor: 'background.paper'
      }}
    >
      <CardContent sx={{ p: { xs: 1.75, sm: 2.25 }, '&:last-child': { pb: { xs: 1.75, sm: 2.25 } } }}>
        {title && (
          <Typography variant="h4" sx={{ fontWeight: 850, mb: 1.5 }}>
            {title}
          </Typography>
        )}
        {cardsGrid}
        {subtitle && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.25, fontWeight: 600 }}>
            {subtitle}
          </Typography>
        )}
      </CardContent>
    </Card>
  );
}
