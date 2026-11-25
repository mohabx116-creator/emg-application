import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from '@mui/material';
import { IconCheck, IconPaperclip, IconPlus, IconSend, IconTransfer, IconX } from '@tabler/icons-react';
import { useParams } from 'react-router-dom';
import AttachmentGallery from 'ui-component/emg/AttachmentGallery';
import SmartEntryAdvisor from 'ui-component/emg/SmartEntryAdvisor';
import {
  createEmgSteel,
  EmgProject,
  EmgSupplier,
  executeEmgWorkflow,
  getEmgProject,
  getEmgSteel,
  getEmgSuppliers,
  getEmgWorkflowAudit,
  uploadEmgAttachment
} from 'api/emg';
import type { EmgSteel } from 'api/emg';
import useAuth from 'hooks/useAuth';
import WorkSectionShell, { formatWorkMoney, WorkSectionSkeleton, WorkStatusChip } from '../WorkSectionShell';

const today = new Date().toISOString().slice(0, 10);

export default function EmgSteel() {
  const { projectId } = useParams();
  const id = Number(projectId);
  const { user } = useAuth();
  const canCreate = !user?.permissions || user.permissions.includes('steel.create') || user.permissions.includes('steel.manage');
  const canSubmit = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('steel.submit');
  const canReview = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('steel.approve');
  const canPost = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('steel.post');

  const [project, setProject] = useState<EmgProject | null>(null);
  const [records, setRecords] = useState<EmgSteel[]>([]);
  const [suppliers, setSuppliers] = useState<EmgSupplier[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [attachment, setAttachment] = useState<File | null>(null);
  const [workflowBusy, setWorkflowBusy] = useState<number | null>(null);
  const [attachmentTarget, setAttachmentTarget] = useState<number | null>(null);
  const [message, setMessage] = useState<{ severity: 'success' | 'warning' | 'error'; text: string } | null>(null);

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  const [form, setForm] = useState({
    operationType: 'توريد حديد',
    steelType: '',
    barDiameter: '',
    grade: '',
    quantity: '',
    unit: 'طن',
    unitPrice: '',
    operationDate: today,
    deliveryNoteNumber: '',
    description: '',
    supplierId: '',
    notes: ''
  });

  const load = () => {
    setLoading(true);
    Promise.all([getEmgProject(id), getEmgSteel(id), getEmgSuppliers().catch(() => [])])
      .then(([p, r, s]) => {
        setProject(p);
        setRecords(r);
        setSuppliers(s);
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل أعمال الحديد.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id]);

  const total = useMemo(() => (Number(form.quantity) || 0) * (Number(form.unitPrice) || 0), [form.quantity, form.unitPrice]);
  const update = (key: keyof typeof form, value: string) => setForm((current) => ({ ...current, [key]: value }));

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const created = await createEmgSteel({
        projectId: id,
        supplierId: form.supplierId ? Number(form.supplierId) : null,
        operationType: form.operationType,
        steelType: form.steelType || null,
        barDiameter: form.barDiameter || null,
        grade: form.grade || null,
        quantity: Number(form.quantity),
        unit: form.unit,
        unitPrice: Number(form.unitPrice),
        operationDate: form.operationDate,
        deliveryNoteNumber: form.deliveryNoteNumber || null,
        description: form.description,
        notes: form.notes || null
      });
      let severity: 'success' | 'warning' = 'success';
      let text = 'تم تسجيل توريد الحديد بنجاح.';
      if (attachment) {
        try {
          await uploadEmgAttachment('steel', created.id, attachment);
          text = 'تم تسجيل توريد الحديد ورفع المرفق بنجاح.';
        } catch {
          severity = 'warning';
          text = 'تم تسجيل توريد الحديد، لكن تعذر رفع المرفق.';
        }
      }
      setMessage({ severity, text });
      setAttachment(null);
      setCreateOpen(false);
      setForm({ ...form, steelType: '', barDiameter: '', grade: '', quantity: '', unitPrice: '', deliveryNoteNumber: '', description: '', notes: '' });
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر تسجيل توريد الحديد.' });
    } finally {
      setSaving(false);
    }
  };

  const executeRecordAction = async (record: EmgSteel, action: string) => {
    setWorkflowBusy(record.id);
    setMessage(null);
    try {
      if (action === 'اعتماد' || action === 'ترحيل') {
        const audit = await getEmgWorkflowAudit('steel', record.id);
        if (audit.blockingChecks.length > 0) {
          setMessage({ severity: 'error', text: audit.blockingChecks.join(' · ') });
          return;
        }
      }
      await executeEmgWorkflow('steel', record.id, action);
      setMessage({ severity: 'success', text: `تم تنفيذ إجراء ${action} على سجل الحديد.` });
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'الإجراء غير مسموح للحالة الحالية أو لا تملك الصلاحية.' });
    } finally {
      setWorkflowBusy(null);
    }
  };

  const availableStatuses = useMemo(() => Array.from(new Set(records.map((r) => r.status).filter(Boolean))), [records]);
  const totalAmount = useMemo(() => records.reduce((sum, r) => sum + (r.totalAmount || 0), 0), [records]);
  const approvedCount = useMemo(() => records.filter((r) => r.status === 'معتمد' || r.status === 'مرحّل').length, [records]);
  const pendingCount = records.length - approvedCount;

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return records.filter((r) => {
      if (statusFilter !== 'all' && r.status !== statusFilter) return false;
      if (!q) return true;
      return `${r.description} ${r.operationType} ${r.steelType || ''} ${r.barDiameter || ''} ${r.supplierName || ''} ${r.deliveryNoteNumber || ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [records, search, statusFilter]);

  const paginated = useMemo(() => filtered.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage), [filtered, page, rowsPerPage]);

  useEffect(() => {
    setPage(0);
  }, [search, statusFilter]);

  if (loading && !project) return <WorkSectionSkeleton />;
  if (!project) return <Alert severity="error">المشروع غير موجود أو لا تملك صلاحية الوصول إليه.</Alert>;

  const createForm = (
    <Box component="form" onSubmit={submit}>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small">
            <InputLabel>نوع العملية</InputLabel>
            <Select value={form.operationType} label="نوع العملية" onChange={(e) => update('operationType', e.target.value)}>
              <MenuItem value="توريد حديد">توريد حديد</MenuItem>
              <MenuItem value="حدادة ومصنعية">حدادة ومصنعية</MenuItem>
              <MenuItem value="نقل وتعتيق">نقل وتعتيق</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField fullWidth size="small" required type="date" label="تاريخ العملية" value={form.operationDate} onChange={(e) => update('operationDate', e.target.value)} InputLabelProps={{ shrink: true }} />
        </Grid>

        <Grid size={12}>
          <TextField fullWidth size="small" required label="الوصف" value={form.description} onChange={(e) => update('description', e.target.value)} />
        </Grid>

        <Grid size={12}>
          <SmartEntryAdvisor
            projectId={id}
            description={form.description}
            unit={form.unit}
            quantity={form.quantity ? Number(form.quantity) : null}
            unitPrice={form.unitPrice ? Number(form.unitPrice) : null}
            onApply={(s) => {
              const matched = s.matchedSupplierName ? suppliers.find((x) => x.name === s.matchedSupplierName) : undefined;
              setForm((cur) => ({
                ...cur,
                unit: s.suggestedUnit || cur.unit,
                supplierId: matched ? String(matched.id) : cur.supplierId
              }));
            }}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" label="نوع الحديد" placeholder="مثال: عز، بشاي" value={form.steelType} onChange={(e) => update('steelType', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 6, sm: 4 }}>
          <TextField fullWidth size="small" label="القطر (مم)" placeholder="مثال: 12، 16" value={form.barDiameter} onChange={(e) => update('barDiameter', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 6, sm: 4 }}>
          <TextField fullWidth size="small" label="الرتبة / الدرجة" value={form.grade} onChange={(e) => update('grade', e.target.value)} />
        </Grid>

        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" required type="number" label="الكمية" value={form.quantity} onChange={(e) => update('quantity', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" required label="الوحدة" value={form.unit} onChange={(e) => update('unit', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" required type="number" label="سعر الوحدة" value={form.unitPrice} onChange={(e) => update('unitPrice', e.target.value)} />
        </Grid>

        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small">
            <InputLabel>المورد</InputLabel>
            <Select value={form.supplierId} label="المورد" onChange={(e) => update('supplierId', e.target.value)}>
              <MenuItem value="">بدون مورد محدد</MenuItem>
              {suppliers.filter((s) => s.isActive).map((s) => (
                <MenuItem key={s.id} value={s.id}>{s.name}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField fullWidth size="small" label="رقم إذن التسليم" value={form.deliveryNoteNumber} onChange={(e) => update('deliveryNoteNumber', e.target.value)} />
        </Grid>

        <Grid size={12}>
          <Box sx={{ p: 1.5, borderRadius: 2, border: '1px solid', borderColor: 'primary.light', bgcolor: 'rgba(30, 136, 229, 0.06)' }}>
            <Typography variant="caption" color="text.secondary" fontWeight={700}>الإجمالي المحسوب</Typography>
            <Typography variant="h3" color="primary.main" fontWeight={900}>{formatWorkMoney(total)}</Typography>
          </Box>
        </Grid>

        <Grid size={12}>
          <TextField fullWidth size="small" multiline minRows={2} label="ملاحظات" value={form.notes} onChange={(e) => update('notes', e.target.value)} />
        </Grid>

        <Grid size={12}>
          <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={1.5}>
            <Button component="label" size="small" variant="outlined" startIcon={<IconPaperclip size={16} />} sx={{ borderRadius: 2 }}>
              {attachment ? `المرفق: ${attachment.name}` : 'إرفاق الفاتورة أو إذن التسليم (اختياري)'}
              <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx" onChange={(e) => setAttachment(e.target.files?.[0] || null)} />
            </Button>
            <Stack direction="row" spacing={1.25} justifyContent="flex-end">
              <Button onClick={() => setCreateOpen(false)} disabled={saving} color="inherit" sx={{ borderRadius: 2, fontWeight: 700 }}>إلغاء</Button>
              <Button type="submit" disabled={saving} variant="contained" color="secondary" startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <IconPlus size={18} />} sx={{ borderRadius: 2, fontWeight: 700, px: 3 }}>
                {saving ? 'جاري الحفظ...' : 'تسجيل الحديد'}
              </Button>
            </Stack>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );

  return (
    <>
      <WorkSectionShell
        project={project}
        activeSection="steel"
        title="أعمال وتوريد الحديد"
        addButtonLabel="تسجيل توريد حديد جديد"
        canCreate={canCreate}
        onOpenCreate={() => setCreateOpen(true)}
        onRefresh={load}
        loading={loading}
        message={message}
        onCloseMessage={() => setMessage(null)}
        totalAmount={totalAmount}
        totalCount={records.length}
        approvedCount={approvedCount}
        pendingCount={pendingCount}
        search={search}
        onSearchChange={setSearch}
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        availableStatuses={availableStatuses}
        filteredCount={filtered.length}
        page={page}
        rowsPerPage={rowsPerPage}
        onPageChange={setPage}
        onRowsPerPageChange={setRowsPerPage}
        createModalOpen={createOpen}
        createModalTitle="تسجيل توريد / عمل حديد جديد"
        onCloseCreateModal={() => !saving && setCreateOpen(false)}
        createModalContent={createForm}
      >
        <TableContainer sx={{ maxHeight: 580, overflowX: 'auto' }}>
          <Table stickyHeader size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 800 }}>التاريخ</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>الوصف والنوع</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>المواصفات</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>المورد / الإذن</TableCell>
                <TableCell align="right" sx={{ fontWeight: 800 }}>الكمية × السعر</TableCell>
                <TableCell align="right" sx={{ fontWeight: 800 }}>الإجمالي</TableCell>
                <TableCell align="center" sx={{ fontWeight: 800 }}>الحالة</TableCell>
                <TableCell align="center" sx={{ fontWeight: 800 }}>الإجراءات</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {paginated.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} align="center" sx={{ py: 5 }}>
                    <Typography color="text.secondary" fontWeight={600}>
                      {search || statusFilter !== 'all' ? 'لا توجد سجلات حديد مطابقة.' : 'لا توجد أعمال حديد مسجلة حتى الآن.'}
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                paginated.map((record) => (
                  <TableRow key={record.id} hover>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{new Date(record.operationDate).toLocaleDateString('ar-EG')}</TableCell>
                    <TableCell sx={{ maxWidth: 280 }}>
                      <Typography variant="body2" fontWeight={700}>{record.description}</Typography>
                      <Chip size="small" variant="outlined" label={record.operationType} sx={{ mt: 0.35, height: 20, fontWeight: 600 }} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{record.steelType || '—'}</Typography>
                      {(record.barDiameter || record.grade) && (
                        <Typography variant="caption" color="text.secondary">
                          {[record.barDiameter && `قطر ${record.barDiameter}`, record.grade].filter(Boolean).join(' · ')}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{record.supplierName || '—'}</Typography>
                      {record.deliveryNoteNumber && <Typography variant="caption" color="text.secondary">إذن: {record.deliveryNoteNumber}</Typography>}
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
                      <Typography variant="body2" fontWeight={600}>{record.quantity} {record.unit}</Typography>
                      <Typography variant="caption" color="text.secondary">{formatWorkMoney(record.unitPrice)}</Typography>
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                      {formatWorkMoney(record.totalAmount)}
                    </TableCell>
                    <TableCell align="center"><WorkStatusChip status={record.status} /></TableCell>
                    <TableCell align="center" sx={{ whiteSpace: 'nowrap' }}>
                      <Stack direction="row" spacing={0.5} justifyContent="center">
                        <Button size="small" onClick={() => setAttachmentTarget(record.id)} startIcon={<IconPaperclip size={14} />}>المرفقات</Button>
                        {canSubmit && (record.status === 'مسودة' || record.status === 'مرفوض') && (
                          <Button size="small" disabled={workflowBusy === record.id} onClick={() => executeRecordAction(record, 'إرسال')} startIcon={<IconSend size={14} />}>إرسال</Button>
                        )}
                        {canReview && record.status === 'للمراجعة' && (
                          <>
                            <Button size="small" color="success" disabled={workflowBusy === record.id} onClick={() => executeRecordAction(record, 'اعتماد')} startIcon={<IconCheck size={14} />}>اعتماد</Button>
                            <Button size="small" color="error" disabled={workflowBusy === record.id} onClick={() => executeRecordAction(record, 'رفض')} startIcon={<IconX size={14} />}>رفض</Button>
                          </>
                        )}
                        {canPost && record.status === 'معتمد' && (
                          <Button size="small" color="secondary" disabled={workflowBusy === record.id} onClick={() => executeRecordAction(record, 'ترحيل')} startIcon={<IconTransfer size={14} />}>ترحيل</Button>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </WorkSectionShell>

      <Dialog open={attachmentTarget !== null} onClose={() => setAttachmentTarget(null)} fullWidth maxWidth="md">
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          مرفقات سجل الحديد
          <IconButton onClick={() => setAttachmentTarget(null)} size="small"><IconX size={18} /></IconButton>
        </DialogTitle>
        <DialogContent dividers>
          {attachmentTarget !== null && <AttachmentGallery entityType="steel" entityId={attachmentTarget} />}
        </DialogContent>
      </Dialog>
    </>
  );
}
