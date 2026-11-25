import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  Stack,
  TextField,
  Typography
} from '@mui/material';
import {
  IconArrowRight,
  IconCheck,
  IconDownload,
  IconEdit,
  IconEye,
  IconFile,
  IconSend,
  IconTransfer,
  IconTrash,
  IconX
} from '@tabler/icons-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import {
  deleteEmgSiteMovement,
  downloadEmgAttachment,
  EmgAttachment,
  EmgSiteMovement,
  EmgWorkflowAudit,
  EmgWorkflowResult,
  executeEmgWorkflow,
  getEmgAttachmentPreviewUrl,
  getEmgAttachments,
  getEmgSiteMovement,
  getEmgWorkflowHistory,
  getEmgWorkflowAudit,
  updateEmgSiteMovement,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;

export default function EmgSiteMovementDetails() {
  const { movementId } = useParams();
  const id = Number(movementId);
  const navigate = useNavigate();
  const { user } = useAuth();
  const canUpdate = user?.permissions?.includes('site-movements.update');
  const canSubmit = user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('site-movements.submit');
  const canReview =
    user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('site-movements.approve');
  const canPost =
    user?.permissions?.includes('workflow.execute') || user?.permissions?.includes('site-movements.post');
  const [movement, setMovement] = useState<EmgSiteMovement | null>(null);
  const [attachments, setAttachments] = useState<EmgAttachment[]>([]);
  const [history, setHistory] = useState<EmgWorkflowResult[]>([]);
  const [audit, setAudit] = useState<EmgWorkflowAudit | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ severity: 'success' | 'error'; text: string } | null>(null);
  const [previewFile, setPreviewFile] = useState<EmgAttachment | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [editForm, setEditForm] = useState({
    description: '',
    expenseCategory: '',
    amount: '',
    movementDate: '',
    paidTo: '',
    paymentMethod: '',
    reference: '',
    details: ''
  });

  const load = async () => {
    setLoading(true);
    try {
      const data = await getEmgSiteMovement(id);
      setMovement(data);
      setEditForm({
        description: data.description,
        expenseCategory: data.expenseCategory,
        amount: String(data.totalAmount),
        movementDate: data.movementDate.slice(0, 10),
        paidTo: data.paidTo,
        paymentMethod: data.paymentMethod,
        reference: data.reference,
        details: data.details
      });
      const [files, actions, auditResult] = await Promise.all([
        getEmgAttachments('site-movement', id).catch(() => []),
        getEmgWorkflowHistory('site-movement', id).catch(() => []),
        getEmgWorkflowAudit('site-movement', id).catch(() => null)
      ]);
      setAttachments(files);
      setHistory(actions);
      setAudit(auditResult);
    } catch {
      setMessage({ severity: 'error', text: 'تعذر تحميل تفاصيل الحركة.' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id]);

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
      await uploadEmgAttachment('site-movement', id, file);
      setMessage({ severity: 'success', text: 'تم رفع المرفق بنجاح.' });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر رفع المرفق.' });
    } finally {
      setUploading(false);
    }
  };

  const action = async (name: string) => {
    if (!movement) return;
    setBusy(true);
    setMessage(null);
    try {
      await executeEmgWorkflow('site-movement', movement.id, name);
      setMessage({ severity: 'success', text: `تم تنفيذ إجراء ${name} بنجاح.` });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'الإجراء غير مسموح للحالة الحالية أو لا تملك الصلاحية.' });
    } finally {
      setBusy(false);
    }
  };

  const saveEdit = async () => {
    if (!movement) return;
    setBusy(true);
    setMessage(null);
    try {
      await updateEmgSiteMovement(movement.id, {
        projectId: movement.projectId,
        supplierId: movement.supplierId,
        movementType: movement.movementType,
        description: editForm.description,
        unit: movement.unit || null,
        quantity: null,
        unitPrice: null,
        amount: Number(editForm.amount),
        movementDate: editForm.movementDate,
        reference: editForm.reference || null,
        details: editForm.details || null,
        expenseCategory: editForm.expenseCategory || null,
        paidTo: editForm.paidTo || null,
        paymentMethod: editForm.paymentMethod || null,
        isPaid: movement.isPaid
      });
      setEditOpen(false);
      setMessage({ severity: 'success', text: 'تم تعديل الحركة بنجاح.' });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر تعديل الحركة.' });
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!movement || !window.confirm('هل أنت متأكد من حذف هذه الحركة؟ لا يمكن التراجع عن الحذف.')) return;
    setBusy(true);
    setMessage(null);
    try {
      await deleteEmgSiteMovement(movement.id);
      navigate(`/emg/projects/${movement.projectId}/movements`);
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر حذف الحركة.' });
      setBusy(false);
    }
  };

  if (loading)
    return (
      <Box sx={{ p: 4, display: 'grid', placeItems: 'center' }}>
        <CircularProgress />
      </Box>
    );
  if (!movement) return <Alert severity="error">الحركة غير موجودة أو لا تملك صلاحية الوصول إليها.</Alert>;

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
          <Typography variant="h2">تفاصيل حركة الموقع</Typography>
          <Typography color="text.secondary">
            {movement.description} · {movement.projectName}
          </Typography>
        </Box>
        <Button
          component={Link}
          to={`/emg/projects/${movement.projectId}/movements`}
          variant="outlined"
          startIcon={<IconArrowRight size={18} />}
        >
          العودة للحركات
        </Button>
      </Stack>
      {message && (
        <Alert severity={message.severity} sx={{ mb: 2 }}>
          {message.text}
        </Alert>
      )}
      {audit && (audit.blockingChecks.length > 0 || audit.warnings.length > 0) && (
        <Alert severity={audit.blockingChecks.length > 0 ? 'error' : 'warning'} sx={{ mb: 2 }}>
          <Typography fontWeight={700} sx={{ mb: 0.5 }}>نتيجة المراجعة قبل الاعتماد</Typography>
          {audit.blockingChecks.map((check) => <Typography key={`block-${check}`} variant="body2">• {check}</Typography>)}
          {audit.warnings.map((warning) => <Typography key={`warn-${warning}`} variant="body2">• تنبيه: {warning}</Typography>)}
        </Alert>
      )}

      <MainCard
        title="بيانات الحركة"
        secondary={
          <Chip
            label={movement.status}
            color={
              movement.status === 'مرفوض' ? 'error' : movement.status === 'معتمد' || movement.status === 'مرحّل' ? 'success' : 'warning'
            }
          />
        }
      >
        <Grid container spacing={2}>
          {[
            ['نوع الحركة', movement.movementType],
            ['البند', movement.expenseCategory || '—'],
            ['المورد أو المقاول', movement.supplierName || '—'],
            ['تاريخ الحركة', new Date(movement.movementDate).toLocaleDateString('ar-EG')],
            ['الكمية', movement.quantity == null ? '—' : `${movement.quantity} ${movement.unit || ''}`],
            ['سعر الوحدة', movement.unitPrice == null ? '—' : formatMoney(movement.unitPrice)],
            ['الإجمالي', formatMoney(movement.totalAmount)],
            ['حالة الدفع', movement.isPaid ? `مدفوع · ${movement.paymentMethod || '—'}` : 'غير مدفوع'],
            ['دُفع إلى', movement.paidTo || '—'],
            ['المرجع', movement.reference || '—']
          ].map(([label, value]) => (
            <Grid key={label} size={{ xs: 12, sm: 6, md: 4 }}>
              <Typography variant="caption" color="text.secondary">
                {label}
              </Typography>
              <Typography fontWeight={600}>{value}</Typography>
            </Grid>
          ))}
          <Grid size={{ xs: 12 }}>
            <Typography variant="caption" color="text.secondary">
              التفاصيل
            </Typography>
            <Typography sx={{ whiteSpace: 'pre-wrap' }}>{movement.details || 'لا توجد تفاصيل إضافية.'}</Typography>
          </Grid>
        </Grid>
        <Divider sx={{ my: 2 }} />
        <Stack direction="row" spacing={1} flexWrap="wrap">
          {canSubmit && (movement.status === 'مسودة' || movement.status === 'مرفوض') && (
            <Button disabled={busy} onClick={() => action('إرسال')} startIcon={<IconSend size={16} />}>
              إرسال للمراجعة
            </Button>
          )}
          {canReview && movement.status === 'للمراجعة' && (
            <>
              <Button color="success" disabled={busy} onClick={() => action('اعتماد')} startIcon={<IconCheck size={16} />}>
                اعتماد
              </Button>
              <Button color="error" disabled={busy} onClick={() => action('رفض')} startIcon={<IconX size={16} />}>
                رفض
              </Button>
            </>
          )}
          {canPost && movement.status === 'معتمد' && (
            <Button color="secondary" disabled={busy} onClick={() => action('ترحيل')} startIcon={<IconTransfer size={16} />}>
              ترحيل
            </Button>
          )}
        </Stack>
        {canUpdate && (movement.status === 'مسودة' || movement.status === 'مرفوض') && (
          <>
            <Divider sx={{ my: 2 }} />
            <Stack direction="row" spacing={1}>
              <Button disabled={busy} onClick={() => setEditOpen(true)} startIcon={<IconEdit size={16} />}>
                تعديل
              </Button>
              <Button color="error" disabled={busy} onClick={remove} startIcon={<IconTrash size={16} />}>
                حذف
              </Button>
            </Stack>
          </>
        )}
      </MainCard>

      <Grid container spacing={2} sx={{ mt: 0.5 }}>
        <Grid size={{ xs: 12, md: 6 }}>
          <MainCard
            title="المرفقات"
            secondary={
              <Stack direction="row" spacing={1} alignItems="center">
                <Chip size="small" label={`${attachments.length} ملف`} variant="outlined" />
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
                      <Stack direction="row" spacing={1} alignItems="center" minWidth={0}>
                        <IconFile size={22} />
                        <Box minWidth={0}>
                          <Typography fontWeight={700} noWrap>
                            {file.originalFileName}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {Math.ceil(file.sizeInBytes / 1024)} ك.ب · {new Date(file.uploadedAtUtc).toLocaleString('ar-EG')}
                          </Typography>
                        </Box>
                      </Stack>
                      <Stack direction="row" spacing={1} justifyContent="flex-end">
                        <Button size="small" variant="outlined" onClick={() => openPreview(file)} startIcon={<IconEye size={15} />}>
                          عرض
                        </Button>
                        <Button
                          size="small"
                          onClick={() => downloadEmgAttachment(file.id, file.originalFileName)}
                          startIcon={<IconDownload size={15} />}
                        >
                          تحميل
                        </Button>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
              {attachments.length === 0 && <Typography color="text.secondary">لا توجد مرفقات لهذه الحركة.</Typography>}
            </Stack>
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <MainCard title="سجل الاعتماد">
            <Stack spacing={1.5}>
              {history.map((item) => (
                <Box key={`${item.actionDateUtc}-${item.action}`}>
                  <Typography fontWeight={600}>
                    {item.action} · {item.currentStatus}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    من {item.previousStatus} · {new Date(item.actionDateUtc).toLocaleString('ar-EG')}
                  </Typography>
                </Box>
              ))}
              {history.length === 0 && <Typography color="text.secondary">لم يتم تنفيذ إجراءات اعتماد حتى الآن.</Typography>}
            </Stack>
          </MainCard>
        </Grid>
      </Grid>
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>تعديل حركة الموقع</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              required
              label="الوصف"
              value={editForm.description}
              onChange={(event) => setEditForm({ ...editForm, description: event.target.value })}
            />
            <TextField
              label="بند المصروف"
              value={editForm.expenseCategory}
              onChange={(event) => setEditForm({ ...editForm, expenseCategory: event.target.value })}
            />
            <TextField
              required
              type="number"
              label="المبلغ"
              value={editForm.amount}
              onChange={(event) => setEditForm({ ...editForm, amount: event.target.value })}
            />
            <TextField
              required
              type="date"
              label="تاريخ الحركة"
              value={editForm.movementDate}
              onChange={(event) => setEditForm({ ...editForm, movementDate: event.target.value })}
              InputLabelProps={{ shrink: true }}
            />
            <TextField
              label="دُفع إلى"
              value={editForm.paidTo}
              onChange={(event) => setEditForm({ ...editForm, paidTo: event.target.value })}
            />
            <TextField
              label="طريقة الدفع"
              value={editForm.paymentMethod}
              onChange={(event) => setEditForm({ ...editForm, paymentMethod: event.target.value })}
            />
            <TextField
              label="المرجع"
              value={editForm.reference}
              onChange={(event) => setEditForm({ ...editForm, reference: event.target.value })}
            />
            <TextField
              label="تفاصيل إضافية"
              multiline
              minRows={2}
              value={editForm.details}
              onChange={(event) => setEditForm({ ...editForm, details: event.target.value })}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>إلغاء</Button>
          <Button variant="contained" disabled={busy} onClick={saveEdit}>
            حفظ التعديل
          </Button>
        </DialogActions>
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
