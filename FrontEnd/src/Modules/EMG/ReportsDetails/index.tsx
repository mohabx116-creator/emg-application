import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Grid,
  InputAdornment,
  Link as MuiLink,
  Skeleton,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
  useTheme
} from '@mui/material';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  IconAlertCircle,
  IconArrowRight,
  IconBuilding,
  IconCash,
  IconChartDonut,
  IconCheck,
  IconCoin,
  IconDownload,
  IconHammer,
  IconPrinter,
  IconSearch,
  IconTableExport,
  IconTrendingUp,
  IconTruckDelivery
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { hasAnyPermission } from 'Modules/EMG/roleAccess';
import { localizeCategory, localizeCurrency } from '../reportLabels';
import { withAlpha } from 'utils/colorUtils';
import {
  downloadEmgProjectMovementsCsv,
  downloadEmgProjectWorkbook,
  EmgHistoricalProjectReport,
  EmgOperationalAudit,
  EmgProjectReport,
  getEmgHistoricalProjectReport,
  getEmgProjectAudit,
  getEmgProjectReport
} from 'api/emg';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;
const formatDate = (value: string) => (value ? new Date(value).toLocaleDateString('ar-EG') : '—');

function GradientKpiCard({
  title,
  value,
  subtitle,
  icon,
  variant = 'blue'
}: {
  title: string;
  value: string;
  subtitle?: string;
  icon: React.ReactNode;
  variant?: 'blue' | 'purple';
}) {
  const gradient =
    variant === 'purple'
      ? 'linear-gradient(135deg, #311b92 0%, #5e35b1 55%, #7e57c2 100%)'
      : 'linear-gradient(135deg, #0d47a1 0%, #1565c0 55%, #1e88e5 100%)';

  return (
    <Card
      sx={{
        height: '100%',
        position: 'relative',
        overflow: 'hidden',
        borderRadius: 3,
        color: '#ffffff',
        background: gradient,
        boxShadow: '0 8px 24px rgba(13, 43, 82, 0.16)',
        '&::before': {
          content: '""',
          position: 'absolute',
          width: 150,
          height: 150,
          borderRadius: '50%',
          background: 'rgba(255,255,255,0.10)',
          top: -60,
          left: -45,
          pointerEvents: 'none'
        }
      }}
    >
      <CardContent sx={{ position: 'relative', zIndex: 1, p: 2.25, '&:last-child': { pb: 2.25 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.86)', fontWeight: 600 }}>
              {title}
            </Typography>
            <Typography variant="h3" sx={{ color: '#ffffff', fontWeight: 800, mt: 0.75, fontVariantNumeric: 'tabular-nums' }}>
              {value}
            </Typography>
            {subtitle && (
              <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.80)', display: 'block', mt: 0.5 }}>
                {subtitle}
              </Typography>
            )}
          </Box>
          <Box
            sx={{
              width: 40,
              height: 40,
              borderRadius: 2,
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
              bgcolor: 'rgba(255,255,255,0.92)',
              color: variant === 'purple' ? '#4527a0' : '#1565c0'
            }}
          >
            {icon}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

function SolidIconKpiCard({
  title,
  value,
  subtitle,
  icon,
  tone = 'error'
}: {
  title: string;
  value: string;
  subtitle?: string;
  icon: React.ReactNode;
  tone?: 'error' | 'warning' | 'success' | 'primary';
}) {
  const theme = useTheme();
  const colorMap = {
    error: theme.vars.palette.error.dark,
    warning: theme.vars.palette.warning.main,
    success: theme.vars.palette.success.dark,
    primary: theme.vars.palette.primary.main
  };
  const accent = colorMap[tone];

  return (
    <Card variant="outlined" sx={{ height: '100%', borderRadius: 3, borderColor: 'divider' }}>
      <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 600 }}>
              {title}
            </Typography>
            <Typography variant="h3" sx={{ color: accent, fontWeight: 800, mt: 0.75, fontVariantNumeric: 'tabular-nums' }}>
              {value}
            </Typography>
            {subtitle && (
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                {subtitle}
              </Typography>
            )}
          </Box>
          <Box
            sx={{
              width: 40,
              height: 40,
              borderRadius: 2,
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
              bgcolor: accent,
              color: '#ffffff'
            }}
          >
            {icon}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

function ReportDetailsSkeleton() {
  return (
    <Box sx={{ pb: 4, minWidth: 0, maxWidth: '100%' }}>
      <Skeleton variant="text" width={180} height={24} sx={{ mb: 1.5 }} />
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={1.5} sx={{ mb: 2.5 }}>
        <Box sx={{ minWidth: 0, maxWidth: '100%' }}>
          <Skeleton variant="text" width={260} height={38} />
          <Skeleton variant="text" width={320} height={22} />
        </Box>
        <Stack direction="row" spacing={1}>
          <Skeleton variant="rounded" width={100} height={38} />
          <Skeleton variant="rounded" width={100} height={38} />
        </Stack>
      </Stack>
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {[0, 1, 2, 3].map((i) => (
          <Grid key={i} size={{ xs: 12, sm: 6, lg: 3 }}>
            <Skeleton variant="rounded" height={116} sx={{ borderRadius: 3 }} />
          </Grid>
        ))}
      </Grid>
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid size={{ xs: 12, lg: 7 }}>
          <Skeleton variant="rounded" height={280} sx={{ borderRadius: 3 }} />
        </Grid>
        <Grid size={{ xs: 12, lg: 5 }}>
          <Skeleton variant="rounded" height={280} sx={{ borderRadius: 3 }} />
        </Grid>
      </Grid>
      <Skeleton variant="rounded" height={240} sx={{ borderRadius: 3 }} />
    </Box>
  );
}

export default function EmgReportsDetails() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const theme = useTheme();
  const { projectId } = useParams();
  const id = Number(projectId);

  const canViewReports = hasAnyPermission(user, ['reports.view']);
  const canExport = hasAnyPermission(user, ['reports.export']);

  const [report, setReport] = useState<EmgProjectReport | null>(null);
  const [audit, setAudit] = useState<EmgOperationalAudit | null>(null);
  const [historical, setHistorical] = useState<EmgHistoricalProjectReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<'xlsx' | 'csv' | null>(null);
  const [activeTab, setActiveTab] = useState(0);
  const [ledgerQuery, setLedgerQuery] = useState('');

  useEffect(() => {
    if (!canViewReports) {
      setLoading(false);
      return;
    }
    if (!Number.isInteger(id)) {
      setError('رقم المشروع غير صحيح.');
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    Promise.all([
      getEmgProjectReport(id),
      getEmgProjectAudit(id),
      getEmgHistoricalProjectReport(id).catch(() => null)
    ])
      .then(([loadedReport, loadedAudit, loadedHistorical]) => {
        setReport(loadedReport);
        setAudit(loadedAudit);
        setHistorical(loadedHistorical);
      })
      .catch(() => setError('تعذر تحميل تفاصيل التقرير. تأكد من صلاحياتك أو وجود المشروع.'))
      .finally(() => setLoading(false));
  }, [canViewReports, id]);

  const download = async (type: 'xlsx' | 'csv') => {
    setDownloading(type);
    try {
      if (type === 'xlsx') await downloadEmgProjectWorkbook(id);
      else await downloadEmgProjectMovementsCsv(id);
    } catch {
      setError('تعذر إنشاء ملف التصدير.');
    } finally {
      setDownloading(null);
    }
  };

  const q = ledgerQuery.trim().toLowerCase();

  const filteredMovements = useMemo(
    () =>
      (report?.movements ?? []).filter(
        (m) => !q || `${m.description} ${m.type} ${m.supplierName || ''} ${m.reference || ''} ${m.status}`.toLowerCase().includes(q)
      ),
    [report?.movements, q]
  );

  const filteredAdvances = useMemo(
    () =>
      (report?.advances ?? []).filter(
        (a) => !q || `${a.payerName || ''} ${a.clientName || ''} ${a.reference || ''} ${a.status}`.toLowerCase().includes(q)
      ),
    [report?.advances, q]
  );

  const filteredSupplies = useMemo(
    () =>
      (report?.supplierSupplies ?? []).filter(
        (s) => !q || `${s.supplierName} ${s.itemName} ${s.supplyType || ''} ${s.invoiceNumber || ''}`.toLowerCase().includes(q)
      ),
    [report?.supplierSupplies, q]
  );

  const filteredPayments = useMemo(
    () =>
      (report?.supplierPayments ?? []).filter(
        (p) => !q || `${p.supplierName} ${p.method} ${p.reference || ''}`.toLowerCase().includes(q)
      ),
    [report?.supplierPayments, q]
  );

  if (!canViewReports) {
    return <Alert severity="error">ليس لديك صلاحية عرض التقارير.</Alert>;
  }

  if (loading) return <ReportDetailsSkeleton />;

  if (error || !report || !audit) {
    return (
      <Alert severity="error" action={<Button onClick={() => navigate('/emg/reports')}>العودة للتقارير</Button>}>
        {error || 'التقرير غير متاح.'}
      </Alert>
    );
  }

  const account = report.finalAccount;
  const verifiedBalance = (amount: number) => account.hasUnresolvedImportRecords ? 'قيد المطابقة' : formatMoney(amount);
  const auditItems = [
    { label: 'أعمال الحفر', value: audit.excavationTotal, color: theme.vars.palette.secondary.main, icon: <IconHammer size={18} /> },
    { label: 'الخرسانات', value: audit.concreteTotal, color: theme.vars.palette.primary.main, icon: <IconChartDonut size={18} /> },
    { label: 'الحديد', value: audit.steelTotal, color: theme.vars.palette.success.dark, icon: <IconCoin size={18} /> },
    { label: 'التشطيبات', value: audit.finishingTotal, color: theme.vars.palette.warning.dark, icon: <IconTrendingUp size={18} /> },
    { label: 'توريدات الموردين', value: audit.supplierSuppliesTotal, color: theme.vars.palette.secondary.dark, icon: <IconTruckDelivery size={18} /> },
    { label: 'مدفوعات الموردين', value: audit.supplierPaymentsTotal, color: theme.vars.palette.error.dark, icon: <IconCash size={18} /> }
  ];

  const statementRows: Array<{ label: string; value: string; tone?: 'error' | 'success' | 'primary' }> = [
    { label: 'العميل', value: account.clientName || '—' },
    { label: 'الدافع', value: account.payerName || '—' },
    { label: 'إجمالي الوارد', value: formatMoney(account.totalIncoming), tone: 'success' },
    { label: 'إجمالي الصادر', value: formatMoney(account.totalOutgoing ?? account.totalExpenses) },
    { label: 'الرصيد قبل نصيب المكتب', value: verifiedBalance(account.netBeforeOfficeShare) },
    { label: `نصيب المكتب (${account.officeSharePercentage}%)`, value: verifiedBalance(account.officeShareAmount) },
    { label: 'دفعات المكتب المسددة', value: formatMoney(account.officePaid ?? 0) },
    {
      label: 'صافي المستحق للمكتب',
      value: verifiedBalance(account.officeNetDue ?? 0),
      tone: (account.officeNetDue ?? 0) > 0 ? 'error' : 'success'
    },
    { label: 'المتبقي للموردين', value: formatMoney(account.supplierOutstanding), tone: account.supplierOutstanding > 0 ? 'error' : 'success' },
    {
      label: 'رصيد الدافع',
      value: `${verifiedBalance(account.payerBalance)}${account.balanceStatus ? ` · ${account.balanceStatus}` : ''}`,
      tone: account.payerBalance > 0 ? 'error' : 'success'
    }
  ];

  const validChecksCount = audit.checks.filter((c) => c.isValid).length;

  return (
    <Box sx={{ pb: 4, minWidth: 0, maxWidth: '100%' }}>
      {/* Breadcrumbs */}
      <Breadcrumbs sx={{ mb: 1.5 }}>
        <MuiLink component={Link} to="/emg/reports" underline="hover" color="text.secondary">
          التقارير
        </MuiLink>
        <Typography color="primary" fontWeight={600}>
          {account.projectName}
        </Typography>
      </Breadcrumbs>

      {/* Executive Header */}
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', md: 'center' }}
        spacing={2}
        sx={{ mb: 2.5, minWidth: 0, maxWidth: '100%' }}
      >
        <Box sx={{ minWidth: 0, maxWidth: '100%' }}>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            <Typography variant="h2" sx={{ fontSize: { xs: '1.45rem', sm: '1.75rem' }, fontWeight: 800 }}>
              {account.projectName}
            </Typography>
            {account.status && (
              <Chip size="small" label={account.status} color={account.status === 'معتمد' || account.status === 'نشط' ? 'success' : 'warning'} />
            )}
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.4 }}>
            التقرير المالي والتشغيلي التفصيلي · العميل: {account.clientName || '—'} · الدافع: {account.payerName || '—'}
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          <Button size="small" variant="outlined" onClick={() => navigate('/emg/reports')} startIcon={<IconArrowRight size={16} />} sx={{ borderRadius: 2 }}>
            التقارير
          </Button>
          <Button size="small" variant="outlined" component={Link} to={`/emg/projects/${id}`} startIcon={<IconBuilding size={16} />} sx={{ borderRadius: 2 }}>
            المشروع
          </Button>
          {canExport && (
            <>
              <Button
                size="small"
                variant="contained"
                disabled={!!downloading}
                onClick={() => download('xlsx')}
                startIcon={<IconDownload size={16} />}
                sx={{ borderRadius: 2, fontWeight: 700 }}
              >
                {downloading === 'xlsx' ? 'جاري التصدير...' : 'تصدير Excel'}
              </Button>
              <Button
                size="small"
                variant="outlined"
                disabled={!!downloading}
                onClick={() => download('csv')}
                startIcon={<IconTableExport size={16} />}
                sx={{ borderRadius: 2 }}
              >
                {downloading === 'csv' ? 'جاري التصدير...' : 'CSV'}
              </Button>
            </>
          )}
          <Button size="small" variant="text" onClick={() => window.print()} startIcon={<IconPrinter size={16} />} sx={{ borderRadius: 2 }}>
            طباعة
          </Button>
        </Stack>
      </Stack>

      {/* Tier 1: 4 Executive Financial KPIs */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <GradientKpiCard
            title="إجمالي الداخل"
            value={formatMoney(account.totalIncoming)}
            subtitle={`${report.advances.length} دفعة تحت الحساب`}
            icon={<IconTrendingUp size={20} />}
            variant="blue"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <GradientKpiCard
            title="صافي المشروع"
            value={verifiedBalance(account.netAfterOfficeShare)}
            subtitle={account.balanceStatus ? `الحالة: ${account.balanceStatus}` : 'بعد نصيب المكتب'}
            icon={<IconCash size={20} />}
            variant="purple"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <SolidIconKpiCard
            title="إجمالي المصروفات"
            value={formatMoney(account.totalExpenses)}
            subtitle={`${report.movements.length} حركة موقع`}
            icon={<IconCoin size={20} />}
            tone="error"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <SolidIconKpiCard
            title={`نصيب المكتب (${account.officeSharePercentage}%)`}
            value={verifiedBalance(account.officeShareAmount)}
            subtitle={`المستحق: ${verifiedBalance(account.officeNetDue ?? 0)}`}
            icon={<IconChartDonut size={20} />}
            tone="warning"
          />
        </Grid>
      </Grid>

      {/* Tier 2: Clean Final Account Ledger (7 cols) + Operational Audit Breakdown (5 cols) */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid size={{ xs: 12, lg: 7 }}>
          <MainCard title="كشف الحساب النهائي للمشروع" sx={{ borderRadius: 3, height: '100%' }} contentSX={{ p: { xs: 2, sm: 2.5 } }}>
            <Grid container spacing={1.5}>
              {statementRows.map((row) => (
                <Grid key={row.label} size={{ xs: 12, sm: 6 }}>
                  <Stack
                    direction="row"
                    justifyContent="space-between"
                    alignItems="center"
                    spacing={1}
                    sx={{
                      py: 1.15,
                      px: 1.5,
                      borderRadius: 2,
                      bgcolor: 'action.hover'
                    }}
                  >
                    <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 500 }}>
                      {row.label}
                    </Typography>
                    <Typography
                      variant="subtitle2"
                      sx={{
                        fontWeight: 700,
                        fontVariantNumeric: 'tabular-nums',
                        color:
                          row.tone === 'error'
                            ? 'error.dark'
                            : row.tone === 'success'
                              ? 'success.dark'
                              : row.tone === 'primary'
                                ? 'primary.dark'
                                : 'text.primary'
                      }}
                    >
                      {row.value}
                    </Typography>
                  </Stack>
                </Grid>
              ))}
            </Grid>
          </MainCard>
        </Grid>

        <Grid size={{ xs: 12, lg: 5 }}>
          <MainCard
            title="تدقيق بنود الأعمال"
            secondary={
              <Chip
                size="small"
                icon={validChecksCount === audit.checks.length ? <IconCheck size={14} /> : <IconAlertCircle size={14} />}
                label={`${validChecksCount}/${audit.checks.length} فحص سليم`}
                color={validChecksCount === audit.checks.length ? 'success' : 'warning'}
                variant="outlined"
              />
            }
            sx={{ borderRadius: 3, height: '100%' }}
            contentSX={{ p: { xs: 2, sm: 2.5 } }}
          >
            <Grid container spacing={1.25}>
              {auditItems.map((item) => (
                <Grid key={item.label} size={{ xs: 6 }}>
                  <Card variant="outlined" sx={{ borderRadius: 2.5, borderColor: 'divider' }}>
                    <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                      <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.75 }}>
                        <Box
                          sx={{
                            width: 30,
                            height: 30,
                            borderRadius: 1.5,
                            display: 'grid',
                            placeItems: 'center',
                            bgcolor: withAlpha(item.color, 0.14),
                            color: item.color,
                            flexShrink: 0
                          }}
                        >
                          {item.icon}
                        </Box>
                        <Typography variant="caption" color="text.secondary" fontWeight={600} noWrap>
                          {item.label}
                        </Typography>
                      </Stack>
                      <Typography variant="subtitle1" fontWeight={800} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                        {formatMoney(item.value)}
                      </Typography>
                    </CardContent>
                  </Card>
                </Grid>
              ))}
            </Grid>

            {audit.checks.some((c) => !c.isValid) && (
              <>
                <Divider sx={{ my: 1.75 }} />
                <Stack spacing={0.75}>
                  {audit.checks
                    .filter((c) => !c.isValid)
                    .map((check) => (
                      <Alert key={check.name} severity="warning" sx={{ py: 0.25 }}>
                        <Typography variant="caption" fontWeight={600}>
                          {check.name}: {check.message}
                        </Typography>
                      </Alert>
                    ))}
                </Stack>
              </>
            )}
          </MainCard>
        </Grid>
      </Grid>

      {/* Tier 3: Unified Tabbed Detailed Ledgers */}
      <MainCard sx={{ borderRadius: 3, minWidth: 0, maxWidth: '100%' }} contentSX={{ p: { xs: 1.5, sm: 2.5 } }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ xs: 'stretch', md: 'center' }}
          spacing={1.5}
          sx={{ mb: 2, borderBottom: '1px solid', borderColor: 'divider', pb: 1.5 }}
        >
          <Tabs
            value={activeTab}
            onChange={(_, val) => {
              setActiveTab(val);
              setLedgerQuery('');
            }}
            variant="scrollable"
            scrollButtons="auto"
          >
            <Tab label={`حركات الموقع (${report.movements.length})`} />
            <Tab label={`دفعات تحت الحساب (${report.advances.length})`} />
            <Tab label={`توريدات الموردين (${report.supplierSupplies.length})`} />
            <Tab label={`مدفوعات الموردين (${report.supplierPayments.length})`} />
          </Tabs>

          <TextField
            size="small"
            value={ledgerQuery}
            onChange={(e) => setLedgerQuery(e.target.value)}
            placeholder="بحث داخل الجدول..."
            sx={{ minWidth: { md: 240 } }}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <IconSearch size={17} />
                  </InputAdornment>
                )
              }
            }}
          />
        </Stack>

        {/* Tab 0: Site Movements */}
        {activeTab === 0 && (
          filteredMovements.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
              لا توجد حركات موقع مطابقة.
            </Typography>
          ) : (
            <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow sx={{ '& th': { fontWeight: 700, color: 'text.secondary', whiteSpace: 'nowrap' } }}>
                    <TableCell>التاريخ</TableCell>
                    <TableCell>النوع</TableCell>
                    <TableCell>الوصف</TableCell>
                    <TableCell>المورد</TableCell>
                    <TableCell>المرجع</TableCell>
                    <TableCell align="right">المبلغ</TableCell>
                    <TableCell align="center">الحالة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredMovements.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(item.date)}</TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.type}</TableCell>
                      <TableCell>{item.description}</TableCell>
                      <TableCell>{item.supplierName || '—'}</TableCell>
                      <TableCell>{item.reference || '—'}</TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {formatMoney(item.amount)}
                      </TableCell>
                      <TableCell align="center">
                        <Chip size="small" label={item.status} color={item.status === 'معتمد' || item.status === 'مرحّل' ? 'success' : 'warning'} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )
        )}

        {/* Tab 1: Advances */}
        {activeTab === 1 && (
          filteredAdvances.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
              لا توجد دفعات تحت الحساب مسجلة.
            </Typography>
          ) : (
            <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow sx={{ '& th': { fontWeight: 700, color: 'text.secondary', whiteSpace: 'nowrap' } }}>
                    <TableCell>التاريخ</TableCell>
                    <TableCell>الدافع / العميل</TableCell>
                    <TableCell>المرجع</TableCell>
                    <TableCell align="right">المبلغ</TableCell>
                    <TableCell align="right">الموزّع</TableCell>
                    <TableCell align="right">المتبقي</TableCell>
                    <TableCell align="center">الحالة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredAdvances.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(item.date)}</TableCell>
                      <TableCell>{item.payerName || item.clientName}</TableCell>
                      <TableCell>{item.reference || '—'}</TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {formatMoney(item.amount)}
                      </TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                        {formatMoney(item.allocated)}
                      </TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 600, color: 'warning.dark', whiteSpace: 'nowrap' }}>
                        {formatMoney(item.remaining)}
                      </TableCell>
                      <TableCell align="center">
                        <Chip size="small" label={item.status} color={item.status === 'معتمد' || item.status === 'مرحّل' ? 'success' : 'warning'} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )
        )}

        {/* Tab 2: Supplier Supplies */}
        {activeTab === 2 && (
          filteredSupplies.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
              لا توجد توريدات موردين مسجلة.
            </Typography>
          ) : (
            <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow sx={{ '& th': { fontWeight: 700, color: 'text.secondary', whiteSpace: 'nowrap' } }}>
                    <TableCell>التاريخ</TableCell>
                    <TableCell>المورد</TableCell>
                    <TableCell>الصنف</TableCell>
                    <TableCell>الكمية</TableCell>
                    <TableCell align="right">سعر الوحدة</TableCell>
                    <TableCell align="right">الإجمالي</TableCell>
                    <TableCell>رقم الفاتورة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredSupplies.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(item.date)}</TableCell>
                      <TableCell>{item.supplierName}</TableCell>
                      <TableCell>{item.itemName}</TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        {item.quantity} {item.unit}
                      </TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                        {formatMoney(item.unitPrice)}
                      </TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {formatMoney(item.totalAmount)}
                      </TableCell>
                      <TableCell>{item.invoiceNumber || '—'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )
        )}

        {/* Tab 3: Supplier Payments */}
        {activeTab === 3 && (
          filteredPayments.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
              لا توجد مدفوعات موردين مسجلة.
            </Typography>
          ) : (
            <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow sx={{ '& th': { fontWeight: 700, color: 'text.secondary', whiteSpace: 'nowrap' } }}>
                    <TableCell>التاريخ</TableCell>
                    <TableCell>المورد</TableCell>
                    <TableCell align="right">المبلغ</TableCell>
                    <TableCell>طريقة الدفع</TableCell>
                    <TableCell>المرجع</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredPayments.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(item.date)}</TableCell>
                      <TableCell>{item.supplierName}</TableCell>
                      <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 700, whiteSpace: 'nowrap' }}>
                        {formatMoney(item.amount)}
                      </TableCell>
                      <TableCell>{item.method}</TableCell>
                      <TableCell>{item.reference || '—'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )
        )}
      </MainCard>
      {historical && (
        <MainCard
          title={(
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Box>
                <Typography variant="h4">السجل التاريخي المنشور</Typography>
                <Typography variant="body2" color="text.secondary">
                  قراءة منفصلة عن الحساب التشغيلي — لا تدخل في إجمالياته
                </Typography>
              </Box>
              <Chip size="small" label={`${historical.entries.length} حركة`} color="info" variant="outlined" />
            </Stack>
          )}
          sx={{ mt: 2 }}
        >
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2 }}>
            {historical.totals.map((item) => (
              <Chip
                key={`${item.ledgerStream}-${item.currency}`}
                label={`${localizeCategory(item.ledgerStream)} · ${localizeCurrency(item.currency)}: ${money.format(item.amount)} (${item.entryCount})`}
                color="info"
                variant="outlined"
              />
            ))}
          </Stack>
          {historical.openingBalances.length > 0 && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              أرصدة البداية التشغيلية المحفوظة: {historical.openingBalances.map((item) => `${item.streamKey} ${money.format(item.amount)} ${localizeCurrency(item.currency)}`).join(' · ')}
            </Alert>
          )}
          {historical.entries.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 2, textAlign: 'center' }}>
              لا توجد حركات تاريخية منشورة.
            </Typography>
          ) : (
            <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>المصدر</TableCell>
                    <TableCell>الشيت / الخلية</TableCell>
                    <TableCell>التصنيف</TableCell>
                    <TableCell align="right">المبلغ</TableCell>
                    <TableCell>رقم الدفعة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {historical.entries.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell>{item.sourceFileName}</TableCell>
                      <TableCell>{item.sheetName} · {item.sourceCoordinate}</TableCell>
                      <TableCell>{localizeCategory(item.businessEventType)} / {localizeCategory(item.ledgerStream)}</TableCell>
                      <TableCell align="right">{money.format(item.amount)} {localizeCurrency(item.currency)}</TableCell>
                      <TableCell>#{item.batchId}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </MainCard>
      )}
    </Box>
  );
}
