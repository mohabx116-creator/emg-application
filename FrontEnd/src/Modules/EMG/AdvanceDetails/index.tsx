import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
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
import { IconArrowRight, IconCheck, IconDownload, IconEye, IconFile, IconPlus, IconSend, IconTransfer, IconX } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import {
  allocateEmgAdvance,
  downloadEmgAttachment,
  EmgAdvance,
  EmgAdvanceAllocation,
  EmgAttachment,
  EmgProjectReport,
  EmgWorkflowAudit,
  getEmgAdvance,
  getEmgAdvanceAllocations,
  getEmgAttachmentPreviewUrl,
  getEmgAttachments,
  getEmgProjectReport,
  getEmgWorkflowAudit,
  executeEmgWorkflow,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const fmt = (v: number) => `${money.format(v || 0)} ج.م`;

export default function AdvanceDetails() {
  const { advanceId } = useParams();
  const id = Number(advanceId);
  const { user } = useAuth();
  const canAllocate = user?.permissions?.includes('advances.allocate');
  const canSubmit = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('advances.submit');
  const canApprove = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('advances.approve');
  const canPost = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('advances.post');
  const [advance, setAdvance] = useState<EmgAdvance | null>(null);
  const [report, setReport] = useState<EmgProjectReport | null>(null);
  const [allocations, setAllocations] = useState<EmgAdvanceAllocation[]>([]);
  const [audit, setAudit] = useState<EmgWorkflowAudit | null>(null);
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
  const [workflowBusy, setWorkflowBusy] = useState(false);
  const load = () =>
    Promise.all([getEmgAdvance(id), getEmgAdvanceAllocations(id), getEmgAttachments('advance-payment', id).catch(() => []), getEmgWorkflowAudit('advance-payment', id).catch(() => null)])
      .then(async ([a, al, files, auditResult]) => {
        setAdvance(a);
        setAllocations(al);
        setAttachments(files);
        setAudit(auditResult);
        if (a) setReport(await getEmgProjectReport(a.projectId));
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل تفاصيل الدفعة.' }));
  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id]);
  const selectedMovement = useMemo(
    () => report?.movements.find((item) => item.id === Number(targetId)),
    [report?.movements, targetId]
  );
  const selectedSupplierPayment = useMemo(
    () => report?.supplierPayments.find((item) => item.id === Number(targetId)),
    [report?.supplierPayments, targetId]
  );
  const selectedTargetAmount = selectedMovement?.amount ?? selectedSupplierPayment?.amount ?? null;
  const selectedTargetAllocated = selectedTargetAmount === null ? 0 : allocations
    .filter((item) => selectedMovement ? item.siteMovementId === selectedMovement.id : selectedSupplierPayment ? item.supplierPaymentId === selectedSupplierPayment.id : false)
    .reduce((sum, item) => sum + item.amount, 0);
  const targetRemaining = selectedTargetAmount === null ? null : selectedTargetAmount - selectedTargetAllocated;
  const allocationAmount = Number(amount);
  const targetWarning = targetRemaining !== null && Number.isFinite(allocationAmount) && allocationAmount > targetRemaining
    ? `قيمة التخصيص أكبر من المتبقي على المستهدف بمقدار ${fmt(allocationAmount - targetRemaining)}.`
    : null;
  const availableAdvance = advance?.unallocatedAmount ?? 0;
  const advanceWarning = Number.isFinite(allocationAmount) && allocationAmount > availableAdvance
    ? `هذا التخصيص سيتجاوز المتاح في الدفعة بمقدار ${fmt(allocationAmount - availableAdvance)}. سيظهر المبلغ كمستحق على الدافع، وسيتم السماح به.`
    : null;
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
  const runWorkflow = async (actionName: string) => {
    setWorkflowBusy(true);
    setMessage(null);
    try {
      await executeEmgWorkflow('advance-payment', id, actionName);
      setMessage({ severity: 'success', text: `تم تنفيذ إجراء ${actionName} على الدفعة.` });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || `تعذر تنفيذ إجراء ${actionName}.` });
    } finally {
      setWorkflowBusy(false);
    }
  };
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!Number.isFinite(allocationAmount) || allocationAmount <= 0 || targetWarning) {
      setMessage({ severity: 'error', text: targetWarning || 'أدخل قيمة تخصيص أكبر من صفر.' });
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
        <Button component={Link} to="/emg/advances" endIcon={<IconArrowRight size={18} />} variant="outlined" sx={{ gap: 0.75 }}>
          كل الدفعات
        </Button>
      </Stack>
      <Card sx={{ mb: 2 }}>
        <CardContent>
          <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1.5}>
            <Box>
              <Typography fontWeight={700}>حالة الدفعة: {advance.status}</Typography>
              <Typography variant="body2" color="text.secondary">تمر الدفعة بمراحل التسجيل ثم المراجعة والاعتماد والترحيل قبل توزيعها.</Typography>
            </Box>
            <Stack direction="row" spacing={1} flexWrap="wrap">
              {canSubmit && (advance.status === 'مسودة' || advance.status === 'مرفوض') && <Button size="small" variant="contained" disabled={workflowBusy} onClick={() => void runWorkflow('إرسال')} startIcon={<IconSend size={15} />}>إرسال للمراجعة</Button>}
              {canApprove && advance.status === 'للمراجعة' && <><Button size="small" color="success" variant="contained" disabled={workflowBusy || Boolean(audit?.blockingChecks.length)} onClick={() => void runWorkflow('اعتماد')} startIcon={<IconCheck size={15} />}>اعتماد</Button><Button size="small" color="error" variant="outlined" disabled={workflowBusy} onClick={() => void runWorkflow('رفض')} startIcon={<IconX size={15} />}>رفض</Button></>}
              {canPost && advance.status === 'معتمد' && <Button size="small" color="secondary" variant="contained" disabled={workflowBusy || Boolean(audit?.blockingChecks.length)} onClick={() => void runWorkflow('ترحيل')} startIcon={<IconTransfer size={15} />}>ترحيل</Button>}
            </Stack>
          </Stack>
        </CardContent>
      </Card>
      {message && (
        <Alert severity={message.severity} sx={{ mb: 2 }}>
          {message.text}
        </Alert>
      )}
      {audit && (audit.blockingChecks.length > 0 || audit.warnings.length > 0) && (
        <Alert severity={audit.blockingChecks.length > 0 ? 'error' : 'warning'} sx={{ mb: 2 }}>
          <Typography fontWeight={700} sx={{ mb: 0.5 }}>نتيجة مراجعة الدفعة</Typography>
          {audit.blockingChecks.map((check) => <Typography key={`advance-block-${check}`} variant="body2">• {check}</Typography>)}
          {audit.warnings.map((warning) => <Typography key={`advance-warning-${warning}`} variant="body2">• تنبيه: {warning}</Typography>)}
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
            <Typography color="text.secondary">المتاح</Typography>
            <Typography variant="h3" color="success.main">
              {fmt(advance.unallocatedAmount)}
            </Typography>
          </Box>
          <Box>
            <Typography color="text.secondary">مستحق على الدافع</Typography>
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
                    {targetType === 'movement'
                      ? report?.movements.map((item) => (
                          <MenuItem key={item.id} value={item.id}>
                            {item.description} · {fmt(item.amount)}
                          </MenuItem>
                        ))
                      : report?.supplierPayments.map((item) => (
                          <MenuItem key={item.id} value={item.id}>
                            {item.supplierName} · {fmt(item.amount)}
                          </MenuItem>
                        ))}
                  </Select>
                </FormControl>
              )}
              <TextField required type="number" label="قيمة التخصيص" value={amount} onChange={(e) => setAmount(e.target.value)} />
              {targetRemaining !== null && <Alert severity="info">المتبقي على المستهدف: <strong>{fmt(targetRemaining)}</strong></Alert>}
              {targetWarning && <Alert severity="error">{targetWarning}</Alert>}
              {advanceWarning && <Alert severity="warning">{advanceWarning}</Alert>}
              <TextField label="ملاحظات" value={notes} onChange={(e) => setNotes(e.target.value)} />
              <Button type="submit" disabled={saving || !Number.isFinite(allocationAmount) || allocationAmount <= 0 || Boolean(targetWarning)} variant="contained" startIcon={<IconPlus size={18} />}>
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
              const movement = report?.movements.find((entry) => entry.id === item.siteMovementId);
              const payment = report?.supplierPayments.find((entry) => entry.id === item.supplierPaymentId);
              const label = movement
                ? `حركة موقع: ${movement.description}`
                : payment
                  ? `دفعة مورد: ${payment.supplierName}`
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
                          {item.accountCategory} · {new Date(item.allocationDate).toLocaleDateString('ar-EG')} ·{' '}
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
                            {Math.ceil(file.sizeInBytes / 1024)} ك.ب · {new Date(file.uploadedAtUtc).toLocaleDateString('ar-EG')}
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
