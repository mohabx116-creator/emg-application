import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Skeleton,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import { Link, useParams } from 'react-router-dom';
import { IconArrowRight, IconCash, IconPlus, IconReceipt, IconRefresh, IconTrash, IconX } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { createEmgAdvance, deleteEmgTransaction, EmgAdvance, getEmgAdvances, uploadEmgAttachment } from 'api/emg';
import useAuth from 'hooks/useAuth';
import { invalidateEmgCache, useEmgProjectsCache } from 'hooks/useEmgData';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import { formatEmgMoney, getLocalIsoDate } from 'utils/emgFormatting';

const MAX_ATTACHMENT_SIZE = 20 * 1024 * 1024;
const ALLOWED_ATTACHMENT_EXTENSIONS = new Set(['pdf', 'jpg', 'jpeg', 'png', 'webp', 'xlsx', 'xls', 'doc', 'docx']);

const getAttachmentError = (file: File) => {
  const extension = file.name.split('.').pop()?.toLowerCase() || '';
  if (!ALLOWED_ATTACHMENT_EXTENSIONS.has(extension)) return 'نوع المرفق غير مسموح. ارفع PDF أو صورة أو ملف Excel أو Word.';
  if (file.size > MAX_ATTACHMENT_SIZE) return 'حجم المرفق يتجاوز 20 ميجابايت.';
  return null;
};

export default function EmgAdvances() {
  const { projectId: routeProjectId } = useParams();
  const lockedProjectId = routeProjectId ? Number(routeProjectId) : null;
  const theme = useTheme();
  const downSM = useMediaQuery(theme.breakpoints.down('sm'));
  const { user } = useAuth();
  const { setActiveProjectId } = useActiveProject();
  const { data: projects = [] } = useEmgProjectsCache(true);

  const canCreate = user?.permissions?.includes('advances.create');
  const [advances, setAdvances] = useState<EmgAdvance[]>([]);
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
    setLoading(true);
    getEmgAdvances(pid)
      .then((a) => setAdvances(a))
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل الدفعات تحت الحساب.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectFilter, lockedProjectId]);

  const totals = useMemo(
    () =>
      advances.reduce(
        (x, a) => ({ amount: x.amount + a.amount, allocated: x.allocated + a.allocatedAmount, due: x.due + a.dueFromPayer }),
        { amount: 0, allocated: 0, due: 0 }
      ),
    [advances]
  );

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
      await invalidateEmgCache('emg/advances');
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
      await invalidateEmgCache('emg/advances');
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

      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="إجمالي الدفعات" sx={{ borderRadius: 3 }}>
            <Typography variant="h3" sx={{ fontWeight: 850, fontVariantNumeric: 'tabular-nums' }}>
              {formatEmgMoney(totals.amount)}
            </Typography>
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="المخصص على حركات" sx={{ borderRadius: 3 }}>
            <Typography variant="h3" color="primary" sx={{ fontWeight: 850, fontVariantNumeric: 'tabular-nums' }}>
              {formatEmgMoney(totals.allocated)}
            </Typography>
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="مستحق على الدافع" sx={{ borderRadius: 3 }}>
            <Typography variant="h3" color="error.main" sx={{ fontWeight: 850, fontVariantNumeric: 'tabular-nums' }}>
              {formatEmgMoney(totals.due)}
            </Typography>
          </MainCard>
        </Grid>
      </Grid>

      <MainCard
        title="قائمة الدفعات"
        sx={{ borderRadius: 3 }}
        secondary={
          <Stack direction="row" spacing={1}>
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
            {advances.map((advance) => (
              <Grid key={advance.id} size={{ xs: 12, md: 6, xl: 4 }}>
                <Card
                  component={Link}
                  to={`/emg/advances/${advance.id}`}
                  variant="outlined"
                  sx={{ textDecoration: 'none', color: 'inherit', display: 'block', borderRadius: 3, '&:hover': { borderColor: 'primary.main' } }}
                >
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
                      <Box>
                        <Typography variant="h4" sx={{ fontWeight: 800 }}>
                          {advance.projectName}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          العميل: {advance.clientName || 'غير محدد'}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          الدافع: {advance.payerName || 'غير محدد'}
                        </Typography>
                      </Box>
                      <Stack direction="row" spacing={0.5} alignItems="center">
                        <Chip size="small" label={advance.balanceStatus} color={advance.dueFromPayer > 0 ? 'error' : 'success'} />
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
                    <Stack spacing={0.75} sx={{ mt: 2 }}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">قيمة الدفعة</Typography>
                        <Typography fontWeight={800} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                          {formatEmgMoney(advance.amount)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">المخصص</Typography>
                        <Typography sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatEmgMoney(advance.allocatedAmount)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">المتاح</Typography>
                        <Typography color="primary.main" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                          {formatEmgMoney(advance.unallocatedAmount)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">مستحق على الدافع</Typography>
                        <Typography color="error.main" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                          {formatEmgMoney(advance.dueFromPayer)}
                        </Typography>
                      </Stack>
                    </Stack>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
                      {advance.paymentDate ? new Date(advance.paymentDate).toLocaleDateString('ar-EG') : 'التاريخ غير مدون'} · {advance.paymentMethod} · {advance.status}
                    </Typography>
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>
        )}
        {!loading && !advances.length && (
          <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
            لا توجد دفعات تحت الحساب لهذا الاختيار.
          </Typography>
        )}
      </MainCard>

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
