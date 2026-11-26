import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Grid,
  IconButton,
  InputAdornment,
  Paper,
  Skeleton,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography
} from '@mui/material';
import { useTheme } from '@mui/material/styles';
import {
  IconAlertTriangle,
  IconArrowRight,
  IconBuildingSkyscraper,
  IconCheck,
  IconExternalLink,
  IconEye,
  IconFileSpreadsheet,
  IconInfoCircle,
  IconListDetails,
  IconRefresh,
  IconSearch,
  IconX
} from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { EmgConsolidatedStatementRow, EmgStatementColumn, getEmgConsolidatedStatementPreview } from 'api/emg';
import { localizeDiagnosticText, localizeEntityType } from '../reportLabels';

const money = new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const formatMoney = (value: number | null | undefined) => (value === null || value === undefined ? '—' : `${money.format(value)} ج.م`);

const statusLabel: Record<string, string> = {
  VERIFIED_PROJECTION: 'مصنف',
  INCOMPLETE_UNVERIFIED: 'غير مكتمل التحقق',
  UNAPPROVED_OVERLAP: 'تشخيصي متداخل',
  UNAPPROVED: 'غير معتمد',
  DIAGNOSTIC: 'تشخيصي'
};

function isDiagnosticColumn(column: EmgStatementColumn) {
  return !column.isOfficial || column.status !== 'VERIFIED_PROJECTION' || column.column === 'H' || column.column === 'T';
}

function ColumnValue({ column, showImported }: { column: EmgStatementColumn; showImported: boolean }) {
  const diagnostic = isDiagnosticColumn(column);
  const amount = column.isOfficial ? column.currentAmount : column.column === 'H' ? column.diagnosticAmount : null;
  const imported = column.isOfficial ? column.importedAmount : null;
  const isZero = (amount === null || amount === 0) && (!showImported || imported === null || imported === 0);

  if (isZero && !diagnostic) {
    return (
      <Typography variant="body2" sx={{ color: 'text.disabled', fontWeight: 500, textAlign: 'end', minWidth: 112 }}>
        —
      </Typography>
    );
  }

  return (
    <Stack spacing={0.25} alignItems="flex-end" sx={{ minWidth: 124 }}>
      <Stack direction="row" spacing={0.5} alignItems="center">
        {diagnostic && column.diagnostic && (
          <Tooltip title={localizeDiagnosticText(column.diagnostic)} arrow>
            <Box component="span" sx={{ display: 'inline-flex', color: 'warning.main', cursor: 'help' }}>
              <IconInfoCircle size={14} />
            </Box>
          </Tooltip>
        )}
        <Typography
          variant="body2"
          sx={{
            fontWeight: amount && amount !== 0 ? 800 : 600,
            fontVariantNumeric: 'tabular-nums',
            color:
              amount === null || amount === 0
                ? 'text.disabled'
                : column.isOfficial
                  ? 'text.primary'
                  : 'warning.dark'
          }}
        >
          {amount === null ? '—' : amount === 0 ? '0.00 ج.م' : formatMoney(amount)}
        </Typography>
      </Stack>

      {column.isOfficial ? (
        showImported && imported !== null && imported !== 0 ? (
          <Typography variant="caption" sx={{ color: 'text.secondary', fontSize: '0.7rem', fontVariantNumeric: 'tabular-nums' }}>
            مستورد: {formatMoney(imported)}
          </Typography>
        ) : null
      ) : (
        <Chip
          size="small"
          label={statusLabel[column.status] || column.status}
          sx={{
            height: 18,
            fontSize: '0.66rem',
            fontWeight: 700,
            bgcolor: 'rgba(217, 119, 6, 0.12)',
            color: 'warning.dark'
          }}
        />
      )}
    </Stack>
  );
}

function DimensionCard({ label, value, imported, meaning }: { label: string; value: number; imported: number; meaning: string }) {
  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        borderRadius: 2.5,
        bgcolor: 'background.paper',
        borderColor: 'divider',
        transition: 'border-color 180ms ease, box-shadow 180ms ease',
        '&:hover': {
          borderColor: 'primary.main',
          boxShadow: '0 6px 18px rgba(15, 23, 42, 0.06)'
        }
      }}
    >
      <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 0.75 }}>
          <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 700 }}>
            {label}
          </Typography>
          <Tooltip title={localizeDiagnosticText(meaning, `إجمالي ${label} المعتمد للمشروع.`)} arrow>
            <Box component="span" sx={{ display: 'inline-flex', color: 'text.secondary', cursor: 'help' }}>
              <IconInfoCircle size={15} />
            </Box>
          </Tooltip>
        </Stack>
        <Typography variant="h4" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums', color: value ? 'text.primary' : 'text.disabled' }}>
          {formatMoney(value)}
        </Typography>
        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>
          الأساس المستورد: {formatMoney(imported)}
        </Typography>
      </CardContent>
    </Card>
  );
}

function DetailsDialog({ row, onClose }: { row: EmgConsolidatedStatementRow | null; onClose: () => void }) {
  if (!row) return null;
  const dimensions = [
    ['التكاليف التشغيلية', row.dimensions.operationalCosts],
    ['توريدات الموردين', row.dimensions.supplierSupplies],
    ['مدفوعات الموردين', row.dimensions.supplierPayments],
    ['تمويل المالك', row.dimensions.ownerFunding],
    ['توزيعات الدفعات', row.dimensions.advanceAllocations],
    ['مدفوعات المكتب', row.dimensions.officePayments]
  ] as const;

  return (
    <Dialog open fullWidth maxWidth="lg" onClose={onClose} dir="rtl" PaperProps={{ sx: { borderRadius: 3 } }}>
      <DialogTitle sx={{ px: 3, py: 2.25 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} alignItems={{ xs: 'flex-start', sm: 'center' }} justifyContent="space-between" spacing={1.5}>
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
              <Typography variant="h3" sx={{ fontWeight: 800 }}>
                {row.projectName}
              </Typography>
              <Chip size="small" label={`كود: ${row.projectCode}`} variant="outlined" sx={{ fontWeight: 700 }} />
              <Chip
                size="small"
                color={row.isComplete ? 'success' : 'warning'}
                label={row.isComplete ? 'مكتمل التصنيف' : 'يحتاج مراجعة'}
                sx={{ fontWeight: 700 }}
              />
            </Stack>
            <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
              تفاصيل الأبعاد المالية ومصادر التصنيف والسجلات غير المحسومة
            </Typography>
          </Box>

          <Stack direction="row" spacing={1} alignItems="center" sx={{ flexShrink: 0 }}>
            <Button
              component={Link}
              to={`/emg/reports/${row.projectId}`}
              onClick={onClose}
              size="small"
              variant="contained"
              color="secondary"
              endIcon={<IconExternalLink size={16} />}
              sx={{ borderRadius: 2, fontWeight: 700, whiteSpace: 'nowrap' }}
            >
              فتح تقرير المشروع
            </Button>
            <IconButton onClick={onClose} aria-label="إغلاق" size="small">
              <IconX size={20} />
            </IconButton>
          </Stack>
        </Stack>
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3 }}>
        <Alert severity={row.isComplete ? 'success' : 'warning'} sx={{ mb: 2.5, borderRadius: 2 }}>
          {row.isComplete
            ? 'كل السجلات المالية لهذا المشروع مصنّفة بالكامل ضمن الأبعاد المعتمدة.'
            : 'يوجد مبلغ أو أكثر يحتاج مراجعة محاسبية، ولم يتم إسقاطه من التفاصيل أدناه.'}
        </Alert>

        <Typography variant="subtitle1" sx={{ fontWeight: 800, mb: 1.25 }}>
          الأبعاد المالية للمشروع
        </Typography>
        <Grid container spacing={1.75} sx={{ mb: 2.5 }}>
          {dimensions.map(([label, value]) => (
            <Grid key={label} size={{ xs: 12, sm: 6, md: 4 }}>
              <DimensionCard label={label} value={value.currentAmount} imported={value.importedAmount} meaning={value.meaning} />
            </Grid>
          ))}
        </Grid>

        <Typography variant="subtitle1" sx={{ fontWeight: 800, mb: 1.25 }}>
          المؤشرات التشخيصية المنفصلة
        </Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.75} sx={{ mb: 2.5 }}>
          <Card
            variant="outlined"
            sx={{
              flex: 1,
              p: 2,
              borderRadius: 2.5,
              borderColor: 'warning.main',
              bgcolor: 'rgba(217, 119, 6, 0.04)'
            }}
          >
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.secondary' }}>
                استحقاق المكتب التشخيصي (عمود H)
              </Typography>
              <Chip size="small" color="warning" label="تشخيصي غير معتمد" sx={{ fontWeight: 700 }} />
            </Stack>
            <Typography variant="h4" sx={{ fontWeight: 800, mt: 0.75, color: 'warning.dark' }}>
              {formatMoney(row.officeAccrual.diagnosticAmount)}
            </Typography>
            <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.75, lineHeight: 1.6 }}>
              {localizeDiagnosticText(
                row.officeAccrual.diagnostic,
                'قيمة تشخيصية مشتقة فقط؛ أساس نسبة المكتب التعاقدية غير معتمد بعد.'
              )}
            </Typography>
          </Card>

          <Card
            variant="outlined"
            sx={{
              flex: 1,
              p: 2,
              borderRadius: 2.5,
              borderColor: 'warning.main',
              bgcolor: 'rgba(217, 119, 6, 0.04)'
            }}
          >
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.secondary' }}>
                صافي المعاملة التشخيصي (عمود T)
              </Typography>
              <Chip size="small" color="warning" label="تشخيصي غير معتمد" sx={{ fontWeight: 700 }} />
            </Stack>
            <Typography variant="h4" sx={{ fontWeight: 800, mt: 0.75, color: 'text.disabled' }}>
              —
            </Typography>
            <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 0.75, lineHeight: 1.6 }}>
              السيناريوهات التشخيصية منفصلة ولا تمثل رصيدًا رسميًا ولا تُجمع ضمن الأعمدة المعتمدة.
            </Typography>
          </Card>
        </Stack>

        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.25 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
            السجلات غير المحسومة
          </Typography>
          <Chip
            size="small"
            color={row.unresolvedRecords.length === 0 ? 'success' : 'warning'}
            label={`${row.unresolvedRecords.length} سجل`}
            sx={{ fontWeight: 700 }}
          />
        </Stack>

        {row.unresolvedRecords.length === 0 ? (
          <Alert severity="success" sx={{ borderRadius: 2 }}>
            لا توجد سجلات غير محسومة لهذا المشروع.
          </Alert>
        ) : (
          <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
            <Table size="small">
              <TableHead>
                <TableRow sx={{ bgcolor: 'action.hover' }}>
                  <TableCell sx={{ fontWeight: 800 }}>المصدر</TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>الورقة / الصف</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                    المبلغ
                  </TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>التشخيص</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {row.unresolvedRecords.map((item) => (
                  <TableRow key={`${item.entityType}-${item.entityId}`} hover>
                    <TableCell sx={{ fontWeight: 700 }}>
                      {localizeEntityType(item.entityType)} #{item.entityId}
                    </TableCell>
                    <TableCell>
                      {item.sourceSheet || 'غير متاح'} {item.sourceRow ? `/ ${item.sourceRow}` : ''}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                      {formatMoney(item.amount)}
                    </TableCell>
                    <TableCell>{localizeDiagnosticText(item.diagnostic, 'سجل غير محسوم التصنيف ويحتاج مراجعة محاسبية.')}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </DialogContent>
    </Dialog>
  );
}

export default function ConsolidatedStatement() {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const { user } = useAuth();

  const [rows, setRows] = useState<EmgConsolidatedStatementRow[]>([]);
  const [snapshotRule, setSnapshotRule] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<EmgConsolidatedStatementRow | null>(null);

  // UX Controls
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'complete' | 'review'>('all');
  const [showImported, setShowImported] = useState(false);
  const [hideEmptyColumns, setHideEmptyColumns] = useState(true);

  const load = () => {
    setLoading(true);
    setError(null);
    getEmgConsolidatedStatementPreview()
      .then((preview) => {
        setRows(preview.projects);
        setSnapshotRule(preview.snapshotRule);
      })
      .catch((reason) => {
        const status = reason?.response?.status;
        setError(status === 401 || status === 403 ? 'ليس لديك صلاحية عرض الكشف المجمع.' : 'تعذر تحميل الكشف المجمع حاليًا.');
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const allColumns = useMemo(() => rows[0]?.columns || [], [rows]);
  const unresolvedCount = useMemo(() => rows.reduce((sum, row) => sum + row.unresolvedRecords.length, 0), [rows]);
  const incompleteCount = useMemo(() => rows.filter((row) => !row.isComplete).length, [rows]);
  const completeCount = rows.length - incompleteCount;

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return rows.filter((row) => {
      if (statusFilter === 'complete' && !row.isComplete) return false;
      if (statusFilter === 'review' && row.isComplete) return false;
      if (!q) return true;
      return row.projectName.toLowerCase().includes(q) || String(row.projectCode || '').toLowerCase().includes(q);
    });
  }, [rows, search, statusFilter]);

  const visibleColumns = useMemo(() => {
    if (!hideEmptyColumns || filteredRows.length === 0) return allColumns;
    return allColumns.filter((col, colIndex) =>
      filteredRows.some((row) => {
        const cell = row.columns[colIndex];
        if (!cell) return false;
        const val = cell.isOfficial ? cell.currentAmount : cell.diagnosticAmount;
        return (val !== null && val !== undefined && val !== 0) || (cell.importedAmount !== null && cell.importedAmount !== undefined && cell.importedAmount !== 0);
      })
    );
  }, [allColumns, filteredRows, hideEmptyColumns]);

  const visibleColumnIndices = useMemo(() => {
    const allowed = new Set(visibleColumns.map((c) => c.column));
    return allColumns.reduce<number[]>((acc, col, idx) => {
      if (allowed.has(col.column)) acc.push(idx);
      return acc;
    }, []);
  }, [allColumns, visibleColumns]);

  if (user?.permissions && !user.permissions.includes('reports.view')) {
    return <Alert severity="error">ليس لديك صلاحية عرض التقارير.</Alert>;
  }

  const headerBg = isDark ? '#1a233a' : '#f8fafc';
  const diagHeaderBg = isDark ? 'rgba(217, 119, 6, 0.16)' : '#fffbeb';
  const diagCellBg = isDark ? 'rgba(217, 119, 6, 0.06)' : 'rgba(254, 243, 199, 0.32)';

  return (
    <Box sx={{ pb: 5 }}>
      {/* Breadcrumbs */}
      <Breadcrumbs sx={{ mb: 1.5 }}>
        <Button component={Link} to="/emg/reports" size="small" startIcon={<IconArrowRight size={16} />} sx={{ fontWeight: 700 }}>
          التقارير
        </Button>
        <Typography variant="body2" color="text.primary" sx={{ fontWeight: 700 }}>
          كشف الحساب المجمع
        </Typography>
      </Breadcrumbs>

      {/* Executive Page Header */}
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', md: 'center' }}
        spacing={2}
        sx={{ mb: 3 }}
      >
        <Box>
          <Stack direction="row" spacing={1.25} alignItems="center">
            <Typography variant="h2" sx={{ fontWeight: 850 }}>
              كشف الحساب المجمع
            </Typography>
            <Tooltip
              title={localizeDiagnosticText(
                snapshotRule,
                'البيانات للقراءة فقط: القيم الحالية تشمل كافة السجلات المعتمدة والمرحّلة، والأساس المستورد يمثل الجزء التاريخي منها، بينما عمودا H و T تشخيصيان فقط.'
              )}
              arrow
            >
              <Chip
                size="small"
                icon={<IconInfoCircle size={15} />}
                label="للقراءة فقط"
                variant="outlined"
                color="primary"
                sx={{ fontWeight: 700, cursor: 'help' }}
              />
            </Tooltip>
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            عرض موحد للمشاريع المسموح بها وفق أعمدة النموذج الرئيسي — اضغط على أي مشروع لعرض تفاصيل الأبعاد والتصنيف
          </Typography>
        </Box>

        <Stack direction="row" spacing={1.25}>
          <Button
            component={Link}
            to="/emg/reports"
            variant="outlined"
            color="inherit"
            startIcon={<IconListDetails size={17} />}
            sx={{ borderRadius: 2, fontWeight: 700 }}
          >
            تقارير المشاريع
          </Button>
          <Button
            variant="contained"
            color="secondary"
            onClick={load}
            startIcon={<IconRefresh size={17} />}
            disabled={loading}
            sx={{ borderRadius: 2, fontWeight: 700 }}
          >
            {loading ? 'جاري التحديث...' : 'تحديث البيانات'}
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2.5, borderRadius: 2 }}>
          {error}
        </Alert>
      )}

      {/* Loading Skeleton */}
      {loading && (
        <Stack spacing={2.5}>
          <Grid container spacing={2}>
            {[0, 1, 2, 3].map((i) => (
              <Grid key={i} size={{ xs: 12, sm: 6, lg: 3 }}>
                <Skeleton variant="rounded" height={112} sx={{ borderRadius: 3 }} />
              </Grid>
            ))}
          </Grid>
          <Skeleton variant="rounded" height={420} sx={{ borderRadius: 3 }} />
        </Stack>
      )}

      {!loading && !error && rows.length === 0 && (
        <MainCard sx={{ borderRadius: 3 }}>
          <Alert severity="info">لا توجد مشاريع متاحة ضمن نطاق صلاحياتك.</Alert>
        </MainCard>
      )}

      {!loading && !error && rows.length > 0 && (
        <>
          {/* Tier 1: Berry Signature Executive Summary KPIs */}
          <Grid container spacing={2} sx={{ mb: 3 }}>
            {/* Berry Blue Gradient Card */}
            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card
                sx={{
                  height: '100%',
                  borderRadius: 3,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                  boxShadow: '0 10px 26px rgba(13, 71, 161, 0.22)'
                }}
              >
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
                        المشاريع المتاحة
                      </Typography>
                      <Typography variant="h2" sx={{ color: '#fff', fontWeight: 900, mt: 0.5 }}>
                        {rows.length}
                      </Typography>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.78)' }}>
                        ضمن نطاق صلاحياتك
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 46, height: 46 }}>
                      <IconBuildingSkyscraper size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            {/* Berry Purple Gradient Card */}
            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card
                sx={{
                  height: '100%',
                  borderRadius: 3,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #311b92 0%, #7e57c2 100%)',
                  boxShadow: '0 10px 26px rgba(49, 27, 146, 0.22)'
                }}
              >
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
                        مكتمل التصنيف
                      </Typography>
                      <Typography variant="h2" sx={{ color: '#fff', fontWeight: 900, mt: 0.5 }}>
                        {completeCount}
                      </Typography>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.78)' }}>
                        {rows.length ? `${Math.round((completeCount / rows.length) * 100)}% من المشاريع` : '—'}
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 46, height: 46 }}>
                      <IconCheck size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            {/* Clean White Card: Needs Review */}
            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card variant="outlined" sx={{ height: '100%', borderRadius: 3, bgcolor: 'background.paper' }}>
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                        يحتاج مراجعة محاسبية
                      </Typography>
                      <Typography
                        variant="h2"
                        sx={{ fontWeight: 900, mt: 0.5, color: incompleteCount > 0 ? 'warning.dark' : 'success.main' }}
                      >
                        {incompleteCount}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {incompleteCount > 0 ? 'اضغط على المشروع للفحص' : 'جميع المشاريع مكتملة'}
                      </Typography>
                    </Box>
                    <Avatar
                      sx={{
                        bgcolor: incompleteCount > 0 ? 'rgba(217, 119, 6, 0.14)' : 'rgba(16, 185, 129, 0.14)',
                        color: incompleteCount > 0 ? 'warning.dark' : 'success.main',
                        width: 46,
                        height: 46
                      }}
                    >
                      <IconAlertTriangle size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            {/* Clean White Card: Unresolved Records */}
            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card variant="outlined" sx={{ height: '100%', borderRadius: 3, bgcolor: 'background.paper' }}>
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                        سجلات غير محسومة
                      </Typography>
                      <Typography
                        variant="h2"
                        sx={{ fontWeight: 900, mt: 0.5, color: unresolvedCount > 0 ? 'warning.dark' : 'success.main' }}
                      >
                        {unresolvedCount}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        الأعمدة المعروضة: {visibleColumns.length} من {allColumns.length}
                      </Typography>
                    </Box>
                    <Avatar
                      sx={{
                        bgcolor: 'rgba(94, 53, 177, 0.12)',
                        color: 'secondary.main',
                        width: 46,
                        height: 46
                      }}
                    >
                      <IconFileSpreadsheet size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Tier 2: Unified Table Card with Search, Filters & View Toggles */}
          <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden', borderColor: 'divider' }}>
            {/* Filter & View Controls Bar */}
            <Box sx={{ p: 2, borderBottom: '1px solid', borderColor: 'divider', bgcolor: isDark ? 'background.paper' : '#fcfcfd' }}>
              <Stack
                direction={{ xs: 'column', lg: 'row' }}
                spacing={2}
                alignItems={{ xs: 'stretch', lg: 'center' }}
                justifyContent="space-between"
              >
                {/* Search + Status Chips */}
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ xs: 'stretch', sm: 'center' }} flexWrap="wrap" useFlexGap>
                  <TextField
                    size="small"
                    placeholder="ابحث باسم المشروع أو الكود..."
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    sx={{ minWidth: { xs: '100%', sm: 260 } }}
                    InputProps={{
                      startAdornment: (
                        <InputAdornment position="start">
                          <IconSearch size={17} />
                        </InputAdornment>
                      ),
                      endAdornment: search ? (
                        <InputAdornment position="end">
                          <IconButton size="small" onClick={() => setSearch('')} aria-label="مسح البحث">
                            <IconX size={14} />
                          </IconButton>
                        </InputAdornment>
                      ) : undefined
                    }}
                  />

                  <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
                    <Chip
                      clickable
                      size="small"
                      label={`الكل (${rows.length})`}
                      color={statusFilter === 'all' ? 'primary' : 'default'}
                      variant={statusFilter === 'all' ? 'filled' : 'outlined'}
                      onClick={() => setStatusFilter('all')}
                      sx={{ fontWeight: 700 }}
                    />
                    <Chip
                      clickable
                      size="small"
                      label={`مكتمل (${completeCount})`}
                      color={statusFilter === 'complete' ? 'success' : 'default'}
                      variant={statusFilter === 'complete' ? 'filled' : 'outlined'}
                      onClick={() => setStatusFilter('complete')}
                      sx={{ fontWeight: 700 }}
                    />
                    <Chip
                      clickable
                      size="small"
                      label={`يحتاج مراجعة (${incompleteCount})`}
                      color={statusFilter === 'review' ? 'warning' : 'default'}
                      variant={statusFilter === 'review' ? 'filled' : 'outlined'}
                      onClick={() => setStatusFilter('review')}
                      sx={{ fontWeight: 700 }}
                    />
                  </Stack>
                </Stack>

                {/* View Switches */}
                <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap" useFlexGap>
                  <FormControlLabel
                    sx={{ m: 0 }}
                    control={<Switch size="small" checked={hideEmptyColumns} onChange={(e) => setHideEmptyColumns(e.target.checked)} />}
                    label={<Typography variant="caption" sx={{ fontWeight: 700 }}>إخفاء الأعمدة الفارغة</Typography>}
                  />
                  <FormControlLabel
                    sx={{ m: 0 }}
                    control={<Switch size="small" checked={showImported} onChange={(e) => setShowImported(e.target.checked)} />}
                    label={<Typography variant="caption" sx={{ fontWeight: 700 }}>إظهار المستورد التاريخي</Typography>}
                  />
                </Stack>
              </Stack>
            </Box>

            {/* Clean Scannable Sticky Table */}
            {filteredRows.length === 0 ? (
              <Box sx={{ p: 4, textAlign: 'center' }}>
                <Typography variant="subtitle1" color="text.secondary" sx={{ fontWeight: 700 }}>
                  لا توجد مشاريع مطابقة لخيارات البحث أو التصفية الحالية.
                </Typography>
              </Box>
            ) : (
              <TableContainer sx={{ maxHeight: 'calc(100vh - 360px)', overflowX: 'auto' }}>
                <Table stickyHeader size="small" aria-label="كشف الحساب المجمع">
                  <TableHead>
                    <TableRow>
                      <TableCell
                        sx={{
                          position: 'sticky',
                          insetInlineStart: 0,
                          zIndex: 5,
                          minWidth: 260,
                          bgcolor: headerBg,
                          borderBottom: '2px solid',
                          borderColor: 'divider',
                          borderLeft: '1px solid',
                          borderLeftColor: 'divider',
                          py: 1.5
                        }}
                      >
                        <Typography variant="subtitle2" sx={{ fontWeight: 800, color: 'text.primary' }}>
                          المشروع ({filteredRows.length})
                        </Typography>
                      </TableCell>

                      {visibleColumns.map((column) => {
                        const diag = column.column === 'H' || column.column === 'T';
                        return (
                          <TableCell
                            key={column.column}
                            align="right"
                            sx={{
                              minWidth: column.column === 'U' ? 190 : 150,
                              bgcolor: diag ? diagHeaderBg : headerBg,
                              borderBottom: '2px solid',
                              borderBottomColor: diag ? 'warning.main' : 'divider',
                              py: 1.25,
                              whiteSpace: 'nowrap'
                            }}
                          >
                            <Stack spacing={0.4} alignItems="flex-end">
                              <Stack direction="row" spacing={0.5} alignItems="center">
                                {diag && (
                                  <Chip
                                    size="small"
                                    label="تشخيصي"
                                    sx={{
                                      height: 17,
                                      fontSize: '0.62rem',
                                      fontWeight: 800,
                                      bgcolor: 'rgba(217, 119, 6, 0.18)',
                                      color: 'warning.dark'
                                    }}
                                  />
                                )}
                                <Chip
                                  size="small"
                                  label={column.column}
                                  sx={{
                                    height: 18,
                                    fontSize: '0.68rem',
                                    fontWeight: 800,
                                    bgcolor: diag ? 'warning.main' : 'rgba(94, 53, 177, 0.12)',
                                    color: diag ? '#fff' : 'secondary.main'
                                  }}
                                />
                              </Stack>
                              <Typography variant="caption" sx={{ fontWeight: 800, color: diag ? 'warning.dark' : 'text.primary' }}>
                                {column.heading}
                              </Typography>
                            </Stack>
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  </TableHead>

                  <TableBody>
                    {filteredRows.map((row) => (
                      <TableRow
                        hover
                        key={row.projectId}
                        onClick={() => setSelected(row)}
                        sx={{
                          cursor: 'pointer',
                          '&:last-child td': { borderBottom: 0 },
                          '&:hover td': {
                            bgcolor: isDark ? 'rgba(144, 202, 249, 0.06)' : 'rgba(94, 53, 177, 0.03)'
                          }
                        }}
                      >
                        <TableCell
                          sx={{
                            position: 'sticky',
                            insetInlineStart: 0,
                            zIndex: 2,
                            bgcolor: 'background.paper',
                            borderLeft: '1px solid',
                            borderLeftColor: 'divider',
                            py: 1.5
                          }}
                        >
                          <Stack spacing={0.6}>
                            <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                              <Typography variant="subtitle2" sx={{ fontWeight: 800, lineHeight: 1.35 }}>
                                {row.projectName}
                              </Typography>
                              <Tooltip title="عرض التفاصيل والأبعاد المالية" arrow>
                                <Box component="span" sx={{ color: 'text.secondary', display: 'inline-flex', mt: 0.2 }}>
                                  <IconEye size={16} />
                                </Box>
                              </Tooltip>
                            </Stack>
                            <Stack direction="row" spacing={0.75} alignItems="center" flexWrap="wrap" useFlexGap>
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                                كود: {row.projectCode}
                              </Typography>
                              <Chip
                                size="small"
                                color={row.isComplete ? 'success' : 'warning'}
                                label={row.isComplete ? 'مكتمل التصنيف' : 'يحتاج مراجعة'}
                                sx={{ height: 20, fontSize: '0.68rem', fontWeight: 700 }}
                              />
                              {row.unresolvedRecords.length > 0 && (
                                <Chip
                                  size="small"
                                  variant="outlined"
                                  color="warning"
                                  label={`${row.unresolvedRecords.length} غير محسوم`}
                                  sx={{ height: 20, fontSize: '0.66rem', fontWeight: 700 }}
                                />
                              )}
                            </Stack>
                          </Stack>
                        </TableCell>

                        {visibleColumnIndices.map((colIndex) => {
                          const column = row.columns[colIndex];
                          if (!column) return <TableCell key={colIndex} />;
                          const diag = column.column === 'H' || column.column === 'T';
                          return (
                            <TableCell
                              key={column.column}
                              align="right"
                              sx={{
                                bgcolor: diag ? diagCellBg : undefined,
                                py: 1.35
                              }}
                            >
                              <ColumnValue column={column} showImported={showImported} />
                            </TableCell>
                          );
                        })}
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}

            {/* Clean Arabic Footer Note */}
            <Divider />
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              justifyContent="space-between"
              alignItems={{ xs: 'flex-start', sm: 'center' }}
              spacing={1}
              sx={{ px: 2.25, py: 1.5, bgcolor: isDark ? 'background.default' : '#f8fafc' }}
            >
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
                اضغط على أي مشروع لعرض تفاصيل الأبعاد المالية ومصادر التصنيف والسجلات غير المحسومة.
              </Typography>
              <Typography variant="caption" color="warning.dark" sx={{ fontWeight: 700 }}>
                لا يوجد صف إجمالي نهائي أسفل الجدول لتجنّب جمع الأعمدة التشخيصية المتداخلة (H و T).
              </Typography>
            </Stack>
          </MainCard>
        </>
      )}

      <DetailsDialog row={selected} onClose={() => setSelected(null)} />
    </Box>
  );
}
