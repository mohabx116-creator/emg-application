import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Pagination,
  Select,
  Skeleton,
  Stack,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import { Link, useParams } from 'react-router-dom';
import { IconArrowRight, IconCalendar, IconCash, IconFileSpreadsheet, IconPlus, IconReceipt, IconRefresh, IconTrash, IconX } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { createEmgAdvance, deleteEmgTransaction, EmgAdvance, EmgAdvanceLedger, getEmgAdvanceLedger, uploadEmgAttachment } from 'api/emg';
import useAuth from 'hooks/useAuth';
import { invalidateEmgCache, useEmgProjectsCache } from 'hooks/useEmgData';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import { formatEmgMoney, getLocalIsoDate } from 'utils/emgFormatting';
import FinancialSummary from '../FinancialSummary';

const MAX_ATTACHMENT_SIZE = 20 * 1024 * 1024;
const ALLOWED_ATTACHMENT_EXTENSIONS = new Set(['pdf', 'jpg', 'jpeg', 'png', 'webp', 'xlsx', 'xls', 'doc', 'docx']);

const getAttachmentError = (file: File) => {
  const extension = file.name.split('.').pop()?.toLowerCase() || '';
  if (!ALLOWED_ATTACHMENT_EXTENSIONS.has(extension)) return 'نوع المرفق غير مسموح. ارفع PDF أو صورة أو ملف Excel أو Word.';
  if (file.size > MAX_ATTACHMENT_SIZE) return 'حجم المرفق يتجاوز 20 ميجابايت.';
  return null;
};

type HistoricalRecord = EmgAdvanceLedger['historical'][number];

function recordKind(row: HistoricalRecord) {
  if (row.basis === 'conflicting_ownership') return 'ملكية غير محسومة';
  if (row.basis === 'verified_zero') return 'دفعات صفرية مثبتة';
  if (row.fundsProject && row.isAllocation) return 'دفعة وتوزيع';
  if (row.fundsProject) return 'تمويل مثبت';
  if (row.isAllocation) return 'توزيع على المشروع';
  return row.basis === 'transfer_breakdown' ? 'تفصيل تحويل' : 'دليل مرجعي';
}

export default function EmgAdvances() {
  const [selectedRecord, setSelectedRecord] = useState<HistoricalRecord | null>(null);
  const { projectId: routeProjectId } = useParams();
  const lockedProjectId = routeProjectId ? Number(routeProjectId) : null;
  const theme = useTheme();
  const downSM = useMediaQuery(theme.breakpoints.down('sm'));
  const { user } = useAuth();
  const { setActiveProjectId } = useActiveProject();
  const { data: projects = [] } = useEmgProjectsCache(true);

  const canCreate = !!lockedProjectId && user?.permissions?.includes('advances.create');
  const [ledger, setLedger] = useState<EmgAdvanceLedger | null>(null);
  const [receiptsPage, setReceiptsPage] = useState(1);
  const [page, setPage] = useState(1);
  const requestNumber = useRef(0);
  const [showEvidence, setShowEvidence] = useState(false);
  const advances = ledger?.fundingEntries.flatMap(entry => entry.advance ? [entry.advance] : []) ?? [];
  const recognizedHistoricalReceipts = ledger?.fundingEntries.flatMap(entry => entry.historical ? [entry.historical] : []) ?? [];
  const directReceipts = ledger?.fundingEntries.flatMap(entry => entry.direct ? [entry.direct] : []) ?? [];
  const historicalAllocations = ledger?.allocationEntries.flatMap(entry => entry.historical ? [entry.historical] : []) ?? [];
  const ordinaryAllocations = ledger?.allocationEntries.flatMap(entry => entry.allocation ? [{ ...entry.allocation, currency: entry.currency }] : []) ?? [];
  const evidenceRows = ledger?.historical.filter(row => !row.fundsProject && !row.isAllocation) ?? [];
  const allocationAndReferenceRows = showEvidence ? evidenceRows : historicalAllocations;
  const [loading, setLoading] = useState(true);
  const [projectFilter, setProjectFilter] = useState<string>(lockedProjectId ? String(lockedProjectId) : '');
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [uploadingAttachment, setUploadingAttachment] = useState(false);
  const [message, setMessage] = useState<{ severity: 'success' | 'warning' | 'error'; text: string } | null>(null);
  const [form, setForm] = useState({
    projectId: lockedProjectId ? String(lockedProjectId) : '',
    clientName: '',
    payerName: '',
    amount: '',
    currency: 'EGP',
    paymentDate: getLocalIsoDate(),
    paymentMethod: 'تحويل',
    reference: '',
    notes: ''
  });
  const [attachment, setAttachment] = useState<File | null>(null);

  const activeProject = useMemo(
    () => (lockedProjectId ? projects.find((p) => p.id === lockedProjectId) || null : null),
    [projects, lockedProjectId]
  );

  useEffect(() => {
    if (lockedProjectId) {
      setProjectFilter(String(lockedProjectId));
      setActiveProjectId(lockedProjectId);
      setForm((prev) => ({
        ...prev,
        projectId: String(lockedProjectId),
        clientName: prev.clientName || activeProject?.clientName || ''
      }));
    }
  }, [lockedProjectId, activeProject?.clientName, setActiveProjectId]);

  const load = () => {
    const pid = lockedProjectId ?? (projectFilter ? Number(projectFilter) : undefined);
    const request = ++requestNumber.current;
    setLoading(true);
    setLedger(null);
    setReceiptsPage(1);
    setPage(1);
    getEmgAdvanceLedger(pid)
      .then(data => { if (requestNumber.current === request) setLedger(data); })
      .catch(() => { if (requestNumber.current === request) setMessage({ severity: 'error', text: 'تعذر تحميل الدفعات تحت الحساب.' }); })
      .finally(() => { if (requestNumber.current === request) setLoading(false); });
  };

  useEffect(() => {
    load();
    return () => { requestNumber.current++; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectFilter, lockedProjectId]);

  const handleProjectSelect = (value: string) => {
    const selected = projects.find((p) => String(p.id) === value);
    setForm((prev) => ({
      ...prev,
      projectId: value,
      clientName: selected?.clientName || prev.clientName
    }));
  };

  const openDialog = () => {
    const defaultPid = lockedProjectId ? String(lockedProjectId) : projectFilter || form.projectId;
    const selected = projects.find((p) => String(p.id) === defaultPid);
    setForm((prev) => ({
      ...prev,
      projectId: defaultPid,
      clientName: prev.clientName || selected?.clientName || '',
      paymentDate: prev.paymentDate || getLocalIsoDate()
    }));
    setOpen(true);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const amount = Number(form.amount);
    if (!form.projectId || !Number.isFinite(amount) || amount <= 0) {
      setMessage({ severity: 'error', text: 'اختر المشروع وأدخل قيمة دفعة أكبر من صفر.' });
      return;
    }
    setSaving(true);
    try {
      const created = await createEmgAdvance({ ...form, projectId: Number(form.projectId), amount });
      let severity: 'success' | 'warning' | 'error' = 'success';
      let text = 'تم تسجيل الدفعة تحت الحساب وترحيلها بنجاح.';
      if (attachment) {
        setUploadingAttachment(true);
        try {
          await uploadEmgAttachment('advance-payment', created.id, attachment);
          text = 'تم تسجيل الدفعة ورفع المرفق بنجاح.';
        } catch {
          severity = 'warning';
          text = `تم تسجيل الدفعة رقم ${created.id}، لكن تعذر رفع المرفق. افتح تفاصيل الدفعة لإعادة المحاولة.`;
        } finally {
          setUploadingAttachment(false);
        }
      }
      setMessage({ severity, text });
      setForm((previous) => ({ ...previous, amount: '', reference: '', notes: '' }));
      setAttachment(null);
      setOpen(false);
      await Promise.all([invalidateEmgCache('emg/advances'), invalidateEmgCache('emg/projects')]);
      load();
    } catch (e: any) {
      setMessage({ severity: 'error', text: e?.message || 'تعذر تسجيل الدفعة.' });
    } finally {
      setSaving(false);
    }
  };

  const remove = async (advance: EmgAdvance) => {
    if (!window.confirm('سيتم حذف الدفعة نهائيًا وإزالة آثارها غير المرتبطة. هل تريد المتابعة؟')) return;
    try {
      await deleteEmgTransaction('advance-payment', advance.id);
      setMessage({ severity: 'success', text: 'تم حذف الدفعة نهائيًا.' });
      await Promise.all([invalidateEmgCache('emg/advances'), invalidateEmgCache('emg/projects')]);
      load();
    } catch (e: any) {
      setMessage({ severity: 'error', text: e?.message || 'تعذر حذف الدفعة. قد توجد تخصيصات مرتبطة بها.' });
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      {lockedProjectId && (
        <Breadcrumbs sx={{ mb: 1.5 }}>
          <Typography component={Link} to="/emg/projects" variant="body2" color="text.secondary" sx={{ textDecoration: 'none', fontWeight: 700 }}>
            المشروعات
          </Typography>
          <Typography
            component={Link}
            to={`/emg/projects/${lockedProjectId}`}
            variant="body2"
            color="text.secondary"
            sx={{ textDecoration: 'none', fontWeight: 700 }}
          >
            {activeProject?.name || `مشروع #${lockedProjectId}`}
          </Typography>
          <Typography variant="body2" color="primary" sx={{ fontWeight: 700 }}>
            الدفعات تحت الحساب
          </Typography>
        </Breadcrumbs>
      )}

      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={2} sx={{ mb: 3 }}>
        <Box>
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            الدفعات تحت الحساب
          </Typography>
          <Typography color="text.secondary">
            {activeProject ? `${activeProject.name} — متابعة الدفعات والتخصيصات` : 'تسجيل الدفعات، متابعة تخصيصها، ومعرفة المتاح أو المستحق على الدافع'}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          {canCreate && (
            <Button variant="contained" startIcon={<IconPlus size={18} />} onClick={openDialog} sx={{ borderRadius: 2, fontWeight: 700 }}>
              تسجيل دفعة
            </Button>
          )}
          {lockedProjectId && (
            <Button component={Link} to={`/emg/projects/${lockedProjectId}`} variant="outlined" color="inherit" endIcon={<IconArrowRight size={17} />} sx={{ borderRadius: 2, fontWeight: 700 }}>
              تفاصيل المشروع
            </Button>
          )}
        </Stack>
      </Stack>

      {message && (
        <Alert severity={message.severity} onClose={() => setMessage(null)} sx={{ mb: 2, borderRadius: 2 }}>
          {message.text}
        </Alert>
      )}

      {loading && !ledger ? (
        <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
          {[0, 1, 2].map((i) => (
            <Grid key={i} size={{ xs: 12, sm: 4 }}>
              <Skeleton variant="rounded" height={106} sx={{ borderRadius: 2.5 }} />
            </Grid>
          ))}
        </Grid>
      ) : (
        (ledger?.totals ?? []).map((t) => (
          <Box key={t.currency} sx={{ mb: 2.5 }}>
            <FinancialSummary
              title={ledger && ledger.totals.length > 1 ? `الملخص المالي (${t.currency})` : undefined}
              subtitle={
                t.allocated != null
                  ? `إجمالي المخصص على البنود: ${formatEmgMoney(t.allocated, t.currency === 'EGP' ? 'ج.م' : t.currency)}`
                  : undefined
              }
              currency={t.currency}
              totalWorks={t.works}
              totalPayments={t.payments}
              cashBalance={t.cashBalance}
            />
          </Box>
        ))
      )}
      {(lockedProjectId || projectFilter) &&
        ledger?.warnings.map((w) => (
          <Alert severity="warning" key={w} sx={{ mb: 1.5, borderRadius: 2 }}>
            {w}
          </Alert>
        ))}

      <MainCard
        title={`قائمة الدفعات المقبوضة (${advances.length + recognizedHistoricalReceipts.length + directReceipts.length})`}
        sx={{ borderRadius: 3 }}
        secondary={
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            {!lockedProjectId && (
              <FormControl size="small" sx={{ minWidth: { xs: 160, sm: 220 } }}>
                <InputLabel>المشروع</InputLabel>
                <Select value={projectFilter} label="المشروع" onChange={(e) => setProjectFilter(e.target.value)}>
                  <MenuItem value="">كل المشروعات</MenuItem>
                  {projects.map((p) => (
                    <MenuItem key={p.id} value={String(p.id)}>
                      {p.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            <Button onClick={load} startIcon={<IconRefresh size={16} />} variant="outlined" size="small">
              تحديث
            </Button>
          </Stack>
        }
      >
        {loading ? (
          <Grid container spacing={2}>
            {[0, 1, 2].map((i) => (
              <Grid key={i} size={{ xs: 12, md: 6, xl: 4 }}>
                <Skeleton variant="rounded" height={190} sx={{ borderRadius: 3 }} />
              </Grid>
            ))}
          </Grid>
        ) : (
          <Grid container spacing={2}>
            {directReceipts.map((row) => {
              const currencyLabel = row.currency === 'EGP' ? 'ج.م' : row.currency;
              const accentColor = theme.vars.palette.secondary.main;
              return (
                <Grid key={`direct-${row.source}-${row.id}`} size={{ xs: 12, md: 6, xl: 4 }}>
                  <Card
                    variant="outlined"
                    sx={{
                      height: '100%',
                      borderRadius: 3,
                      bgcolor: 'background.paper',
                      borderColor: 'divider',
                      borderTop: '3px solid',
                      borderTopColor: accentColor,
                      boxShadow: theme.shadows[1]
                    }}
                  >
                    <CardContent sx={{ p: 2, '&:last-child': { pb: 1.75 }, display: 'flex', flexDirection: 'column', height: '100%', gap: 1.5 }}>
                      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
                        <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                          <Box
                            sx={{
                              width: 38,
                              height: 38,
                              borderRadius: 2,
                              display: 'grid',
                              placeItems: 'center',
                              bgcolor: 'rgba(94, 53, 177, 0.12)',
                              color: accentColor,
                              flexShrink: 0
                            }}
                          >
                            <IconCash size={20} />
                          </Box>
                          <Box sx={{ minWidth: 0 }}>
                            <Typography variant="subtitle1" sx={{ fontWeight: 800, lineHeight: 1.3 }} noWrap>
                              {row.party || row.projectName}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                              {row.source} · {row.projectName}
                            </Typography>
                          </Box>
                        </Stack>
                        <Chip size="small" label="سداد مباشر من المالك" color="secondary" variant="filled" sx={{ fontWeight: 700, height: 24, flexShrink: 0 }} />
                      </Stack>

                      <Box
                        sx={{
                          px: 1.5,
                          py: 1.1,
                          borderRadius: 2,
                          bgcolor: 'action.hover',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 1
                        }}
                      >
                        <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                          قيمة السداد المباشر
                        </Typography>
                        <Typography variant="h4" sx={{ fontWeight: 900, color: accentColor, fontVariantNumeric: 'tabular-nums' }}>
                          {formatEmgMoney(row.amount, currencyLabel)}
                        </Typography>
                      </Box>

                      <Box sx={{ mt: 'auto' }}>
                        <Divider sx={{ mb: 1.1 }} />
                        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                          <Stack direction="row" spacing={0.5} alignItems="center" color="text.secondary">
                            <IconCalendar size={14} />
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                              {row.date ? new Date(row.date).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'UTC' }) : 'التاريخ غير مدون'}
                            </Typography>
                          </Stack>
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                            {row.reference || `#${row.id}`}
                          </Typography>
                        </Stack>
                      </Box>
                    </CardContent>
                  </Card>
                </Grid>
              );
            })}
            {advances.map((advance) => {
              const currencyLabel = advance.currency === 'EGP' ? 'ج.م' : advance.currency;
              const isOverAllocated = advance.dueFromPayer > 0;
              const accentColor = isOverAllocated ? theme.vars.palette.error.main : theme.vars.palette.secondary.main;

              return (
                <Grid key={advance.id} size={{ xs: 12, md: 6, xl: 4 }}>
                  <Card
                    component={Link}
                    to={`/emg/advances/${advance.id}`}
                    variant="outlined"
                    sx={{
                      textDecoration: 'none',
                      color: 'inherit',
                      display: 'flex',
                      flexDirection: 'column',
                      height: '100%',
                      borderRadius: 3,
                      bgcolor: 'background.paper',
                      borderColor: 'divider',
                      borderTop: '3px solid',
                      borderTopColor: accentColor,
                      boxShadow: theme.shadows[1],
                      transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
                      '&:hover': {
                        transform: 'translateY(-2px)',
                        boxShadow: theme.shadows[4],
                        borderColor: accentColor
                      }
                    }}
                  >
                    <CardContent sx={{ p: 2, '&:last-child': { pb: 1.75 }, display: 'flex', flexDirection: 'column', flex: 1, gap: 1.5 }}>
                      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                        <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                          <Box
                            sx={{
                              width: 38,
                              height: 38,
                              borderRadius: 2,
                              display: 'grid',
                              placeItems: 'center',
                              bgcolor: isOverAllocated ? 'rgba(211, 47, 47, 0.12)' : 'rgba(94, 53, 177, 0.12)',
                              color: accentColor,
                              flexShrink: 0
                            }}
                          >
                            <IconCash size={20} />
                          </Box>
                          <Box sx={{ minWidth: 0 }}>
                            <Typography variant="subtitle1" sx={{ fontWeight: 800, lineHeight: 1.3 }} noWrap>
                              {advance.projectName}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 600 }} noWrap>
                              العميل: {advance.clientName || 'غير محدد'} · الدافع: {advance.payerName || 'غير محدد'}
                            </Typography>
                          </Box>
                        </Stack>
                        <Stack direction="row" spacing={0.5} alignItems="center" sx={{ flexShrink: 0 }}>
                          <Chip
                            size="small"
                            label={advance.balanceStatus}
                            color={isOverAllocated ? 'error' : 'success'}
                            sx={{ fontWeight: 700, height: 24 }}
                          />
                          <IconButton
                            aria-label="حذف الدفعة"
                            size="small"
                            color="error"
                            onClick={(event) => {
                              event.preventDefault();
                              event.stopPropagation();
                              void remove(advance);
                            }}
                          >
                            <IconTrash size={17} />
                          </IconButton>
                        </Stack>
                      </Stack>

                      <Box
                        sx={{
                          px: 1.5,
                          py: 1.1,
                          borderRadius: 2,
                          bgcolor: 'action.hover',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 1
                        }}
                      >
                        <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                          قيمة الدفعة
                        </Typography>
                        <Typography
                          variant="h4"
                          sx={{
                            fontWeight: 900,
                            color: accentColor,
                            fontVariantNumeric: 'tabular-nums'
                          }}
                        >
                          {formatEmgMoney(advance.amount, currencyLabel)}
                        </Typography>
                      </Box>

                      <Stack spacing={0.75}>
                        <Stack direction="row" justifyContent="space-between" alignItems="center">
                          <Typography variant="body2" color="text.secondary">
                            المخصص
                          </Typography>
                          <Typography variant="body2" fontWeight={700} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                            {formatEmgMoney(advance.allocatedAmount, currencyLabel)}
                          </Typography>
                        </Stack>
                        <Stack direction="row" justifyContent="space-between" alignItems="center">
                          <Typography variant="body2" color="text.secondary">
                            غير مخصص من الإيصال
                          </Typography>
                          <Typography variant="body2" fontWeight={700} color="primary.main" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                            {formatEmgMoney(advance.unallocatedAmount, currencyLabel)}
                          </Typography>
                        </Stack>
                        <Stack direction="row" justifyContent="space-between" alignItems="center">
                          <Typography variant="body2" color="text.secondary">
                            زيادة التخصيص
                          </Typography>
                          <Typography
                            variant="body2"
                            fontWeight={700}
                            color={isOverAllocated ? 'error.main' : 'text.secondary'}
                            sx={{ fontVariantNumeric: 'tabular-nums' }}
                          >
                            {formatEmgMoney(advance.dueFromPayer, currencyLabel)}
                          </Typography>
                        </Stack>
                      </Stack>

                      <Box sx={{ mt: 'auto' }}>
                        <Divider sx={{ mb: 1.1 }} />
                        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                          <Stack direction="row" spacing={0.5} alignItems="center" color="text.secondary">
                            <IconCalendar size={14} />
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                              {advance.paymentDate ? new Date(advance.paymentDate).toLocaleDateString('ar-EG-u-nu-latn') : 'التاريخ غير مدون'}
                            </Typography>
                          </Stack>
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                            {advance.paymentMethod} · {advance.status}
                          </Typography>
                        </Stack>
                      </Box>
                    </CardContent>
                  </Card>
                </Grid>
              );
            })}
            {recognizedHistoricalReceipts.slice((receiptsPage - 1) * 24, receiptsPage * 24).map((row) => {
              const isWarning = row.basis === 'conflicting_ownership';
              const accentColor = isWarning ? theme.vars.palette.warning.main : theme.vars.palette.secondary.main;
              const chipColor: 'warning' | 'secondary' = isWarning ? 'warning' : 'secondary';

              return (
                <Grid key={`receipt-hist-${row.id}`} size={{ xs: 12, md: 6, xl: 4 }}>
                  <Card
                    variant="outlined"
                    sx={{
                      height: '100%',
                      borderRadius: 3,
                      bgcolor: 'background.paper',
                      borderColor: 'divider',
                      borderTop: '3px solid',
                      borderTopColor: accentColor,
                      boxShadow: theme.shadows[1],
                      transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
                      '&:hover': {
                        transform: 'translateY(-2px)',
                        boxShadow: theme.shadows[4],
                        borderColor: accentColor
                      }
                    }}
                  >
                    <CardActionArea
                      onClick={() => setSelectedRecord(row)}
                      sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'space-between' }}
                    >
                      <CardContent sx={{ width: '100%', p: 2, '&:last-child': { pb: 1.75 }, display: 'flex', flexDirection: 'column', flex: 1, gap: 1.5 }}>
                        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
                          <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                            <Box
                              sx={{
                                width: 38,
                                height: 38,
                                borderRadius: 2,
                                display: 'grid',
                                placeItems: 'center',
                                bgcolor: isWarning ? 'rgba(237, 108, 2, 0.12)' : 'rgba(94, 53, 177, 0.12)',
                                color: accentColor,
                                flexShrink: 0
                              }}
                            >
                              <IconCash size={20} />
                            </Box>
                            <Box sx={{ minWidth: 0 }}>
                              <Typography variant="subtitle1" sx={{ fontWeight: 800, lineHeight: 1.3 }} noWrap>
                                {row.category || row.sheet}
                              </Typography>
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                                مشروع: {row.projectName}
                              </Typography>
                            </Box>
                          </Stack>
                          <Chip
                            size="small"
                            label={recordKind(row)}
                            color={chipColor}
                            variant="filled"
                            sx={{ fontWeight: 700, height: 24, flexShrink: 0 }}
                          />
                        </Stack>

                        <Box
                          sx={{
                            px: 1.5,
                            py: 1.1,
                            borderRadius: 2,
                            bgcolor: 'action.hover',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            gap: 1
                          }}
                        >
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                            القيمة المسجلة
                          </Typography>
                          <Typography
                            variant="h4"
                            sx={{
                              fontWeight: 900,
                              color: accentColor,
                              fontVariantNumeric: 'tabular-nums'
                            }}
                          >
                            {formatEmgMoney(row.amount, row.currency === 'EGP' ? 'ج.م' : row.currency)}
                          </Typography>
                        </Box>

                        <Typography
                          variant="body2"
                          color="text.primary"
                          sx={{
                            minHeight: '2.6em',
                            lineHeight: 1.45,
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            overflow: 'hidden'
                          }}
                        >
                          {row.description || 'بدون ملاحظات إضافية في المصدر'}
                        </Typography>

                        <Box sx={{ mt: 'auto' }}>
                          <Divider sx={{ mb: 1.1 }} />
                          <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                            <Stack direction="row" spacing={0.5} alignItems="center" color="text.secondary">
                              <IconCalendar size={14} />
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                                {row.date ? new Date(row.date).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'UTC' }) : 'التاريخ غير مدون'}
                              </Typography>
                            </Stack>
                            <Tooltip title={`${row.workbook} · ${row.sheet}!${row.cell}`}>
                              <Stack
                                direction="row"
                                spacing={0.5}
                                alignItems="center"
                                sx={{
                                  px: 0.9,
                                  py: 0.25,
                                  borderRadius: 1.5,
                                  bgcolor: 'action.hover',
                                  color: 'text.secondary'
                                }}
                              >
                                <IconFileSpreadsheet size={13} />
                                <Typography variant="caption" sx={{ fontFamily: 'monospace', fontWeight: 700, fontSize: '0.72rem' }}>
                                  {row.cell}
                                </Typography>
                              </Stack>
                            </Tooltip>
                          </Stack>
                        </Box>
                      </CardContent>
                    </CardActionArea>
                  </Card>
                </Grid>
              );
            })}
          </Grid>
        )}
        {!loading && recognizedHistoricalReceipts.length > 24 && (
          <Stack alignItems="center" sx={{ mt: 2.5 }}>
            <Pagination
              color="secondary"
              shape="rounded"
              page={receiptsPage}
              count={Math.ceil(recognizedHistoricalReceipts.length / 24)}
              onChange={(_, nextPage) => setReceiptsPage(nextPage)}
            />
          </Stack>
        )}
        {!loading && ledger && !directReceipts.length && !advances.length && !recognizedHistoricalReceipts.length && (
          <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
            {allocationAndReferenceRows.length > 0
              ? 'لا توجد دفعات مقبوضة مثبتة لهذا الاختيار؛ راجع سجل التوزيعات والمراجع التاريخية أدناه.'
              : 'لا توجد دفعات مقبوضة مسجلة لهذا الاختيار.'}
          </Typography>
        )}
      </MainCard>

      {!loading && ledger && (historicalAllocations.length + ordinaryAllocations.length + evidenceRows.length > 0) && (
        <MainCard
          title={showEvidence ? 'مراجع المصدر' : 'التوزيعات على بنود المشروع'}
          secondary={evidenceRows.length > 0 && <Button size="small" onClick={() => { setShowEvidence(!showEvidence); setPage(1); }}>
            {showEvidence ? 'عرض التوزيعات' : `مراجع المصدر (${evidenceRows.length})`}
          </Button>}
          sx={{ borderRadius: 3, mt: 2.5 }}
        >
          <Grid container spacing={2}>
            {!showEvidence && ordinaryAllocations.map((row) => {
              const currencyLabel = row.currency === 'EGP' ? 'ج.م' : row.currency;
              const accentColor = theme.vars.palette.primary.main;
              return (
                <Grid key={`allocation-${row.id}`} size={{ xs: 12, md: 6, xl: 4 }}>
                  <Card
                    variant="outlined"
                    sx={{
                      height: '100%',
                      borderRadius: 3,
                      bgcolor: 'background.paper',
                      borderColor: 'divider',
                      borderTop: '3px solid',
                      borderTopColor: accentColor,
                      boxShadow: theme.shadows[1],
                      transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
                      '&:hover': {
                        transform: 'translateY(-2px)',
                        boxShadow: theme.shadows[4],
                        borderColor: accentColor
                      }
                    }}
                  >
                    <CardActionArea
                      component={Link}
                      to={`/emg/advances/${row.advancePaymentId}`}
                      sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'space-between' }}
                    >
                      <CardContent sx={{ width: '100%', p: 2, '&:last-child': { pb: 1.75 }, display: 'flex', flexDirection: 'column', flex: 1, gap: 1.5 }}>
                        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
                          <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                            <Box
                              sx={{
                                width: 38,
                                height: 38,
                                borderRadius: 2,
                                display: 'grid',
                                placeItems: 'center',
                                bgcolor: 'rgba(25, 118, 210, 0.12)',
                                color: accentColor,
                                flexShrink: 0
                              }}
                            >
                              <IconReceipt size={20} />
                            </Box>
                            <Box sx={{ minWidth: 0 }}>
                              <Typography variant="subtitle1" sx={{ fontWeight: 800, lineHeight: 1.3 }} noWrap>
                                {row.accountCategory}
                              </Typography>
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                                دفعة #{row.advancePaymentId}
                              </Typography>
                            </Box>
                          </Stack>
                          <Chip size="small" label="توزيع تشغيلي" color="primary" variant="outlined" sx={{ fontWeight: 700, height: 24, flexShrink: 0 }} />
                        </Stack>

                        <Box
                          sx={{
                            px: 1.5,
                            py: 1.1,
                            borderRadius: 2,
                            bgcolor: 'action.hover',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            gap: 1
                          }}
                        >
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                            قيمة التوزيع
                          </Typography>
                          <Typography variant="h4" sx={{ fontWeight: 900, color: accentColor, fontVariantNumeric: 'tabular-nums' }}>
                            {formatEmgMoney(row.amount, currencyLabel)}
                          </Typography>
                        </Box>

                        <Typography
                          variant="body2"
                          color="text.primary"
                          sx={{
                            minHeight: '2.6em',
                            lineHeight: 1.45,
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            overflow: 'hidden'
                          }}
                        >
                          {row.notes || 'توزيع مسجل على بند المشروع'}
                        </Typography>

                        <Box sx={{ mt: 'auto' }}>
                          <Divider sx={{ mb: 1.1 }} />
                          <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                            <Stack direction="row" spacing={0.5} alignItems="center" color="text.secondary">
                              <IconCalendar size={14} />
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                                {row.allocationDate ? new Date(row.allocationDate).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'UTC' }) : 'التاريخ غير مدون'}
                              </Typography>
                            </Stack>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                              {row.siteMovementId ? `حركة موقع #${row.siteMovementId}` : row.supplierPaymentId ? `دفعة مورد #${row.supplierPaymentId}` : `دفعة #${row.advancePaymentId}`}
                            </Typography>
                          </Stack>
                        </Box>
                      </CardContent>
                    </CardActionArea>
                  </Card>
                </Grid>
              );
            })}
            {allocationAndReferenceRows.slice((page - 1) * 24, page * 24).map((row) => {
              const isFunding = row.fundsProject && !row.isAllocation;
              const isWarning = row.basis === 'conflicting_ownership';
              const accentColor = isWarning
                ? theme.vars.palette.warning.main
                : isFunding
                  ? theme.vars.palette.secondary.main
                  : theme.vars.palette.primary.main;
              const chipColor: 'warning' | 'secondary' | 'primary' | 'default' = isWarning
                ? 'warning'
                : isFunding
                  ? 'secondary'
                  : row.isAllocation
                    ? 'primary'
                    : 'default';

              return (
                <Grid key={`source-${row.id}`} size={{ xs: 12, md: 6, xl: 4 }}>
                  <Card
                    variant="outlined"
                    sx={{
                      height: '100%',
                      borderRadius: 3,
                      bgcolor: 'background.paper',
                      borderColor: 'divider',
                      borderTop: '3px solid',
                      borderTopColor: accentColor,
                      boxShadow: theme.shadows[1],
                      transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
                      '&:hover': {
                        transform: 'translateY(-2px)',
                        boxShadow: theme.shadows[4],
                        borderColor: accentColor
                      }
                    }}
                  >
                    <CardActionArea
                      onClick={() => setSelectedRecord(row)}
                      sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'space-between' }}
                    >
                      <CardContent sx={{ width: '100%', p: 2, '&:last-child': { pb: 1.75 }, display: 'flex', flexDirection: 'column', flex: 1, gap: 1.5 }}>
                        {/* Header: Icon + Category/Project + Semantic Status Chip */}
                        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1.25}>
                          <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
                            <Box
                              sx={{
                                width: 38,
                                height: 38,
                                borderRadius: 2,
                                display: 'grid',
                                placeItems: 'center',
                                bgcolor: isWarning
                                  ? 'rgba(237, 108, 2, 0.12)'
                                  : isFunding
                                    ? 'rgba(94, 53, 177, 0.12)'
                                    : 'rgba(25, 118, 210, 0.12)',
                                color: accentColor,
                                flexShrink: 0
                              }}
                            >
                              {isFunding ? <IconCash size={20} /> : <IconReceipt size={20} />}
                            </Box>
                            <Box sx={{ minWidth: 0 }}>
                              <Typography variant="subtitle1" sx={{ fontWeight: 800, lineHeight: 1.3 }} noWrap>
                                {row.category || row.sheet}
                              </Typography>
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }} noWrap>
                                مشروع: {row.projectName}
                              </Typography>
                            </Box>
                          </Stack>
                          <Chip
                            size="small"
                            label={recordKind(row)}
                            color={chipColor}
                            variant={isFunding || isWarning ? 'filled' : 'outlined'}
                            sx={{ fontWeight: 700, height: 24, flexShrink: 0 }}
                          />
                        </Stack>

                        {/* Highlighted Amount Box */}
                        <Box
                          sx={{
                            px: 1.5,
                            py: 1.1,
                            borderRadius: 2,
                            bgcolor: 'action.hover',
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'center',
                            gap: 1
                          }}
                        >
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                            {row.isAllocation ? 'قيمة التوزيع' : 'القيمة المسجلة'}
                          </Typography>
                          <Typography
                            variant="h4"
                            sx={{
                              fontWeight: 900,
                              color: accentColor,
                              fontVariantNumeric: 'tabular-nums'
                            }}
                          >
                            {formatEmgMoney(row.amount, row.currency === 'EGP' ? 'ج.م' : row.currency)}
                          </Typography>
                        </Box>

                        {/* Description */}
                        <Typography
                          variant="body2"
                          color="text.primary"
                          sx={{
                            minHeight: '2.6em',
                            lineHeight: 1.45,
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            overflow: 'hidden'
                          }}
                        >
                          {row.description || 'بدون ملاحظات إضافية في المصدر'}
                        </Typography>

                        {/* Footer: Date + Excel Cell Reference Badge */}
                        <Box sx={{ mt: 'auto' }}>
                          <Divider sx={{ mb: 1.1 }} />
                          <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                            <Stack direction="row" spacing={0.5} alignItems="center" color="text.secondary">
                              <IconCalendar size={14} />
                              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                                {row.date ? new Date(row.date).toLocaleDateString('ar-EG-u-nu-latn', { timeZone: 'UTC' }) : 'التاريخ غير مدون'}
                              </Typography>
                            </Stack>
                            <Tooltip title={`${row.workbook} · ${row.sheet}!${row.cell}`}>
                              <Stack
                                direction="row"
                                spacing={0.5}
                                alignItems="center"
                                sx={{
                                  px: 0.9,
                                  py: 0.25,
                                  borderRadius: 1.5,
                                  bgcolor: 'action.hover',
                                  color: 'text.secondary'
                                }}
                              >
                                <IconFileSpreadsheet size={13} />
                                <Typography variant="caption" sx={{ fontFamily: 'monospace', fontWeight: 700, fontSize: '0.72rem' }}>
                                  {row.cell}
                                </Typography>
                              </Stack>
                            </Tooltip>
                          </Stack>
                        </Box>
                      </CardContent>
                    </CardActionArea>
                  </Card>
                </Grid>
              );
            })}
          </Grid>
          {allocationAndReferenceRows.length > 24 && (
            <Stack alignItems="center" sx={{ mt: 2.5 }}>
              <Pagination
                color="primary"
                shape="rounded"
                page={page}
                count={Math.ceil(allocationAndReferenceRows.length / 24)}
                onChange={(_, nextPage) => setPage(nextPage)}
              />
            </Stack>
          )}
        </MainCard>
      )}

      <Dialog open={selectedRecord != null} onClose={() => setSelectedRecord(null)} fullWidth maxWidth="sm" fullScreen={downSM}>
        {selectedRecord && <>
          <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <Typography variant="h3">{recordKind(selectedRecord)}</Typography>
            <IconButton aria-label="إغلاق" onClick={() => setSelectedRecord(null)}><IconX size={20} /></IconButton>
          </DialogTitle>
          <DialogContent dividers>
            <Stack spacing={1.5}>
              <Typography variant="h4">{selectedRecord.projectName}</Typography>
              <Typography>{formatEmgMoney(selectedRecord.amount, selectedRecord.currency)} · {selectedRecord.category}</Typography>
              {selectedRecord.description && <Typography>{selectedRecord.description}</Typography>}
              <Typography>{selectedRecord.date ? new Date(selectedRecord.date).toLocaleDateString('en-GB', { timeZone: 'UTC' }) : 'التاريخ غير مدون'}</Typography>
              <Typography color="text.secondary">{selectedRecord.workbook} · {selectedRecord.sheet}!{selectedRecord.cell}</Typography>
            </Stack>
          </DialogContent>
        </>}
      </Dialog>

      <Dialog open={open} onClose={() => !saving && setOpen(false)} fullWidth maxWidth="sm" fullScreen={downSM}>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="h3" sx={{ fontWeight: 800 }}>
            تسجيل دفعة تحت الحساب
          </Typography>
          <IconButton onClick={() => setOpen(false)} disabled={saving} aria-label="إغلاق">
            <IconX size={20} />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers>
          <Box component="form" onSubmit={submit} sx={{ pt: 1 }}>
            <Grid container spacing={2}>
              {!lockedProjectId && (
                <Grid size={{ xs: 12 }}>
                  <FormControl fullWidth required>
                    <InputLabel>المشروع</InputLabel>
                    <Select value={form.projectId} label="المشروع" onChange={(e) => handleProjectSelect(e.target.value)}>
                      {projects.map((p) => (
                        <MenuItem key={p.id} value={String(p.id)}>
                          {p.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
              )}
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField fullWidth label="اسم العميل" value={form.clientName} onChange={(e) => setForm({ ...form, clientName: e.target.value })} />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField fullWidth label="اسم الدافع" value={form.payerName} onChange={(e) => setForm({ ...form, payerName: e.target.value })} />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  required
                  type="number"
                  inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                  label="قيمة الدفعة"
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: e.target.value })}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField fullWidth select label="العملة" value={form.currency} onChange={e => setForm({ ...form, currency: e.target.value })}>
                  <MenuItem value="EGP">EGP</MenuItem><MenuItem value="USD">USD</MenuItem>
                </TextField>
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField
                  fullWidth
                  type="date"
                  label="تاريخ الدفع"
                  value={form.paymentDate}
                  onChange={(e) => setForm({ ...form, paymentDate: e.target.value })}
                  InputLabelProps={{ shrink: true }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField fullWidth label="طريقة الدفع" value={form.paymentMethod} onChange={(e) => setForm({ ...form, paymentMethod: e.target.value })} />
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <TextField fullWidth label="المرجع" value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} />
              </Grid>
              <Grid size={{ xs: 12 }}>
                <TextField fullWidth multiline minRows={2} label="ملاحظات" value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
              </Grid>
              <Grid size={{ xs: 12 }}>
                <Button component="label" variant="outlined" disabled={saving} startIcon={<IconReceipt size={17} />} fullWidth={downSM}>
                  {attachment ? `المرفق: ${attachment.name}` : 'إرفاق إثبات الدفعة (اختياري)'}
                  <input
                    hidden
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx"
                    capture="environment"
                    onChange={(e) => {
                      const selected = e.target.files?.[0] || null;
                      if (!selected) {
                        setAttachment(null);
                        return;
                      }
                      const error = getAttachmentError(selected);
                      if (error) {
                        setAttachment(null);
                        setMessage({ severity: 'error', text: error });
                        e.currentTarget.value = '';
                        return;
                      }
                      setMessage(null);
                      setAttachment(selected);
                    }}
                  />
                </Button>
              </Grid>
              <Grid size={{ xs: 12 }}>
                <Stack direction="row" spacing={1.5} justifyContent="flex-end">
                  <Button onClick={() => setOpen(false)} disabled={saving}>
                    إلغاء
                  </Button>
                  <Button type="submit" disabled={saving || uploadingAttachment} variant="contained" startIcon={<IconCash size={18} />} sx={{ fontWeight: 700 }}>
                    {saving ? (attachment ? 'جاري الحفظ ورفع المرفق...' : 'جاري الحفظ...') : 'حفظ الدفعة'}
                  </Button>
                </Stack>
              </Grid>
            </Grid>
          </Box>
        </DialogContent>
      </Dialog>
    </Box>
  );
}
