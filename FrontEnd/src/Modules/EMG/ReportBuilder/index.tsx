import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Checkbox,
  Chip,
  Collapse,
  Dialog,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  Grid,
  IconButton,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tabs,
  TextField,
  Typography
} from '@mui/material';
import { IconAdjustmentsHorizontal, IconArrowRight, IconChartBar, IconDownload, IconFilterOff, IconInfoCircle, IconPlayerPlay, IconSearch, IconX } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import {
  EmgProject,
  EmgReportBuilderPresentation,
  EmgReportBuilderPreview,
  EmgReportBuilderQuery,
  downloadEmgReportBuilderWorkbook,
  getEmgProjects,
  runEmgReportBuilder
} from 'api/emg';
import { hasPermission as userHasPermission } from '../roleAccess';
import { localizeCategory, localizeDiagnosticText, localizeGroupLabel } from '../reportLabels';

const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const formatMoney = (value: number | null | undefined) => (value === null || value === undefined ? '—' : `${money.format(value)} ج.م`);
const dimensions = [['operational-cost', 'تكلفة تشغيلية'], ['supplier-supply', 'توريدات الموردين'], ['supplier-payment', 'مدفوعات الموردين'], ['owner-funding', 'تمويل المالك'], ['advance-allocation', 'توزيعات الدفعات'], ['office-payment', 'مدفوعات المكتب'], ['unresolved', 'غير محسوم']] as const;
const presets = [['master', 'النموذج المجمع الأصلي'], ['detail', 'التفصيل المالي للمشروع'], ['party', 'كشف المقاول/المورد'], ['management', 'ملخص الإدارة']] as const;
const categories = [
  ['SiteExpense', 'مصروفات الموقع'], ['Excavation', 'الحفر'], ['ContractorWorkmanship', 'أعمال المقاول'], ['ConcreteStation', 'خرسانة المحطة'],
  ['Steel', 'الحديد'], ['Buildings', 'المباني'], ['Plaster', 'المحارة'], ['Insulation', 'العزل'], ['Electrical', 'الكهرباء'], ['Plumbing', 'السباكة'],
  ['OfficePayment', 'مدفوعات المكتب'], ['OwnerFunding', 'تمويل المالك'], ['FundingAllocation', 'توزيعات الدفعات'], ['SupplierSupply', 'توريدات الموردين'],
  ['SupplierPayment', 'مدفوعات الموردين'], ['Unresolved', 'غير محسوم']
] as const;
const categoryLabelMap: Record<string, string> = Object.fromEntries(categories);
const groupOptions = [['project', 'المشروع'], ['category', 'التصنيف'], ['party', 'الطرف'], ['date', 'التاريخ'], ['dimension', 'البعد المالي']] as const;
const columnOptions = [['project', 'المشروع'], ['category', 'التصنيف'], ['party', 'الطرف'], ['date', 'التاريخ'], ['importedAmount', 'المستورد'], ['currentAmount', 'الحالي'], ['unresolvedAmount', 'غير محسوم'], ['source', 'المصدر']] as const;
function transactionValue(row: EmgReportBuilderPreview['transactions'][number], column: string) {
  switch (column) {
    case 'project': return `${row.projectCode} · ${row.projectName}`;
    case 'category': return categoryLabelMap[row.category] || row.category;
    case 'party': return row.party || 'غير محدد';
    case 'date': return row.transactionDate ? new Date(row.transactionDate).toLocaleDateString('ar-EG-u-nu-latn') : '—';
    case 'importedAmount': return formatMoney(row.importedAmount);
    case 'currentAmount': return formatMoney(row.currentAmount);
    case 'unresolvedAmount': return formatMoney(row.unresolvedAmount);
    case 'source': return `${row.sourceSheet || 'غير متاح'}${row.sourceRow ? ` / ${row.sourceRow}` : ''}`;
    default: return '—';
  }
}

function Details({ item, onClose }: { item: EmgReportBuilderPreview['transactions'][number] | null; onClose: () => void }) {
  if (!item) return null;
  return (
    <Dialog open fullWidth maxWidth="sm" onClose={onClose} dir="rtl">
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        تفاصيل الحركة
        <IconButton onClick={onClose} aria-label="إغلاق">
          <IconX />
        </IconButton>
      </DialogTitle>
      <DialogContent dividers>
        <Stack spacing={1.5}>
          <Typography><b>المشروع:</b> {item.projectName} · {item.projectCode}</Typography>
          <Typography><b>التصنيف:</b> {localizeCategory(item.category)}</Typography>
          <Typography><b>الطرف:</b> {item.party || 'غير محدد'}</Typography>
          <Typography><b>المبلغ الحالي:</b> {formatMoney(item.currentAmount)}</Typography>
          <Typography><b>المبلغ المستورد:</b> {formatMoney(item.importedAmount)}</Typography>
          <Typography><b>المصدر:</b> {item.sourceSheet || 'غير متاح'} {item.sourceRow ? `/ ${item.sourceRow}` : ''}</Typography>
          <Typography color="text.secondary">{item.sourceText || 'لا يوجد مرجع مصدر.'}</Typography>
          {item.isUnresolved && <Alert severity="warning">{localizeDiagnosticText(item.diagnostic)}</Alert>}
        </Stack>
      </DialogContent>
    </Dialog>
  );
}

export default function ReportBuilder() {
  const { user } = useAuth();
  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [scope, setScope] = useState<'all' | 'selected'>('all');
  const [selectedProjects, setSelectedProjects] = useState<number[]>([]);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selectedDimensions, setSelectedDimensions] = useState<string[]>([]);
  const [preset, setPreset] = useState('master');
  const [selectedGroups, setSelectedGroups] = useState<string[]>(['project', 'category']);
  const [selectedColumns, setSelectedColumns] = useState<string[]>(columnOptions.map(([value]) => value));
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [parties, setParties] = useState('');
  const [snapshot, setSnapshot] = useState<'imported' | 'current' | 'both'>('both');
  const [detailLevel, setDetailLevel] = useState<'totals' | 'transactions' | 'both'>('both');
  const [sortBy, setSortBy] = useState<'project' | 'category' | 'party' | 'date' | 'amount'>('project');
  const [includeUnresolved, setIncludeUnresolved] = useState(true);
  const [result, setResult] = useState<EmgReportBuilderPreview | null>(null);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [presentation, setPresentation] = useState<EmgReportBuilderPresentation>('custom');
  const [exportMessage, setExportMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState(0);
  const [builderExpanded, setBuilderExpanded] = useState(true);
  const [selectedTransaction, setSelectedTransaction] = useState<EmgReportBuilderPreview['transactions'][number] | null>(null);

  // Instant results table filters & pagination
  const [tableSearch, setTableSearch] = useState('');
  const [tableProjectFilter, setTableProjectFilter] = useState<string>('all');
  const [tableCategoryFilter, setTableCategoryFilter] = useState<string>('all');
  const [tableQuickFilter, setTableQuickFilter] = useState<'all' | 'nonzero' | 'review'>('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  const applyPreset = (value: string) => {
    setPreset(value);
    if (value === 'master') { setSelectedGroups(['project', 'category']); setSelectedColumns(columnOptions.map(([item]) => item)); setSnapshot('both'); setDetailLevel('both'); setSelectedCategories([]); setSelectedDimensions([]); }
    if (value === 'detail') { setSelectedGroups(['project', 'category', 'date']); setSelectedColumns(columnOptions.map(([item]) => item)); setSnapshot('current'); setDetailLevel('both'); }
    if (value === 'party') { setSelectedGroups(['party', 'category']); setSelectedColumns(['project', 'category', 'party', 'date', 'currentAmount', 'source']); setSnapshot('current'); setDetailLevel('both'); setSelectedCategories(['ContractorWorkmanship', 'SupplierSupply', 'SupplierPayment']); }
    if (value === 'management') { setSelectedGroups(['project']); setSelectedColumns(['project', 'currentAmount', 'unresolvedAmount']); setSnapshot('current'); setDetailLevel('totals'); setSelectedCategories([]); }
  };

  useEffect(() => { getEmgProjects().then(setProjects).catch(() => setError('تعذر تحميل المشاريع المتاحة.')); }, []);
  const currentQuery = (): EmgReportBuilderQuery => ({
    projectIds: scope === 'selected' ? selectedProjects : null,
    fromDate: fromDate || null,
    toDate: toDate || null,
    categories: selectedCategories.length ? selectedCategories : null,
    parties: parties.split(',').map((item) => item.trim()).filter(Boolean),
    dimensions: selectedDimensions.length ? selectedDimensions : null,
    detailLevel,
    groupBy: selectedGroups,
    columns: selectedColumns,
    snapshot,
    sortBy,
    includeUnresolved
  });
  const run = () => {
    setLoading(true);
    setError(null);
    setExportMessage(null);
    setPage(0);
    setTableSearch('');
    setTableProjectFilter('all');
    setTableCategoryFilter('all');
    setTableQuickFilter('all');
    runEmgReportBuilder(currentQuery())
      .then(setResult)
      .catch((reason: any) => setError(reason?.response?.status === 403 ? 'ليس لديك صلاحية تشغيل مركز التقارير.' : 'تعذر تنفيذ التقرير بهذه الفلاتر.'))
      .finally(() => setLoading(false));
  };
  const exportExcel = async () => {
    setExporting(true);
    setError(null);
    setExportMessage(null);
    try {
      await downloadEmgReportBuilderWorkbook(currentQuery(), presentation);
      setExportMessage('بدأ تنزيل ملف Excel.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'تعذّر إنشاء ملف Excel.');
    } finally {
      setExporting(false);
    }
  };
  const moveColumn = (index: number, direction: -1 | 1) => {
    const next = [...selectedColumns]; const target = index + direction; if (target < 0 || target >= next.length) return; [next[index], next[target]] = [next[target], next[index]]; setSelectedColumns(next);
  };
  const hasPermission = !user?.permissions || user.permissions.includes('reports.view');
  const canExport = userHasPermission(user, 'reports.export');
  const noTransactionColumns = detailLevel !== 'totals' && selectedColumns.length === 0;
  const invalidConfiguration = noTransactionColumns || (scope === 'selected' && selectedProjects.length === 0);
  const resultSummary = useMemo(() => result?.summary || [], [result]);
  const resultTransactions = useMemo(() => result?.transactions || [], [result]);

  const distinctResultProjects = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of resultTransactions) {
      const key = `${row.projectCode}`;
      if (!map.has(key)) map.set(key, `${row.projectCode} · ${row.projectName}`);
    }
    return Array.from(map.entries());
  }, [resultTransactions]);

  const distinctResultCategories = useMemo(() => {
    const set = new Set<string>();
    for (const row of resultTransactions) {
      if (row.category) set.add(row.category);
    }
    return Array.from(set);
  }, [resultTransactions]);

  const filteredSummary = useMemo(() => {
    const q = tableSearch.trim().toLowerCase();
    return resultSummary.filter((row) => {
      if (tableQuickFilter === 'nonzero' && !row.currentAmount && !row.importedAmount && !row.unresolvedAmount) return false;
      if (tableQuickFilter === 'review' && row.isComplete && !row.unresolvedAmount) return false;
      if (!q) return true;
      return `${row.groupLabel} ${row.groupKey}`.toLowerCase().includes(q);
    });
  }, [resultSummary, tableSearch, tableQuickFilter]);

  const filteredTransactions = useMemo(() => {
    const q = tableSearch.trim().toLowerCase();
    return resultTransactions.filter((row) => {
      if (tableProjectFilter !== 'all' && String(row.projectCode) !== tableProjectFilter) return false;
      if (tableCategoryFilter !== 'all' && row.category !== tableCategoryFilter) return false;
      if (tableQuickFilter === 'nonzero' && !row.currentAmount && !row.importedAmount && !row.unresolvedAmount) return false;
      if (tableQuickFilter === 'review' && !row.isUnresolved && !row.unresolvedAmount) return false;
      if (!q) return true;
      const arCat = categoryLabelMap[row.category] || '';
      return `${row.projectName} ${row.projectCode} ${row.category} ${arCat} ${row.party || ''} ${row.sourceSheet || ''} ${row.sourceText || ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [resultTransactions, tableSearch, tableProjectFilter, tableCategoryFilter, tableQuickFilter]);

  const activeTotalCount = tab === 0 ? filteredSummary.length : filteredTransactions.length;
  const paginatedSummary = useMemo(
    () => filteredSummary.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage),
    [filteredSummary, page, rowsPerPage]
  );
  const paginatedTransactions = useMemo(
    () => filteredTransactions.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage),
    [filteredTransactions, page, rowsPerPage]
  );

  const hasActiveTableFilters =
    Boolean(tableSearch.trim()) || tableProjectFilter !== 'all' || tableCategoryFilter !== 'all' || tableQuickFilter !== 'all';

  const resetTableFilters = () => {
    setTableSearch('');
    setTableProjectFilter('all');
    setTableCategoryFilter('all');
    setTableQuickFilter('all');
    setPage(0);
  };

  useEffect(() => {
    setPage(0);
  }, [tab, tableSearch, tableProjectFilter, tableCategoryFilter, tableQuickFilter]);

  if (!hasPermission) return <Alert severity="error">ليس لديك صلاحية عرض التقارير.</Alert>;

  return <Box sx={{ pb: 5 }}>
    <Breadcrumbs sx={{ mb: 2 }}><Button component={Link} to="/emg/reports" size="small" startIcon={<IconArrowRight size={16} />}>التقارير</Button><Typography color="text.primary">مركز التقارير</Typography></Breadcrumbs>
    <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', md: 'center' }} spacing={2} sx={{ mb: 2.5 }}>
      <Box>
        <Typography variant="h2" sx={{ fontWeight: 850 }}>مركز التقارير المالي</Typography>
        <Typography color="text.secondary" sx={{ mt: 0.5 }}>ابنِ تقريرك من نفس البيانات والتصنيفات المعتمدة دون تغيير أي سجل</Typography>
      </Box>
      <Stack direction="row" spacing={1.25}>
        {result && (
          <Button
            variant="outlined"
            color="secondary"
            startIcon={<IconAdjustmentsHorizontal size={17} />}
            onClick={() => setBuilderExpanded((prev) => !prev)}
            sx={{ borderRadius: 2, fontWeight: 700 }}
          >
            {builderExpanded ? 'إخفاء إعدادات التقرير' : 'إظهار إعدادات التقرير'}
          </Button>
        )}
        <Button component={Link} to="/emg/reports/consolidated" variant="outlined" startIcon={<IconChartBar size={17} />} sx={{ borderRadius: 2, fontWeight: 700 }}>
          النموذج المجمع
        </Button>
      </Stack>
    </Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {exportMessage && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setExportMessage(null)}>{exportMessage}</Alert>}
    <Collapse in={builderExpanded}>
    <Grid container spacing={2.25} alignItems="stretch">
      <Grid size={{ xs: 12, lg: 4 }}><MainCard title={<Stack direction="row" spacing={1} alignItems="center"><IconAdjustmentsHorizontal size={20} /><Typography variant="h4">تركيب التقرير</Typography></Stack>}>
        <Stack spacing={2}>
          <FormControl fullWidth size="small"><InputLabel>قالب اختياري</InputLabel><Select value={preset} label="قالب اختياري" onChange={(event) => applyPreset(event.target.value)}>{presets.map(([value, label]) => <MenuItem key={value} value={value}>{label}</MenuItem>)}</Select></FormControl>
          <FormControl fullWidth size="small"><InputLabel>النطاق</InputLabel><Select value={scope} label="النطاق" onChange={(event) => setScope(event.target.value as 'all' | 'selected')}><MenuItem value="all">كل المشاريع المسموح بها</MenuItem><MenuItem value="selected">مشاريع محددة</MenuItem></Select></FormControl>
          {scope === 'selected' && <FormControl fullWidth size="small"><InputLabel>المشاريع</InputLabel><Select multiple value={selectedProjects} label="المشاريع" onChange={(event) => setSelectedProjects(event.target.value as number[])} renderValue={(values) => projects.filter((project) => (values as number[]).includes(project.id)).map((project) => project.code).join('، ')}>{projects.map((project) => <MenuItem key={project.id} value={project.id}><Checkbox checked={selectedProjects.includes(project.id)} /><Typography>{project.name} · {project.code}</Typography></MenuItem>)}</Select></FormControl>}
          <Stack direction="row" spacing={1}><TextField fullWidth size="small" type="date" label="من تاريخ" InputLabelProps={{ shrink: true }} value={fromDate} onChange={(event) => setFromDate(event.target.value)} /><TextField fullWidth size="small" type="date" label="إلى تاريخ" InputLabelProps={{ shrink: true }} value={toDate} onChange={(event) => setToDate(event.target.value)} /></Stack>
          <TextField size="small" label="الأطراف" placeholder="مقاول، مورد..." helperText="أسماء الأطراف مفصولة بفاصلة" value={parties} onChange={(event) => setParties(event.target.value)} />
          <FormControl fullWidth size="small"><InputLabel>لقطة البيانات</InputLabel><Select value={snapshot} label="لقطة البيانات" onChange={(event) => setSnapshot(event.target.value as typeof snapshot)}><MenuItem value="both">المستورد والحالي</MenuItem><MenuItem value="imported">المستورد التاريخي</MenuItem><MenuItem value="current">المعتمد والمرحّل الحالي</MenuItem></Select></FormControl>
          <FormControl fullWidth size="small"><InputLabel>مستوى التفاصيل</InputLabel><Select value={detailLevel} label="مستوى التفاصيل" onChange={(event) => setDetailLevel(event.target.value as typeof detailLevel)}><MenuItem value="totals">إجماليات فقط</MenuItem><MenuItem value="transactions">حركات فقط</MenuItem><MenuItem value="both">الإجماليات والحركات</MenuItem></Select></FormControl>
          <FormControl fullWidth size="small"><InputLabel>ترتيب الحركات</InputLabel><Select value={sortBy} label="ترتيب الحركات" onChange={(event) => setSortBy(event.target.value as typeof sortBy)}><MenuItem value="project">المشروع</MenuItem><MenuItem value="category">التصنيف</MenuItem><MenuItem value="party">الطرف</MenuItem><MenuItem value="date">التاريخ</MenuItem><MenuItem value="amount">المبلغ</MenuItem></Select></FormControl>
          <FormControlLabel control={<Checkbox checked={includeUnresolved} onChange={(event) => setIncludeUnresolved(event.target.checked)} />} label="إظهار السجلات غير المحسومة" />
          {noTransactionColumns && <Typography color="error">اختر قالبًا أو أعمدة لعرض الحركات.</Typography>}
          <Button fullWidth variant="contained" size="large" onClick={run} disabled={loading || invalidConfiguration} startIcon={<IconPlayerPlay size={18} />}>{loading ? 'جاري بناء التقرير...' : 'تشغيل المعاينة'}</Button>
          {canExport && <>
            <FormControl fullWidth size="small">
              <InputLabel id="report-builder-export-layout">عرض Excel</InputLabel>
              <Select labelId="report-builder-export-layout" label="عرض Excel" value={presentation} onChange={(event) => setPresentation(event.target.value as EmgReportBuilderPresentation)}>
                <MenuItem value="custom">مخصص</MenuItem>
                <MenuItem value="detailed">تفصيلي بقالب المشروع</MenuItem>
                <MenuItem value="consolidated">النموذج المجمع</MenuItem>
              </Select>
            </FormControl>
            <Button fullWidth variant="outlined" onClick={exportExcel} disabled={exporting || invalidConfiguration} aria-busy={exporting} startIcon={<IconDownload size={18} />}>
              {exporting ? 'جاري تجهيز Excel...' : 'تصدير Excel'}
            </Button>
          </>}
        </Stack>
      </MainCard></Grid>
      <Grid size={{ xs: 12, lg: 8 }}><Stack spacing={2.25}>
        <MainCard title="التصنيفات" secondary={<Typography variant="caption" color="text.secondary">اختيار متعدد</Typography>}><Stack direction="row" flexWrap="wrap" gap={0.75}>{categories.map(([value, label]) => <Chip key={value} label={label} onClick={() => setSelectedCategories((items) => items.includes(value) ? items.filter((item) => item !== value) : [...items, value])} color={selectedCategories.includes(value) ? 'primary' : 'default'} variant={selectedCategories.includes(value) ? 'filled' : 'outlined'} />)}</Stack><Typography variant="subtitle1" fontWeight={700} sx={{ mt: 2, mb: 1 }}>الأبعاد المالية</Typography><Stack direction="row" flexWrap="wrap" gap={0.75}>{dimensions.map(([value, label]) => <Chip key={value} label={label} onClick={() => setSelectedDimensions((items) => items.includes(value) ? items.filter((item) => item !== value) : [...items, value])} color={selectedDimensions.includes(value) ? 'secondary' : 'default'} variant={selectedDimensions.includes(value) ? 'filled' : 'outlined'} />)}</Stack></MainCard>
        <MainCard title="التجميع والأعمدة" secondary={<Typography variant="caption" color="text.secondary">كل اختيار مستقل وقابل للدمج</Typography>}><Grid container spacing={2}><Grid size={{ xs: 12, md: 5 }}><Typography variant="subtitle1" fontWeight={700} sx={{ mb: 1 }}>التجميع</Typography>{groupOptions.map(([value, label]) => <FormControlLabel key={value} control={<Checkbox size="small" checked={selectedGroups.includes(value)} onChange={() => setSelectedGroups((items) => items.includes(value) ? items.filter((item) => item !== value) : [...items, value])} />} label={label} />)}</Grid><Grid size={{ xs: 12, md: 7 }}><Typography variant="subtitle1" fontWeight={700} sx={{ mb: 1 }}>الأعمدة وترتيبها</Typography><Stack direction="row" flexWrap="wrap" gap={0.75}>{selectedColumns.map((column, index) => <Chip key={column} label={`${columnOptions.find(([value]) => value === column)?.[1] || column} ${index ? '↑' : ''}`} onDelete={() => setSelectedColumns((items) => items.filter((item) => item !== column))} onClick={() => moveColumn(index, -1)} variant="outlined" />)}</Stack><Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>اضغط على العمود لتحريكه لأعلى، واحذفه لإخفائه.</Typography></Grid></Grid></MainCard>
      </Stack></Grid>
    </Grid>
    </Collapse>
    {result && (
      <Box sx={{ mt: 2.5 }}>
        <Alert severity={result.isComplete ? 'success' : 'warning'} sx={{ mb: 2, borderRadius: 2 }}>
          {result.isComplete
            ? 'المعاينة مكتملة للتصنيفات المحددة.'
            : 'المعاينة تحتوي على سجلات غير محسومة أو أبعاد غير مكتملة؛ لا يوجد إجمالي نهائي معتمد.'}
        </Alert>
        {result.warnings.map((warning) => (
          <Alert key={warning} severity="info" icon={<IconInfoCircle />} sx={{ mb: 0.75, borderRadius: 2 }}>
            {warning}
          </Alert>
        ))}

        <MainCard content={false} sx={{ mt: 2, borderRadius: 3, overflow: 'hidden' }}>
          {/* Tabs Header */}
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            justifyContent="space-between"
            alignItems={{ xs: 'stretch', sm: 'center' }}
            sx={{ px: 2, borderBottom: 1, borderColor: 'divider' }}
          >
            <Tabs value={tab} onChange={(_, value) => setTab(value)}>
              <Tab label={`الإجماليات (${filteredSummary.length}${filteredSummary.length !== resultSummary.length ? ` / ${resultSummary.length}` : ''})`} />
              <Tab label={`الحركات (${filteredTransactions.length}${filteredTransactions.length !== resultTransactions.length ? ` / ${resultTransactions.length}` : ''})`} />
            </Tabs>
            {hasActiveTableFilters && (
              <Button
                size="small"
                color="inherit"
                startIcon={<IconFilterOff size={15} />}
                onClick={resetTableFilters}
                sx={{ fontWeight: 700, my: { xs: 1, sm: 0 } }}
              >
                مسح فلاتر الجدول
              </Button>
            )}
          </Stack>

          {/* Instant Filter Toolbar */}
          <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'action.hover' }}>
            <Stack
              direction={{ xs: 'column', lg: 'row' }}
              spacing={1.5}
              alignItems={{ xs: 'stretch', lg: 'center' }}
              justifyContent="space-between"
            >
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', sm: 'center' }} flexWrap="wrap" useFlexGap>
                <TextField
                  size="small"
                  placeholder={tab === 0 ? 'ابحث في المجموعات...' : 'ابحث بالمشروع، الطرف، التصنيف، أو المصدر...'}
                  value={tableSearch}
                  onChange={(e) => setTableSearch(e.target.value)}
                  sx={{ minWidth: { xs: '100%', sm: 260 }, bgcolor: 'background.paper', borderRadius: 1 }}
                  InputProps={{
                    startAdornment: (
                      <InputAdornment position="start">
                        <IconSearch size={17} />
                      </InputAdornment>
                    ),
                    endAdornment: tableSearch ? (
                      <InputAdornment position="end">
                        <IconButton size="small" onClick={() => setTableSearch('')} aria-label="مسح البحث">
                          <IconX size={14} />
                        </IconButton>
                      </InputAdornment>
                    ) : undefined
                  }}
                />

                {tab === 1 && distinctResultProjects.length > 1 && (
                  <FormControl size="small" sx={{ minWidth: 190, bgcolor: 'background.paper', borderRadius: 1 }}>
                    <InputLabel>المشروع</InputLabel>
                    <Select
                      value={tableProjectFilter}
                      label="المشروع"
                      onChange={(e) => setTableProjectFilter(e.target.value)}
                    >
                      <MenuItem value="all">كل المشاريع ({distinctResultProjects.length})</MenuItem>
                      {distinctResultProjects.map(([code, label]) => (
                        <MenuItem key={code} value={code}>
                          {label}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}

                {tab === 1 && distinctResultCategories.length > 1 && (
                  <FormControl size="small" sx={{ minWidth: 170, bgcolor: 'background.paper', borderRadius: 1 }}>
                    <InputLabel>التصنيف</InputLabel>
                    <Select
                      value={tableCategoryFilter}
                      label="التصنيف"
                      onChange={(e) => setTableCategoryFilter(e.target.value)}
                    >
                      <MenuItem value="all">كل التصنيفات ({distinctResultCategories.length})</MenuItem>
                      {distinctResultCategories.map((cat) => (
                        <MenuItem key={cat} value={cat}>
                          {categoryLabelMap[cat] || cat}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              </Stack>

              {/* Quick Filter Chips */}
              <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
                <Chip
                  clickable
                  size="small"
                  label="الكل"
                  color={tableQuickFilter === 'all' ? 'primary' : 'default'}
                  variant={tableQuickFilter === 'all' ? 'filled' : 'outlined'}
                  onClick={() => setTableQuickFilter('all')}
                  sx={{ fontWeight: 700 }}
                />
                <Chip
                  clickable
                  size="small"
                  label="غير الصفرية فقط"
                  color={tableQuickFilter === 'nonzero' ? 'secondary' : 'default'}
                  variant={tableQuickFilter === 'nonzero' ? 'filled' : 'outlined'}
                  onClick={() => setTableQuickFilter('nonzero')}
                  sx={{ fontWeight: 700 }}
                />
                <Chip
                  clickable
                  size="small"
                  label="غير محسوم / مراجعة"
                  color={tableQuickFilter === 'review' ? 'warning' : 'default'}
                  variant={tableQuickFilter === 'review' ? 'filled' : 'outlined'}
                  onClick={() => setTableQuickFilter('review')}
                  sx={{ fontWeight: 700 }}
                />
              </Stack>
            </Stack>
          </Box>

          {/* Tab 0: Summary Table */}
          {tab === 0 && (
            <TableContainer sx={{ maxHeight: 560, overflowX: 'auto' }}>
              <Table stickyHeader size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 800 }}>المجموعة</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800 }}>المستورد</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800 }}>الحالي</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800 }}>غير محسوم</TableCell>
                    <TableCell align="center" sx={{ fontWeight: 800 }}>السجلات</TableCell>
                    <TableCell align="center" sx={{ fontWeight: 800 }}>الحالة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {paginatedSummary.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                        <Typography color="text.secondary" fontWeight={600}>
                          لا توجد مجموعات مطابقة للفلاتر الحالية.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    paginatedSummary.map((row) => (
                      <TableRow key={row.groupKey} hover>
                        <TableCell>
                          <Typography fontWeight={700}>{localizeGroupLabel(row.groupLabel)}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {localizeGroupLabel(row.groupKey)}
                          </Typography>
                        </TableCell>
                        <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', color: row.importedAmount ? 'text.primary' : 'text.disabled' }}>
                          {row.importedAmount ? formatMoney(row.importedAmount) : '—'}
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums', color: row.currentAmount ? 'text.primary' : 'text.disabled' }}>
                          {row.currentAmount ? formatMoney(row.currentAmount) : '—'}
                        </TableCell>
                        <TableCell align="right">
                          {row.unresolvedAmount ? <Chip size="small" color="warning" label={formatMoney(row.unresolvedAmount)} /> : '—'}
                        </TableCell>
                        <TableCell align="center" sx={{ fontWeight: 700 }}>
                          {row.recordCount}
                        </TableCell>
                        <TableCell align="center">
                          <Chip size="small" color={row.isComplete ? 'success' : 'warning'} label={row.isComplete ? 'سليم' : 'مراجعة'} sx={{ fontWeight: 700 }} />
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {/* Tab 1: Transactions Table */}
          {tab === 1 && (
            <TableContainer sx={{ maxHeight: 560, overflowX: 'auto' }}>
              <Table stickyHeader size="small" sx={{ minWidth: 1100 }}>
                <TableHead>
                  <TableRow>
                    {selectedColumns.map((column) => (
                      <TableCell
                        key={column}
                        align={column.includes('Amount') ? 'right' : 'left'}
                        sx={{ fontWeight: 800, whiteSpace: 'nowrap' }}
                      >
                        {columnOptions.find(([value]) => value === column)?.[1] || column}
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {paginatedTransactions.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={selectedColumns.length} align="center" sx={{ py: 4 }}>
                        <Typography color="text.secondary" fontWeight={600}>
                          لا توجد حركات مطابقة للفلاتر الحالية.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    paginatedTransactions.map((row) => (
                      <TableRow
                        key={`${row.entityType}-${row.entityId}`}
                        hover
                        onClick={() => setSelectedTransaction(row)}
                        sx={{ cursor: 'pointer' }}
                      >
                        {selectedColumns.map((column) => {
                          const isMoneyCol = column === 'importedAmount' || column === 'currentAmount' || column === 'unresolvedAmount';
                          const rawNum =
                            column === 'importedAmount'
                              ? row.importedAmount
                              : column === 'currentAmount'
                                ? row.currentAmount
                                : column === 'unresolvedAmount'
                                  ? row.unresolvedAmount
                                  : null;
                          return (
                            <TableCell
                              key={column}
                              align={isMoneyCol ? 'right' : 'left'}
                              sx={{
                                fontVariantNumeric: isMoneyCol ? 'tabular-nums' : undefined,
                                fontWeight: column === 'currentAmount' && rawNum ? 700 : undefined,
                                color: isMoneyCol && !rawNum ? 'text.disabled' : undefined
                              }}
                            >
                              {column === 'category' ? (
                                <Stack direction="row" spacing={0.75} alignItems="center">
                                  <Chip size="small" variant="outlined" label={transactionValue(row, column)} sx={{ fontWeight: 600 }} />
                                  {row.isUnresolved && <Chip size="small" color="warning" label="غير محسوم" sx={{ fontWeight: 700 }} />}
                                </Stack>
                              ) : isMoneyCol && !rawNum ? (
                                '—'
                              ) : (
                                transactionValue(row, column)
                              )}
                            </TableCell>
                          );
                        })}
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {/* Pagination Footer */}
          <TablePagination
            component="div"
            count={activeTotalCount}
            page={Math.min(page, Math.max(0, Math.ceil(activeTotalCount / rowsPerPage) - 1))}
            onPageChange={(_, newPage) => setPage(newPage)}
            rowsPerPage={rowsPerPage}
            onRowsPerPageChange={(e) => {
              setRowsPerPage(parseInt(e.target.value, 10));
              setPage(0);
            }}
            rowsPerPageOptions={[10, 25, 50, 100, 250]}
            labelRowsPerPage="عدد الصفوف:"
            labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`}
            showFirstButton
            showLastButton
            sx={{ borderTop: 1, borderColor: 'divider' }}
          />
        </MainCard>
      </Box>
    )}
    <Details item={selectedTransaction} onClose={() => setSelectedTransaction(null)} />
  </Box>;
}
