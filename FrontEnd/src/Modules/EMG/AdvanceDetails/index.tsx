import { invalidateEmgCache } from 'hooks/useEmgData';
import { FormEvent, useEffect, useRef, useState } from 'react';
import { getAllocationTargets, type AllocationTarget } from 'api/allocationTargets';
import { Link, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from '@mui/material';
import { IconArrowRight, IconDownload, IconEdit, IconEye, IconFile, IconPlus, IconTrash } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import {
  allocateEmgAdvance,
  correctEmgTransaction,
  deleteEmgTransaction,
  downloadEmgAttachment,
  EmgAdvance,
  EmgAdvanceAllocation,
  EmgAttachment,
  getEmgAdvance,
  getEmgAdvanceAllocations,
  getEmgAttachmentPreviewUrl,
  getEmgAttachments,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';

const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });


export default function AdvanceDetails() {
  const { advanceId } = useParams();
  const id = Number(advanceId);
  const { user } = useAuth();
  const canAllocate = user?.permissions?.includes('advances.allocate');
  const [advance, setAdvance] = useState<EmgAdvance | null>(null);
  const fmt = (value: number) => `${money.format(value)} ${advance?.currency ?? 'EGP'}`;
  const sequence = useRef(0);
  const [loadError, setLoadError] = useState(false);
  const [targets, setTargets] = useState<AllocationTarget[]>([]);
  const [allocations, setAllocations] = useState<EmgAdvanceAllocation[]>([]);
  const [attachments, setAttachments] = useState<EmgAttachment[]>([]);
  const [targetType, setTargetType] = useState('movement');
  const [targetId, setTargetId] = useState('');
  const [accountCategory, setAccountCategory] = useState('الموقع');
  const [amount, setAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [message, setMessage] = useState<{ severity: 'success' | 'error'; text: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const [previewFile, setPreviewFile] = useState<EmgAttachment | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [editForm, setEditForm] = useState({ clientName: '', payerName: '', amount: '', paymentDate: '', paymentMethod: '', reference: '', notes: '' });
  const load = async () => {
    const current = ++sequence.current;
    setLoadError(false);
    try {
      const [receipt, rows, files, available] = await Promise.all([
        getEmgAdvance(id), getEmgAdvanceAllocations(id), getEmgAttachments('advance-payment', id).catch(() => []),
        canAllocate ? getAllocationTargets(id).catch(() => null) : Promise.resolve([])
      ]);
      if (sequence.current !== current) return;
      setAdvance(receipt);
      setAllocations(rows);
      setAttachments(files);
      setTargets(available ?? []);
      if (!receipt) setLoadError(true);
      if (!available) setMessage({ severity: 'error', text: 'تعذر تحميل الحركات والمدفوعات المتاحة للتخصيص.' });
    } catch {
      if (sequence.current === current) { setLoadError(true); setAdvance(null); }
    }
  };
  useEffect(() => {
    setAdvance(null);
    setTargetId('');
    setTargets([]);
    setMessage(null);
    if (Number.isInteger(id)) void load();
    return () => { ++sequence.current; };
  }, [id, canAllocate]);
  const selectedTarget = targets.find(item => item.kind === targetType && item.id === Number(targetId));
  const targetRemaining = selectedTarget?.remaining ?? null;
  const allocationAmount = Number(amount);
  const targetWarning = targetRemaining !== null && Number.isFinite(allocationAmount) && allocationAmount > targetRemaining
    ? `قيمة التخصيص أكبر من المتبقي على المستهدف بمقدار ${fmt(allocationAmount - targetRemaining)}.`
    : null;
  const availableAdvance = advance?.unallocatedAmount ?? 0;
  const advanceWarning = Number.isFinite(allocationAmount) && allocationAmount > availableAdvance
    ? `هذا التخصيص سيتجاوز المتاح في الدفعة بمقدار ${fmt(allocationAmount - availableAdvance)}. اختر مبلغًا ضمن المتبقي على الإيصال.`
    : null;
  const canSubmitAllocation = Number.isFinite(allocationAmount) && allocationAmount > 0 && !targetWarning && !advanceWarning &&
    (targetType === 'category' || !!selectedTarget);
  const closePreview = () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(null);
    setPreviewFile(null);
  };
  const openPreview = async (file: EmgAttachment) => {
    closePreview();
    setPreviewFile(file);
    setPreviewLoading(true);
    try {
      setPreviewUrl(await getEmgAttachmentPreviewUrl(file.id));
    } catch {
      setMessage({ severity: 'error', text: 'تعذر عرض المرفق.' });
      setPreviewFile(null);
    } finally {
      setPreviewLoading(false);
    }
  };
  const isImage = previewFile?.contentType.startsWith('image/') ?? false;
  const isPdf = previewFile?.contentType === 'application/pdf' || previewFile?.originalFileName.toLowerCase().endsWith('.pdf');

  const uploadAttachment = async (file: File) => {
    setUploading(true);
    setMessage(null);
    try {
      await uploadEmgAttachment('advance-payment', id, file);
      setMessage({ severity: 'success', text: 'تم رفع مرفق الدفعة بنجاح.' });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر رفع مرفق الدفعة.' });
    } finally {
      setUploading(false);
    }
  };
  const openEdit = () => { if (!advance) return; setEditForm({ clientName: advance.clientName, payerName: advance.payerName, amount: String(advance.amount), paymentDate: advance.paymentDate?.slice(0, 10) ?? '', paymentMethod: advance.paymentMethod, reference: advance.reference || '', notes: advance.notes || '' }); setEditOpen(true); };
  const saveEdit = async (event: FormEvent) => { event.preventDefault(); try { await correctEmgTransaction('advance-payment', id, { ...editForm, amount: Number(editForm.amount) }, 'تصحيح مباشر'); setEditOpen(false); setMessage({ severity: 'success', text: 'تم حفظ تصحيح الدفعة.' }); await load(); } catch (error: any) { setMessage({ severity: 'error', text: error?.message || 'تعذر تصحيح الدفعة.' }); } };
  const remove = async () => { if (!window.confirm('سيتم حذف الدفعة نهائيًا. هل تريد المتابعة؟')) return; try { await deleteEmgTransaction('advance-payment', id); window.location.assign(advance ? `/emg/projects/${advance.projectId}/advances` : '/emg/advances'); } catch (error: any) { setMessage({ severity: 'error', text: error?.message || 'تعذر حذف الدفعة. قد توجد تخصيصات مرتبطة بها.' }); } };
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!canSubmitAllocation) {
      setMessage({ severity: 'error', text: targetWarning || advanceWarning || 'اختر هدفًا متاحًا وأدخل مبلغًا موجبًا.' });
      return;
    }
    setSaving(true);
    try {
      await allocateEmgAdvance(id, {
        siteMovementId: targetType === 'movement' ? Number(targetId) : null,
        supplierPaymentId: targetType === 'supplier' ? Number(targetId) : null,
        accountCategory,
        amount: Number(amount),
        notes
      });
      await Promise.all([invalidateEmgCache('emg/projects'), invalidateEmgCache('emg/advances')]);
      setMessage({ severity: 'success', text: 'تم حفظ توزيع الدفعة.' });
      setTargetId('');
      setAmount('');
      setNotes('');
      load();
    } catch (e: any) {
      setMessage({ severity: 'error', text: e?.message || 'تعذر تخصيص الدفعة.' });
    } finally {
      setSaving(false);
    }
  };
  if (loadError) return <Alert severity="error">تعذر تحميل تفاصيل الدفعة أو لا تملك صلاحية الوصول إليها.</Alert>;
  if (!advance)
    return (
      <Box sx={{ p: 4 }}>
        <Typography>جاري تحميل تفاصيل الدفعة...</Typography>
      </Box>
    );
  return (
    <Box sx={{ pb: 4 }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={2}
        sx={{ mb: 2 }}
      >
        <Box>
          <Typography variant="h2">تفاصيل الدفعة تحت الحساب</Typography>
          <Typography color="text.secondary">
            {advance.projectName} · الدافع: {advance.payerName || 'غير محدد'}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}><Button onClick={openEdit} variant="outlined" startIcon={<IconEdit size={17} />}>تعديل</Button><Button onClick={() => void remove()} color="error" variant="outlined" startIcon={<IconTrash size={17} />}>حذف</Button><Button component={Link} to={`/emg/projects/${advance.projectId}/advances`} endIcon={<IconArrowRight size={18} />} variant="outlined" sx={{ gap: 0.75 }}>كل الدفعات</Button></Stack>
      </Stack>
      <Card sx={{ mb: 2 }}>
        <CardContent>
          <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1.5}>
            <Box>
              <Typography fontWeight={700}>حالة الدفعة: {advance.status}</Typography>
              <Typography variant="body2" color="text.secondary">تُسجّل الدفعة مباشرة، ويمكن تصحيحها أو حذفها وفق ملكية السجل ونطاق المشروع.</Typography>
            </Box>
          </Stack>
        </CardContent>
      </Card>
      {message && (
        <Alert severity={message.severity} sx={{ mb: 2 }}>
          {message.text}
        </Alert>
      )}
      <MainCard>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3}>
          <Box>
            <Typography color="text.secondary">قيمة الدفعة</Typography>
            <Typography variant="h3">{fmt(advance.amount)}</Typography>
          </Box>
          <Box>
            <Typography color="text.secondary">المخصص</Typography>
            <Typography variant="h3" color="primary">
              {fmt(advance.allocatedAmount)}
            </Typography>
          </Box>
          <Box>
            <Typography color="text.secondary">غير مخصص من الإيصال</Typography>
            <Typography variant="h3" color="success.main">
              {fmt(advance.unallocatedAmount)}
            </Typography>
          </Box>
          <Box>
            <Typography color="text.secondary">زيادة التخصيص</Typography>
            <Typography variant="h3" color="error.main">
              {fmt(advance.dueFromPayer)}
            </Typography>
          </Box>
        </Stack>
      </MainCard>
      {canAllocate && (
        <Card component="form" onSubmit={submit} sx={{ mt: 2 }}>
          <CardContent>
            <Typography variant="h3" sx={{ mb: 2 }}>
              توزيع جزء من الدفعة
            </Typography>
            <Stack spacing={2}>
              <FormControl fullWidth>
                <InputLabel>نوع التخصيص</InputLabel>
                <Select
                  value={targetType}
                  label="نوع التخصيص"
                  onChange={(e) => {
                    setTargetType(e.target.value);
                    setTargetId('');
                  }}
                >
                  <MenuItem value="movement">حركة موقع</MenuItem>
                  <MenuItem value="supplier">دفعة مورد</MenuItem>
                  <MenuItem value="category">بند حساب فقط</MenuItem>
                </Select>
              </FormControl>
              <FormControl fullWidth required>
                <InputLabel>بند الحساب</InputLabel>
                <Select value={accountCategory} label="بند الحساب" onChange={(e) => setAccountCategory(e.target.value)}>
                  {['الموقع', 'العرب', 'المقاول', 'المحطة', 'الحديد', 'المباني', 'المحارة', 'العزل', 'الكهرباء', 'السباكة', 'المكتب'].map(
                    (category) => (
                      <MenuItem key={category} value={category}>
                        {category}
                      </MenuItem>
                    )
                  )}
                </Select>
              </FormControl>
              {targetType !== 'category' && (
                <FormControl fullWidth required>
                  <InputLabel>{targetType === 'movement' ? 'حركة الموقع' : 'دفعة المورد'}</InputLabel>
                  <Select
                    value={targetId}
                    label={targetType === 'movement' ? 'حركة الموقع' : 'دفعة المورد'}
                    onChange={(e) => setTargetId(e.target.value)}
                  >
                    {targets.filter(item => item.kind === targetType).map(item => (
                      <MenuItem key={item.id} value={String(item.id)}>{item.description} · {fmt(item.remaining)}</MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
              <TextField required type="number" label="قيمة التخصيص" value={amount} onChange={(e) => setAmount(e.target.value)} />
              {targetRemaining !== null && <Alert severity="info">المتبقي على المستهدف: <strong>{fmt(targetRemaining)}</strong></Alert>}
              {targetWarning && <Alert severity="error">{targetWarning}</Alert>}
              {advanceWarning && <Alert severity="warning">{advanceWarning}</Alert>}
              <TextField label="ملاحظات" value={notes} onChange={(e) => setNotes(e.target.value)} />
              <Button type="submit" disabled={saving || !canSubmitAllocation} variant="contained" startIcon={<IconPlus size={18} />}>
                {saving ? 'جاري الحفظ...' : 'حفظ التوزيع'}
              </Button>
            </Stack>
          </CardContent>
        </Card>
      )}
      <Grid container spacing={2} sx={{ mt: 0.5 }}>
        <Grid size={{ xs: 12, md: 7 }}>
          <MainCard title="سجل التخصيصات">
            {allocations.map((item) => {
              const movement = targets.find(entry => entry.kind === 'movement' && entry.id === item.siteMovementId);
              const payment = targets.find(entry => entry.kind === 'supplier' && entry.id === item.supplierPaymentId);
              const label = movement
                ? `حركة موقع: ${movement.description}`
                : payment
                  ? `دفعة مورد: ${payment.description}`
                  : item.siteMovementId
                    ? `حركة موقع #${item.siteMovementId}`
                    : item.supplierPaymentId
                      ? `دفعة مورد #${item.supplierPaymentId}`
                      : `بند الحساب: ${item.accountCategory}`;
              return (
                <Card key={item.id} variant="outlined" sx={{ mb: 1 }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between">
                      <Box>
                        <Typography fontWeight={700}>{label}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {item.accountCategory} · {new Date(item.allocationDate).toLocaleDateString('ar-EG-u-nu-latn')} ·{' '}
                          {item.notes || 'بدون ملاحظات'}
                        </Typography>
                      </Box>
                      <Typography fontWeight={700} color="primary">
                        {fmt(item.amount)}
                      </Typography>
                    </Stack>
                  </CardContent>
                </Card>
              );
            })}
            {!allocations.length && <Typography color="text.secondary">لا توجد تخصيصات حتى الآن.</Typography>}
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <MainCard
            title="مرفقات الدفعة"
            secondary={
              <Stack direction="row" spacing={1} alignItems="center">
                <Typography variant="caption" color="text.secondary">
                  {attachments.length} ملف
                </Typography>
                <Button component="label" size="small" variant="contained" disabled={uploading}>
                  {uploading ? 'جاري الرفع...' : 'إضافة مرفق'}
                  <input
                    hidden
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx"
                    onChange={(event) => {
                      const file = event.target.files?.[0];
                      if (file) uploadAttachment(file);
                      event.target.value = '';
                    }}
                  />
                </Button>
              </Stack>
            }
          >
            <Stack spacing={1.25}>
              {attachments.map((file) => (
                <Card key={file.id} sx={{ border: '1px solid', borderColor: 'divider', boxShadow: 1 }}>
                  <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                    <Stack
                      direction={{ xs: 'column', sm: 'row' }}
                      justifyContent="space-between"
                      alignItems={{ xs: 'stretch', sm: 'center' }}
                      spacing={1.5}
                    >
                      <Stack direction="row" spacing={1} alignItems="center">
                        <IconFile size={19} />
                        <Box minWidth={0}>
                          <Typography variant="body2" fontWeight={700} noWrap>
                            {file.originalFileName}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {Math.ceil(file.sizeInBytes / 1024)} ك.ب · {new Date(file.uploadedAtUtc).toLocaleDateString('ar-EG-u-nu-latn')}
                          </Typography>
                        </Box>
                      </Stack>
                      <Stack direction="row" spacing={1} justifyContent="flex-end">
                        <Button size="small" variant="outlined" onClick={() => openPreview(file)} startIcon={<IconEye size={14} />}>
                          عرض
                        </Button>
                        <Button
                          size="small"
                          onClick={() => downloadEmgAttachment(file.id, file.originalFileName)}
                          startIcon={<IconDownload size={14} />}
                        >
                          تحميل
                        </Button>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
              {!attachments.length && <Typography color="text.secondary">لا توجد مرفقات.</Typography>}
            </Stack>
          </MainCard>
        </Grid>
      </Grid>
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>تعديل الدفعة</DialogTitle>
        <DialogContent dividers><Stack component="form" id="advance-edit-form" spacing={2} sx={{ pt: 1 }} onSubmit={saveEdit}>
          <TextField label="اسم العميل" value={editForm.clientName} onChange={(e) => setEditForm({ ...editForm, clientName: e.target.value })} />
          <TextField label="اسم الدافع" value={editForm.payerName} onChange={(e) => setEditForm({ ...editForm, payerName: e.target.value })} />
          <TextField required type="number" label="قيمة الدفعة" value={editForm.amount} onChange={(e) => setEditForm({ ...editForm, amount: e.target.value })} />
          <TextField required type="date" label="تاريخ الدفع" value={editForm.paymentDate} onChange={(e) => setEditForm({ ...editForm, paymentDate: e.target.value })} InputLabelProps={{ shrink: true }} />
          <TextField label="طريقة الدفع" value={editForm.paymentMethod} onChange={(e) => setEditForm({ ...editForm, paymentMethod: e.target.value })} />
          <TextField label="المرجع" value={editForm.reference} onChange={(e) => setEditForm({ ...editForm, reference: e.target.value })} />
          <TextField label="ملاحظات" multiline minRows={2} value={editForm.notes} onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })} />
        </Stack></DialogContent>
        <DialogActions><Button onClick={() => setEditOpen(false)}>إلغاء</Button><Button type="submit" form="advance-edit-form" variant="contained">حفظ</Button></DialogActions>
      </Dialog>
      <Dialog open={Boolean(previewFile)} onClose={closePreview} fullWidth maxWidth="lg">
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2 }}>
          {previewFile?.originalFileName}
          <Button onClick={closePreview}>إغلاق</Button>
        </DialogTitle>
        <DialogContent dividers sx={{ minHeight: { xs: 320, md: 560 }, display: 'grid', placeItems: 'center', bgcolor: 'grey.100' }}>
          {previewLoading && <CircularProgress />}
          {!previewLoading && previewUrl && isImage && (
            <Box
              component="img"
              src={previewUrl}
              alt={previewFile?.originalFileName}
              sx={{ maxWidth: '100%', maxHeight: '70vh', objectFit: 'contain', borderRadius: 1, boxShadow: 3 }}
            />
          )}
          {!previewLoading && previewUrl && isPdf && (
            <Box
              component="iframe"
              src={previewUrl}
              title={previewFile?.originalFileName}
              sx={{ width: '100%', height: '70vh', border: 0, bgcolor: 'background.paper' }}
            />
          )}
          {!previewLoading && previewUrl && !isImage && !isPdf && (
            <Stack alignItems="center" spacing={2}>
              <IconFile size={52} />
              <Typography>هذا النوع من الملفات لا يدعم المعاينة المباشرة.</Typography>
              <Button
                variant="contained"
                onClick={() => previewFile && downloadEmgAttachment(previewFile.id, previewFile.originalFileName)}
                startIcon={<IconDownload size={17} />}
              >
                تحميل الملف
              </Button>
            </Stack>
          )}
        </DialogContent>
      </Dialog>
    </Box>
  );
}
