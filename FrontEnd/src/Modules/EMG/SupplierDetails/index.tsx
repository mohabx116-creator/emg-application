import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import HistoricalAccountDetails from '../HistoricalAccountDetails';
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
  Tab,
  Tabs,
  TextField,
  Typography
} from '@mui/material';
import { IconArrowRight, IconCash, IconPlus, IconReceipt, IconTrash, IconTruckDelivery } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import AttachmentGallery from 'ui-component/emg/AttachmentGallery';
import {
  correctEmgTransaction,
  deleteEmgTransaction,
  createEmgSupplierPayment,
  createEmgSupply,
  EmgProject,
  EmgSupplier,
  EmgSupplierPayment,
  EmgSupply,
  EmgWorkflowAudit,
  getEmgProjects,
  getEmgSupplier,
  getEmgSupplierPayments,
  getEmgSupplierSupplies,
  getEmgWorkflowAudit,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const fmt = (v: number) => `${money.format(v || 0)} ج.م`;
const today = new Date().toISOString().slice(0, 10);

type WorkflowTarget = { type: 'supplier-supply'; id: number; supply: EmgSupply } | { type: 'supplier-payment'; id: number };
type CorrectionFields = { supplyType: string; itemName: string; unit: string; quantity: string; unitPrice: string; invoiceNumber: string };
type PaymentCorrectionFields = { amount: string; paymentDate: string; paymentMethod: string; reference: string; notes: string };

export default function SupplierDetails() {
  const { supplierId } = useParams();
  const id = Number(supplierId);
  const { user } = useAuth();
  const canSupply = user?.permissions?.includes('suppliers.supplies-create');
  const canPay = user?.permissions?.includes('suppliers.payments-create');
  const [supplier, setSupplier] = useState<EmgSupplier | null>(null);
  const [supplies, setSupplies] = useState<EmgSupply[]>([]);
  const [payments, setPayments] = useState<EmgSupplierPayment[]>([]);
  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [tab, setTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [workflowTarget, setWorkflowTarget] = useState<WorkflowTarget | null>(null);
  const [workflowAudit, setWorkflowAudit] = useState<EmgWorkflowAudit | null>(null);
  const [workflowBusy, setWorkflowBusy] = useState(false);
  const [workflowError, setWorkflowError] = useState('');
  const [correction, setCorrection] = useState<CorrectionFields | null>(null);
  const [paymentCorrection, setPaymentCorrection] = useState<PaymentCorrectionFields | null>(null);
  const [message, setMessage] = useState<{ severity: 'success' | 'warning' | 'error'; text: string } | null>(null);
  const [supplyAttachment, setSupplyAttachment] = useState<File | null>(null);
  const [paymentAttachment, setPaymentAttachment] = useState<File | null>(null);
  const [attachmentTarget, setAttachmentTarget] = useState<{ type: string; id: number; title: string } | null>(null);
  const [supply, setSupply] = useState({
    projectId: '',
    supplyType: 'خامات',
    itemName: '',
    unit: 'قطعة',
    quantity: '',
    unitPrice: '',
    supplyDate: today,
    invoiceNumber: '',
    notes: ''
  });
  const [payment, setPayment] = useState({
    projectId: '',
    supplyId: '',
    amount: '',
    paymentDate: today,
    paymentMethod: 'نقدي',
    reference: '',
    notes: ''
  });

  const load = useCallback(() => {
    setLoading(true);
    Promise.all([getEmgSupplier(id), getEmgSupplierSupplies(id), getEmgSupplierPayments(id), getEmgProjects()])
      .then(([s, su, pa, pr]) => {
        setSupplier(s);
        setSupplies(su);
        setPayments(pa);
        setProjects(pr);
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل حساب المورد.' }))
      .finally(() => setLoading(false));
  }, [id]);
  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id, load]);
  useEffect(() => {
    if (!workflowTarget) return;
    let active = true;
    getEmgWorkflowAudit(workflowTarget.type, workflowTarget.id)
      .then(audit => { if (active) setWorkflowAudit(audit); })
      .catch(() => { if (active) setWorkflowError('تعذر تحميل الإجراءات المتاحة لهذه الحركة.'); });
    return () => { active = false; };
  }, [workflowTarget]);
  const supplyTotal = useMemo(() => {
    const quantity = Number(supply.quantity);
    const unitPrice = Number(supply.unitPrice);
    return Number.isFinite(quantity) && Number.isFinite(unitPrice) && quantity > 0 && unitPrice >= 0 ? quantity * unitPrice : 0;
  }, [supply.quantity, supply.unitPrice]);
  const selectedPaymentSupply = useMemo(
    () => supplies.find((item) => item.id === Number(payment.supplyId)),
    [supplies, payment.supplyId]
  );
  const paymentAmount = Number(payment.amount);
  const paymentWarning = selectedPaymentSupply && Number.isFinite(paymentAmount) && paymentAmount > selectedPaymentSupply.remainingAmount
    ? `الدفعة أكبر من المتبقي على التوريدة بمقدار ${fmt(paymentAmount - selectedPaymentSupply.remainingAmount)}. سيتم تسجيلها، وسيظهر الرصيد بالسالب كما هو مسموح.`
    : null;
  const duplicateInvoice = useMemo(() => {
    const invoice = supply.invoiceNumber.trim().toLowerCase();
    if (!invoice) return false;
    return supplies.some((item) => item.invoiceNumber.trim().toLowerCase() === invoice);
  }, [supplies, supply.invoiceNumber]);
  const duplicatePaymentReference = useMemo(() => {
    const reference = payment.reference.trim().toLowerCase();
    if (!reference) return false;
    return payments.some((item) => item.reference.trim().toLowerCase() === reference);
  }, [payments, payment.reference]);
  const submitSupply = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const created = await createEmgSupply(id, {
        ...supply,
        projectId: Number(supply.projectId),
        quantity: Number(supply.quantity),
        unitPrice: Number(supply.unitPrice)
      });
      let severity: 'success' | 'warning' = 'success';
      let text = 'تم تسجيل التوريدة.';
      if (supplyAttachment) {
        try {
          await uploadEmgAttachment('supplier-supply', created.id, supplyAttachment);
          text = 'تم تسجيل التوريدة ورفع المرفق.';
        } catch {
          severity = 'warning';
          text = 'تم تسجيل التوريدة، لكن تعذر رفع المرفق.';
        }
      }
      setMessage({ severity, text });
      setSupply({ ...supply, itemName: '', quantity: '', unitPrice: '', invoiceNumber: '', notes: '' });
      setSupplyAttachment(null);
      load();
    } catch (e: any) {
      setMessage({ severity: 'error', text: e?.message || 'تعذر تسجيل التوريدة.' });
    } finally {
      setSaving(false);
    }
  };
  const submitPayment = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    try {
      const created = await createEmgSupplierPayment(id, {
        projectId: payment.projectId ? Number(payment.projectId) : null,
        supplyId: payment.supplyId ? Number(payment.supplyId) : null,
        amount: Number(payment.amount),
        paymentDate: payment.paymentDate,
        paymentMethod: payment.paymentMethod,
        reference: payment.reference,
        notes: payment.notes
      });
      let severity: 'success' | 'warning' = 'success';
      let text = 'تم تسجيل الدفعة وتحديث حساب المورد. قد يظهر الرصيد بالسالب إذا كانت الدفعة أكبر من المستحق.';
      if (paymentAttachment) {
        try {
          await uploadEmgAttachment('supplier-payment', created.id, paymentAttachment);
          text = 'تم تسجيل الدفعة ورفع المرفق.';
        } catch {
          severity = 'warning';
          text = 'تم تسجيل الدفعة، لكن تعذر رفع المرفق.';
        }
      }
      setMessage({ severity, text });
      setPayment({ ...payment, amount: '', reference: '', notes: '' });
      setPaymentAttachment(null);
      load();
    } catch (e: any) {
      setMessage({ severity: 'error', text: e?.message || 'تعذر تسجيل الدفعة.' });
    } finally {
      setSaving(false);
    }
  };
  const openWorkflow = (target: WorkflowTarget) => {
    setWorkflowAudit(null);
    setWorkflowError('');
    setCorrection(null);
    setPaymentCorrection(null);
    setWorkflowTarget(target);
  };
  const saveCorrection = async (event: FormEvent) => {
    event.preventDefault();
    if (!correction || workflowTarget?.type !== 'supplier-supply' || workflowBusy) return;
    setWorkflowError('');
    setWorkflowBusy(true);
    try {
      await correctEmgTransaction('supplier-supply', workflowTarget.id, { ...correction, quantity: Number(correction.quantity), unitPrice: Number(correction.unitPrice) }, 'تصحيح مباشر');
      setWorkflowTarget(null);
      setMessage({ severity: 'success', text: 'تم حفظ التصحيح مباشرة.' });
      load();
    } catch (e: any) {
      setWorkflowError(e?.response?.data?.message || e?.message || 'تعذر حفظ التصحيح.');
    } finally {
      setWorkflowBusy(false);
    }
  };
  const savePaymentCorrection = async (event: FormEvent) => {
    event.preventDefault();
    if (!paymentCorrection || workflowTarget?.type !== 'supplier-payment' || workflowBusy) return;
    setWorkflowBusy(true); setWorkflowError('');
    try { await correctEmgTransaction('supplier-payment', workflowTarget.id, { ...paymentCorrection, amount: Number(paymentCorrection.amount) }, 'تصحيح مباشر'); setWorkflowTarget(null); setMessage({ severity: 'success', text: 'تم حفظ تصحيح الدفعة مباشرة.' }); load(); }
    catch (e: any) { setWorkflowError(e?.response?.data?.message || e?.message || 'تعذر حفظ تصحيح الدفعة.'); }
    finally { setWorkflowBusy(false); }
  };
  const remove = async (type: 'supplier-supply' | 'supplier-payment', entityId: number) => {
    if (!window.confirm('سيتم حذف السجل نهائيًا. هل تريد المتابعة؟')) return;
    try { await deleteEmgTransaction(type, entityId); setMessage({ severity: 'success', text: 'تم حذف السجل نهائيًا.' }); load(); }
    catch (e: any) { setMessage({ severity: 'error', text: e?.response?.data?.message || e?.message || 'تعذر حذف السجل لوجود اعتماديات.' }); }
  };

  if (loading && !supplier)
    return (
      <Box sx={{ minHeight: 400, display: 'grid', placeItems: 'center' }}>
        <CircularProgress />
      </Box>
    );
  if (!supplier) return <Alert severity="error">المورد غير موجود.</Alert>;
  return (
    <Box sx={{ pb: 4 }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={2}
        sx={{ mb: 2 }}
      >
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Box sx={{ p: 1.5, bgcolor: 'primary.lighter', color: 'primary.main', borderRadius: 2, display: 'flex' }}>
            <IconTruckDelivery />
          </Box>
          <Box>
            <Typography variant="h2">{supplier.name}</Typography>
            <Typography color="text.secondary">
              {supplier.supplierType} · {supplier.phone || 'بدون هاتف'}
            </Typography>
          </Box>
        </Stack>
        <Button component={Link} to="/emg/suppliers" endIcon={<IconArrowRight size={18} />} variant="outlined" sx={{ gap: 0.75 }}>
          كل الموردين
        </Button>
      </Stack>
      {message && (
        <Alert severity={message.severity} sx={{ mb: 2 }}>
          {message.text}
        </Alert>
      )}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="إجمالي التوريدات">
            <Typography variant="h3">{fmt(supplier.totalSupplies)}</Typography>
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="إجمالي المدفوع">
            <Typography variant="h3" color="success.main">
              {fmt(supplier.totalPaid)}
            </Typography>
          </MainCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <MainCard title="المتبقي">
            <Typography variant="h3" color="warning.main">
              {fmt(supplier.remaining)}
            </Typography>
          </MainCard>
        </Grid>
      </Grid>
      <MainCard>
        <Tabs value={tab} onChange={(_, value) => setTab(value)} variant="scrollable" allowScrollButtonsMobile>
          <Tab label="التوريدات" />
          <Tab label="المدفوعات" />
          {(canSupply || canPay) && <Tab label="إضافة حركة" />}
        </Tabs>
      </MainCard>
      {tab < 2 && (
        <MainCard sx={{ mt: 2 }} title={tab === 0 ? 'تفاصيل التوريدات' : 'تفاصيل المدفوعات'}>
          {tab === 0
            ? supplies.map((item) => (
                <Card key={item.id} variant="outlined" sx={{ mb: 1 }}>
                  <CardContent>
                    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2}>
                      <Box>
                        <Typography fontWeight={700}>{item.itemName}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {item.projectName} · {item.quantity} {item.unit} · {item.supplyType}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {new Date(item.supplyDate).toLocaleDateString('ar-EG')}
                          {item.invoiceNumber ? ` · فاتورة ${item.invoiceNumber}` : ''}
                        </Typography>
                      </Box>
                      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ xs: 'flex-start', sm: 'center' }}>
                        <Box>
                          <Typography variant="caption" color="text.secondary">
                            الإجمالي
                          </Typography>
                          <Typography fontWeight={700}>{fmt(item.totalAmount)}</Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" color="text.secondary">
                            المدفوع
                          </Typography>
                          <Typography color="success.main">{fmt(item.paidAmount)}</Typography>
                        </Box>
                        <Box>
                          <Typography variant="caption" color="text.secondary">المتبقي المعتمد</Typography>
                          <Typography fontWeight={700} color={item.remainingAmount < 0 ? 'error.main' : 'warning.main'}>
                            {item.status === 'معتمد' || item.status === 'مرحّل' ? fmt(item.remainingAmount) : '—'}
                          </Typography>
                        </Box>
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() =>
                            setAttachmentTarget({ type: 'supplier-supply', id: item.id, title: `مرفقات التوريدة · ${item.itemName}` })
                          }
                        >
                          المرفقات
                        </Button>
                        <Button size="small" onClick={() => openWorkflow({ type: 'supplier-supply', id: item.id, supply: item })}>تصحيح</Button>
                        <Button size="small" color="error" onClick={() => void remove('supplier-supply', item.id)} startIcon={<IconTrash size={15} />}>حذف</Button>
                        <Typography variant="caption" color={item.status === 'معتمد' || item.status === 'مرحّل' ? 'success.main' : 'warning.main'}>
                          {item.status}
                        </Typography>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              ))
            : payments.map((item) => (
                <Card key={item.id} variant="outlined" sx={{ mb: 1 }}>
                  <CardContent>
                    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2}>
                      <Box>
                        <Typography fontWeight={700}>{item.projectName || 'دفعة عامة'}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {item.paymentMethod} · {new Date(item.paymentDate).toLocaleDateString('ar-EG')} · {item.reference || 'بدون مرجع'}
                        </Typography>
                      </Box>
                      <Stack direction="row" spacing={1} alignItems="center">
                        <Typography fontWeight={700} color="success.main">
                          {fmt(item.amount)}
                        </Typography>
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() =>
                            setAttachmentTarget({
                              type: 'supplier-payment',
                              id: item.id,
                              title: `مرفقات دفعة المورد · ${item.projectName || 'دفعة عامة'}`
                            })
                          }
                        >
                          المرفقات
                        </Button>
                        <Button size="small" onClick={() => { openWorkflow({ type: 'supplier-payment', id: item.id }); setPaymentCorrection({ amount: String(item.amount), paymentDate: item.paymentDate.slice(0, 10), paymentMethod: item.paymentMethod, reference: item.reference, notes: item.notes }); }}>تعديل</Button>
                        <Button size="small" color="error" onClick={() => void remove('supplier-payment', item.id)} startIcon={<IconTrash size={15} />}>حذف</Button>
                        <Typography variant="caption" color={item.status === 'معتمد' || item.status === 'مرحّل' ? 'success.main' : 'warning.main'}>{item.status}</Typography>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
          <HistoricalAccountDetails type="suppliers" id={id} kind={tab === 0 ? 'works' : 'settlements'}
            emptyText={tab === 0 && !supplies.length ? 'لا توجد توريدات.' : tab === 1 && !payments.length ? 'لا توجد مدفوعات.' : undefined} />
        </MainCard>
      )}
      {tab === 2 && (
        <Grid container spacing={2} sx={{ mt: 0.5 }}>
          <Grid size={{ xs: 12, md: 6 }}>
            <MainCard title="تسجيل توريدة">
              <Box component="form" onSubmit={submitSupply}>
                <Stack spacing={2}>
                  <FormControl fullWidth required>
                    <InputLabel>المشروع</InputLabel>
                    <Select value={supply.projectId} label="المشروع" onChange={(e) => setSupply({ ...supply, projectId: e.target.value })}>
                      {projects.map((p) => (
                        <MenuItem key={p.id} value={p.id}>
                          {p.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                  <TextField
                    label="نوع التوريد"
                    value={supply.supplyType}
                    onChange={(e) => setSupply({ ...supply, supplyType: e.target.value })}
                  />
                  <TextField
                    required
                    label="اسم البند"
                    value={supply.itemName}
                    onChange={(e) => setSupply({ ...supply, itemName: e.target.value })}
                  />
                  <Grid container spacing={1}>
                    <Grid size={{ xs: 4 }}>
                      <TextField label="الوحدة" value={supply.unit} onChange={(e) => setSupply({ ...supply, unit: e.target.value })} />
                    </Grid>
                    <Grid size={{ xs: 4 }}>
                      <TextField
                        required
                        type="number"
                        label="الكمية"
                        value={supply.quantity}
                        onChange={(e) => setSupply({ ...supply, quantity: e.target.value })}
                      />
                    </Grid>
                    <Grid size={{ xs: 4 }}>
                      <TextField
                        required
                        type="number"
                        label="سعر الوحدة"
                        value={supply.unitPrice}
                        onChange={(e) => setSupply({ ...supply, unitPrice: e.target.value })}
                      />
                    </Grid>
                  </Grid>
                  <TextField
                    type="date"
                    label="تاريخ التوريد"
                    value={supply.supplyDate}
                    onChange={(e) => setSupply({ ...supply, supplyDate: e.target.value })}
                    InputLabelProps={{ shrink: true }}
                  />
                  <TextField
                    label="رقم الفاتورة"
                    value={supply.invoiceNumber}
                    onChange={(e) => setSupply({ ...supply, invoiceNumber: e.target.value })}
                  />
                  {duplicateInvoice && <Alert severity="error">رقم الفاتورة مستخدم بالفعل في توريدة سابقة لهذا المورد. راجع الرقم قبل الحفظ.</Alert>}
                  <Alert severity="info">إجمالي التوريدة المحسوب: <strong>{fmt(supplyTotal)}</strong></Alert>
                  <TextField label="ملاحظات" value={supply.notes} onChange={(e) => setSupply({ ...supply, notes: e.target.value })} />
                  <Button component="label" variant="outlined" startIcon={<IconReceipt size={17} />}>
                    {supplyAttachment ? `المرفق: ${supplyAttachment.name}` : 'إرفاق فاتورة التوريدة'}
                    <input
                      hidden
                      type="file"
                      accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx"
                      onChange={(e) => setSupplyAttachment(e.target.files?.[0] || null)}
                    />
                  </Button>
                  <Button type="submit" disabled={saving || duplicateInvoice || supplyTotal <= 0} variant="contained" startIcon={<IconPlus size={18} />}>
                    حفظ التوريدة
                  </Button>
                </Stack>
              </Box>
            </MainCard>
          </Grid>
          <Grid size={{ xs: 12, md: 6 }}>
            <MainCard title="تسجيل دفعة">
              <Box component="form" onSubmit={submitPayment}>
                <Stack spacing={2}>
                  <FormControl fullWidth>
                    <InputLabel>المشروع</InputLabel>
                    <Select
                      value={payment.projectId}
                      label="المشروع"
                      onChange={(e) => setPayment({ ...payment, projectId: e.target.value, supplyId: '' })}
                    >
                      <MenuItem value="">كل المشاريع</MenuItem>
                      {projects.map((p) => (
                        <MenuItem key={p.id} value={p.id}>
                          {p.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                  <FormControl fullWidth>
                    <InputLabel>التوريدة</InputLabel>
                    <Select
                      value={payment.supplyId}
                      label="التوريدة"
                      onChange={(e) => {
                        const supplyId = e.target.value;
                        const selected = supplies.find((item) => item.id === Number(supplyId));
                        setPayment({ ...payment, supplyId, projectId: selected ? String(selected.projectId) : payment.projectId });
                      }}
                    >
                      <MenuItem value="">بدون تحديد</MenuItem>
                      {supplies
                        .filter((s) => !payment.projectId || s.projectId === Number(payment.projectId))
                        .map((s) => (
                          <MenuItem key={s.id} value={s.id}>
                            {s.itemName} · {fmt(s.totalAmount)}
                          </MenuItem>
                        ))}
                    </Select>
                  </FormControl>
                  <TextField
                    required
                    type="number"
                    label="قيمة الدفعة"
                    value={payment.amount}
                    onChange={(e) => setPayment({ ...payment, amount: e.target.value })}
                  />
                  {paymentWarning && <Alert severity="warning">{paymentWarning}</Alert>}
                  <TextField
                    label="طريقة الدفع"
                    value={payment.paymentMethod}
                    onChange={(e) => setPayment({ ...payment, paymentMethod: e.target.value })}
                  />
                  <TextField
                    type="date"
                    label="تاريخ الدفع"
                    value={payment.paymentDate}
                    onChange={(e) => setPayment({ ...payment, paymentDate: e.target.value })}
                    InputLabelProps={{ shrink: true }}
                  />
                  <TextField
                    label="المرجع"
                    value={payment.reference}
                    onChange={(e) => setPayment({ ...payment, reference: e.target.value })}
                  />
                  {duplicatePaymentReference && <Alert severity="warning">مرجع التحويل أو الإيصال مستخدم بالفعل في دفعة سابقة لهذا المورد. راجع العملية قبل الحفظ.</Alert>}
                  <TextField label="ملاحظات" value={payment.notes} onChange={(e) => setPayment({ ...payment, notes: e.target.value })} />
                  <Button component="label" variant="outlined" startIcon={<IconReceipt size={17} />}>
                    {paymentAttachment ? `المرفق: ${paymentAttachment.name}` : 'إرفاق إيصال الدفع'}
                    <input
                      hidden
                      type="file"
                      accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx"
                      onChange={(e) => setPaymentAttachment(e.target.files?.[0] || null)}
                    />
                  </Button>
                  <Button type="submit" disabled={saving} variant="contained" color="success" startIcon={<IconCash size={18} />}>
                    حفظ الدفعة
                  </Button>
                </Stack>
              </Box>
            </MainCard>
          </Grid>
        </Grid>
      )}
      <Dialog open={Boolean(workflowTarget)} onClose={() => !workflowBusy && setWorkflowTarget(null)} fullWidth maxWidth="sm" aria-labelledby="supplier-workflow-title">
        <DialogTitle id="supplier-workflow-title">{workflowTarget?.type === 'supplier-payment' ? 'تعديل دفعة المورد' : 'تصحيح التوريدة'}</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2}>
            {workflowError && <Alert severity="error">{workflowError}</Alert>}
            {!workflowAudit && !workflowError && workflowTarget?.type === 'supplier-supply' && <CircularProgress size={24} aria-label="تحميل بيانات التصحيح" />}
            {workflowError && <Alert severity="warning">تعذر تحميل سجل الإجراءات. يمكنك متابعة التصحيح.</Alert>}
            {workflowTarget && (
              <>
                {workflowAudit && <Typography fontWeight={700}>الحالة الحالية: {workflowAudit.status}</Typography>}
                {workflowAudit?.rejectionReason && <Alert severity="warning">سبب الرفض: {workflowAudit.rejectionReason}</Alert>}
                {workflowAudit && workflowAudit.blockingChecks.length > 0 && <Alert severity="warning">{workflowAudit.blockingChecks.join('، ')}</Alert>}
                {workflowAudit?.warnings.map(warning => <Typography key={warning} variant="body2" color="text.secondary">{warning}</Typography>)}
                {paymentCorrection && workflowTarget?.type === 'supplier-payment' && (
                  <Stack component="form" id="supplier-payment-correction" spacing={1.5} onSubmit={savePaymentCorrection}>
                    <TextField required type="number" label="قيمة الدفع" value={paymentCorrection.amount} onChange={event => setPaymentCorrection({ ...paymentCorrection, amount: event.target.value })} />
                    <TextField required type="date" label="تاريخ الدفع" value={paymentCorrection.paymentDate} onChange={event => setPaymentCorrection({ ...paymentCorrection, paymentDate: event.target.value })} InputLabelProps={{ shrink: true }} />
                    <TextField label="طريقة الدفع" value={paymentCorrection.paymentMethod} onChange={event => setPaymentCorrection({ ...paymentCorrection, paymentMethod: event.target.value })} />
                    <TextField label="المرجع" value={paymentCorrection.reference} onChange={event => setPaymentCorrection({ ...paymentCorrection, reference: event.target.value })} />
                    <TextField label="ملاحظات" value={paymentCorrection.notes} onChange={event => setPaymentCorrection({ ...paymentCorrection, notes: event.target.value })} />
                  </Stack>
                )}
                {correction && workflowTarget?.type === 'supplier-supply' && (
                  <Stack component="form" id="supplier-supply-correction" spacing={1.5} onSubmit={saveCorrection}>
                    {([
                      ['itemName', 'بيان التوريدة'], ['supplyType', 'نوع التوريد'], ['unit', 'الوحدة'],
                      ['quantity', 'الكمية'], ['unitPrice', 'سعر الوحدة'], ['invoiceNumber', 'رقم الفاتورة']
                    ] as const).map(([field, label]) => (
                      <TextField key={field} label={label} value={correction[field]} required={field === 'itemName' || field === 'unit'}
                        onChange={event => setCorrection({ ...correction, [field]: event.target.value })} />
                    ))}
                  </Stack>
                )}
                {!correction && !paymentCorrection && <Typography color="text.secondary">لا توجد بيانات تصحيح متاحة.</Typography>}
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ flexWrap: 'wrap', gap: 1 }}>
          <Button onClick={() => setWorkflowTarget(null)} disabled={workflowBusy}>إغلاق</Button>
          {correction &&
            <Button type="submit" form="supplier-supply-correction" variant="contained" disabled={workflowBusy}>حفظ التصحيح</Button>}
          {paymentCorrection && <Button type="submit" form="supplier-payment-correction" variant="contained" disabled={workflowBusy}>حفظ التعديل</Button>}
          {workflowTarget?.type === 'supplier-supply' && !correction && <Button disabled={workflowBusy} onClick={() => { const { itemName, supplyType, unit, quantity, unitPrice, invoiceNumber } = workflowTarget.supply; setCorrection({ itemName, supplyType, unit, quantity: String(quantity), unitPrice: String(unitPrice), invoiceNumber }); }}>فتح نموذج التصحيح</Button>}
        </DialogActions>
      </Dialog>
      <Dialog open={Boolean(attachmentTarget)} onClose={() => setAttachmentTarget(null)} fullWidth maxWidth="md">
        <DialogTitle>{attachmentTarget?.title}</DialogTitle>
        <DialogContent dividers>
          {attachmentTarget && (
            <AttachmentGallery
              entityType={attachmentTarget.type}
              entityId={attachmentTarget.id}
              title="مستندات العملية"
              onMessage={setMessage}
            />
          )}
        </DialogContent>
      </Dialog>
    </Box>
  );
}
