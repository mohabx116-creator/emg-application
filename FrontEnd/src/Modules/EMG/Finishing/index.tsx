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
  createEmgFinishing,
  EmgProject,
  EmgSupplier,
  executeEmgWorkflow,
  getEmgFinishing,
  getEmgProject,
  getEmgSuppliers,
  getEmgWorkflowAudit,
  uploadEmgAttachment
} from 'api/emg';
import type { EmgFinishing } from 'api/emg';
import useAuth from 'hooks/useAuth';
import WorkSectionShell, { formatWorkMoney, WorkSectionSkeleton, WorkStatusChip } from '../WorkSectionShell';

const today = new Date().toISOString().slice(0, 10);

export default function EmgFinishing() {
  const { projectId } = useParams();
  const id = Number(projectId);
  const { user } = useAuth();
  const canCreate = !user?.permissions || user.permissions.includes('finishing.create') || user.permissions.includes('finishing.manage');
  const canSubmit = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('finishing.submit');
  const canReview = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('finishing.approve');
  const canPost = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('finishing.post');

  const [project, setProject] = useState<EmgProject | null>(null);
  const [records, setRecords] = useState<EmgFinishing[]>([]);
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
    workType: '',
    accountCategory: 'غير محدد',
    areaName: '',
    quantity: '',
    unit: 'م2',
    unitPrice: '',
    workDate: today,
    description: '',
    supplierId: '',
    notes: ''
  });

  const update = (key: keyof typeof form, value: string) => setForm((current) => ({ ...current, [key]: value }));
  const total = useMemo(() => (Number(form.quantity) || 0) * (Number(form.unitPrice) || 0), [form.quantity, form.unitPrice]);

  const load = () => {
    setLoading(true);
    Promise.all([getEmgProject(id), getEmgFinishing(id), getEmgSuppliers().catch(() => [])])
      .then(([p, r, s]) => {
        setProject(p);
        setRecords(r);
        setSuppliers(s);
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل أعمال التشطيبات.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const created = await createEmgFinishing({
        projectId: id,
        supplierId: form.supplierId ? Number(form.supplierId) : null,
        accountCategory: form.accountCategory,
        workType: form.workType,
        areaName: form.areaName || null,
        quantity: Number(form.quantity),
        unit: form.unit,
        unitPrice: Number(form.unitPrice),
        workDate: form.workDate,
        description: form.description,
        notes: form.notes || null
      });
      let severity: 'success' | 'warning' = 'success';
      let text = 'تم تسجيل أعمال التشطيبات بنجاح.';
      if (attachment) {
        try {
          await uploadEmgAttachment('finishing', created.id, attachment);
          text = 'تم تسجيل أعمال التشطيبات ورفع المرفق بنجاح.';
        } catch {
          severity = 'warning';
          text = 'تم تسجيل أعمال التشطيبات، لكن تعذر رفع المرفق.';
        }
      }
      setMessage({ severity, text });
      setAttachment(null);
      setCreateOpen(false);
      setForm({ ...form, workType: '', areaName: '', quantity: '', unitPrice: '', description: '', notes: '' });
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر تسجيل أعمال التشطيبات.' });
    } finally {
      setSaving(false);
    }
  };

  const executeRecordAction = async (record: EmgFinishing, action: string) => {
    setWorkflowBusy(record.id);
    setMessage(null);
    try {
      if (action === 'اعتماد' || action === 'ترحيل') {
        const audit = await getEmgWorkflowAudit('finishing', record.id);
        if (audit.blockingChecks.length > 0) {
          setMessage({ severity: 'error', text: audit.blockingChecks.join(' · ') });
          return;
        }
      }
      await executeEmgWorkflow('finishing', record.id, action);
      setMessage({ severity: 'success', text: `تم تنفيذ إجراء ${action} على سجل التشطيبات.` });
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
      return `${r.description} ${r.workType || ''} ${r.areaName || ''} ${r.supplierName || ''} ${r.notes || ''}`
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
        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" required label="نوع العمل" placeholder="مثال: محارة، مباني، عزل، كهرباء" value={form.workType} onChange={(e) => update('workType', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <FormControl fullWidth size="small">
            <InputLabel>فئة الحساب</InputLabel>
            <Select value={form.accountCategory} label="فئة الحساب" onChange={(e) => update('accountCategory', e.target.value)}>
              <MenuItem value="غير محدد">غير محدد</MenuItem>
              <MenuItem value="مباني">مباني</MenuItem>
              <MenuItem value="محارة">محارة</MenuItem>
              <MenuItem value="عزل">عزل</MenuItem>
              <MenuItem value="كهرباء">كهرباء</MenuItem>
              <MenuItem value="سباكة">سباكة</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" required type="date" label="تاريخ العمل" value={form.workDate} onChange={(e) => update('workDate', e.target.value)} InputLabelProps={{ shrink: true }} />
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

        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField fullWidth size="small" label="المنطقة / الدور" placeholder="مثال: الدور الثاني، الواجهة" value={form.areaName} onChange={(e) => update('areaName', e.target.value)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small">
            <InputLabel>المقاول / المورد</InputLabel>
            <Select value={form.supplierId} label="المقاول / المورد" onChange={(e) => update('supplierId', e.target.value)}>
              <MenuItem value="">بدون مقاول محدد</MenuItem>
              {suppliers.filter((s) => s.isActive).map((s) => (
                <MenuItem key={s.id} value={s.id}>{s.name}</MenuItem>
              ))}
            </Select>
          </FormControl>
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
              {attachment ? `المرفق: ${attachment.name}` : 'إرفاق مستخلص أو فاتورة (اختياري)'}
              <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx" onChange={(e) => setAttachment(e.target.files?.[0] || null)} />
            </Button>
            <Stack direction="row" spacing={1.25} justifyContent="flex-end">
              <Button onClick={() => setCreateOpen(false)} disabled={saving} color="inherit" sx={{ borderRadius: 2, fontWeight: 700 }}>إلغاء</Button>
              <Button type="submit" disabled={saving} variant="contained" color="secondary" startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <IconPlus size={18} />} sx={{ borderRadius: 2, fontWeight: 700, px: 3 }}>
                {saving ? 'جاري الحفظ...' : 'تسجيل التشطيبات'}
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
        activeSection="finishing"
        title="أعمال التشطيبات"
        addButtonLabel="تسجيل عمل تشطيبات جديد"
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
        createModalTitle="تسجيل أعمال تشطيبات جديدة"
        onCloseCreateModal={() => !saving && setCreateOpen(false)}
        createModalContent={createForm}
      >
        <TableContainer sx={{ maxHeight: 580, overflowX: 'auto' }}>
          <Table stickyHeader size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 800 }}>التاريخ</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>الوصف ونوع العمل</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>المنطقة / الدور</TableCell>
                <TableCell sx={{ fontWeight: 800 }}>المقاول / المورد</TableCell>
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
                      {search || statusFilter !== 'all' ? 'لا توجد سجلات تشطيبات مطابقة.' : 'لا توجد أعمال تشطيبات مسجلة حتى الآن.'}
                    </Typography>
                  </TableCell>
                </TableRow>
              ) : (
                paginated.map((record) => (
                  <TableRow key={record.id} hover>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{new Date(record.workDate).toLocaleDateString('ar-EG')}</TableCell>
                    <TableCell sx={{ maxWidth: 280 }}>
                      <Typography variant="body2" fontWeight={700}>{record.description}</Typography>
                      {record.workType && <Chip size="small" variant="outlined" label={record.workType} sx={{ mt: 0.35, height: 20, fontWeight: 600 }} />}
                    </TableCell>
                    <TableCell>{record.areaName || '—'}</TableCell>
                    <TableCell>{record.supplierName || '—'}</TableCell>
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
          مرفقات سجل التشطيبات
          <IconButton onClick={() => setAttachmentTarget(null)} size="small"><IconX size={18} /></IconButton>
        </DialogTitle>
        <DialogContent dividers>
          {attachmentTarget !== null && <AttachmentGallery entityType="finishing" entityId={attachmentTarget} />}
        </DialogContent>
      </Dialog>
    </>
  );
}
