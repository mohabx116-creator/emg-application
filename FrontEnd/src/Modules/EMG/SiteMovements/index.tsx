import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  Grid,
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
import { IconEye, IconPaperclip, IconReceipt, IconTrash } from '@tabler/icons-react';
import {
  createEmgSiteMovement,
  deleteEmgTransaction,
  EmgEntrySuggestion,
  EmgProject,
  EmgSiteMovement,
  EmgSupplier,
  getEmgProject,
  getEmgSiteMovements,
  getEmgSuppliers,
  suggestEmgEntry,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';
import WorkSectionShell, { formatWorkMoney, WorkSectionSkeleton, WorkStatusChip } from '../WorkSectionShell';

const today = new Date().toISOString().slice(0, 10);

type MovementForm = {
  movementType: string;
  description: string;
  unit: string;
  quantity: string;
  unitPrice: string;
  amount: string;
  movementDate: string;
  expenseCategory: string;
  paidTo: string;
  paymentMethod: string;
  reference: string;
  details: string;
  isPaid: boolean;
  supplierId: string;
};

const initialForm: MovementForm = {
  movementType: 'مصروفات',
  description: '',
  unit: '',
  quantity: '',
  unitPrice: '',
  amount: '',
  movementDate: today,
  expenseCategory: '',
  paidTo: '',
  paymentMethod: 'نقدي',
  reference: '',
  details: '',
  isPaid: true,
  supplierId: ''
};

export default function EmgSiteMovements() {
  const { projectId } = useParams();
  const { user } = useAuth();
  const canCreate = Boolean(user?.permissions?.includes('site-movements.create'));
  const id = Number(projectId);

  const [project, setProject] = useState<EmgProject | null>(null);
  const [movements, setMovements] = useState<EmgSiteMovement[]>([]);
  const [suppliers, setSuppliers] = useState<EmgSupplier[]>([]);
  const [form, setForm] = useState<MovementForm>(initialForm);
  const [attachment, setAttachment] = useState<File | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [message, setMessage] = useState<{ severity: 'success' | 'warning' | 'error'; text: string } | null>(null);
  const [suggestion, setSuggestion] = useState<EmgEntrySuggestion | null>(null);
  const [suggesting, setSuggesting] = useState(false);

  // Table search, filter & pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  const load = () => {
    setLoading(true);
    Promise.all([getEmgProject(id), getEmgSiteMovements(id), getEmgSuppliers().catch(() => [])])
      .then(([projectData, movementData, supplierData]) => {
        setProject(projectData);
        setMovements(movementData);
        setSuppliers(supplierData);
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل حركة الموقع.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (Number.isInteger(id)) load();
  }, [id]);

  useEffect(() => {
    if (!createOpen || form.description.trim().length < 3) {
      setSuggestion(null);
      return;
    }
    const timer = window.setTimeout(async () => {
      setSuggesting(true);
      try {
        const result = await suggestEmgEntry({
          projectId: id,
          description: form.description,
          movementType: form.movementType,
          unit: form.unit || null,
          supplierName: suppliers.find((item) => String(item.id) === form.supplierId)?.name || null,
          quantity: form.quantity ? Number(form.quantity) : null,
          unitPrice: form.unitPrice ? Number(form.unitPrice) : null,
          amount: form.amount ? Number(form.amount) : null
        });
        setSuggestion(result);
      } catch {
        setSuggestion(null);
      } finally {
        setSuggesting(false);
      }
    }, 450);
    return () => window.clearTimeout(timer);
  }, [createOpen, form.description, form.movementType, form.unit, form.quantity, form.unitPrice, form.amount, form.supplierId, suppliers, id]);

  const calculatedAmount = useMemo(() => {
    const quantity = Number(form.quantity);
    const unitPrice = Number(form.unitPrice);
    return quantity > 0 && unitPrice >= 0 ? quantity * unitPrice : Number(form.amount) || 0;
  }, [form.quantity, form.unitPrice, form.amount]);

  const update = (key: keyof MovementForm, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }));

  const applySuggestion = () => {
    if (!suggestion) return;
    const matchedSupplier = suggestion.matchedSupplierName ? suppliers.find((item) => item.name === suggestion.matchedSupplierName) : undefined;
    setForm((current) => ({
      ...current,
      movementType: suggestion.suggestedMovementType || current.movementType,
      expenseCategory: suggestion.suggestedExpenseCategory || current.expenseCategory,
      unit: suggestion.suggestedUnit || current.unit,
      supplierId: matchedSupplier ? String(matchedSupplier.id) : current.supplierId
    }));
    setMessage({ severity: 'success', text: 'تم تطبيق الاقتراح. راجع البيانات قبل التسجيل.' });
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const createdMovement = await createEmgSiteMovement({
        projectId: id,
        supplierId: form.supplierId ? Number(form.supplierId) : null,
        movementType: form.movementType,
        description: form.description,
        unit: form.unit || null,
        quantity: form.quantity ? Number(form.quantity) : null,
        unitPrice: form.unitPrice ? Number(form.unitPrice) : null,
        amount: form.amount ? Number(form.amount) : null,
        movementDate: form.movementDate,
        reference: form.reference || null,
        details: form.details || null,
        expenseCategory: form.movementType === 'مصروفات' ? form.expenseCategory : null,
        paidTo: form.paidTo || null,
        paymentMethod: form.paymentMethod || null,
        isPaid: form.isPaid
      });
      let resultMessage: { severity: 'success' | 'warning'; text: string } = {
        severity: 'success',
        text: 'تم تسجيل حركة الموقع بنجاح، وهي الآن في حالة مسودة للمراجعة.'
      };
      if (attachment) {
        try {
          await uploadEmgAttachment('site-movement', createdMovement.id, attachment);
        } catch {
          resultMessage = { severity: 'warning', text: 'تم تسجيل الحركة، لكن تعذر رفع المرفق. يمكنك رفعه من تفاصيل الحركة لاحقًا.' };
        }
      }
      setForm(initialForm);
      setAttachment(null);
      setSuggestion(null);
      setCreateOpen(false);
      setMessage(resultMessage);
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر تسجيل الحركة. راجع البيانات المطلوبة.' });
    } finally {
      setSaving(false);
    }
  };

  const deleteMovement = async (movement: EmgSiteMovement) => {
    if (!window.confirm('هل تريد حذف هذه الحركة نهائيًا؟')) return;
    setMessage(null);
    try {
      await deleteEmgTransaction('site-movement', movement.id);
      setMessage({ severity: 'success', text: 'تم حذف الحركة نهائيًا.' });
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر حذف الحركة.' });
    }
  };

  const availableStatuses = useMemo(() => Array.from(new Set(movements.map((m) => m.status).filter(Boolean))), [movements]);
  const totalAmount = useMemo(() => movements.reduce((sum, m) => sum + (m.totalAmount || 0), 0), [movements]);
  const approvedCount = useMemo(() => movements.filter((m) => m.status === 'معتمد' || m.status === 'مرحّل').length, [movements]);
  const pendingCount = movements.length - approvedCount;

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return movements.filter((m) => {
      if (statusFilter !== 'all' && m.status !== statusFilter) return false;
      if (!q) return true;
      return `${m.description} ${m.movementType} ${m.expenseCategory || ''} ${m.supplierName || ''} ${m.reference || ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [movements, search, statusFilter]);

  const paginated = useMemo(() => filtered.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage), [filtered, page, rowsPerPage]);

  useEffect(() => {
    setPage(0);
  }, [search, statusFilter]);

  if (!project && loading) return <WorkSectionSkeleton />;
  if (!project) return <Alert severity="error">المشروع غير موجود أو لا تملك صلاحية الوصول إليه.</Alert>;

  const createForm = (
    <Box component="form" onSubmit={submit}>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small">
            <InputLabel>نوع الحركة</InputLabel>
            <Select value={form.movementType} label="نوع الحركة" onChange={(event) => update('movementType', event.target.value)}>
              <MenuItem value="مصروفات">مصروفات</MenuItem>
              <MenuItem value="توريدات">توريدات</MenuItem>
              <MenuItem value="عمالة">عمالة</MenuItem>
              <MenuItem value="معدات">معدات</MenuItem>
              <MenuItem value="مستجدات">مستجدات</MenuItem>
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField
            fullWidth
            size="small"
            required
            type="date"
            label="تاريخ الحركة"
            value={form.movementDate}
            onChange={(event) => update('movementDate', event.target.value)}
            InputLabelProps={{ shrink: true }}
          />
        </Grid>
        <Grid size={12}>
          <TextField
            fullWidth
            size="small"
            required
            label="الوصف"
            value={form.description}
            onChange={(event) => update('description', event.target.value)}
          />
        </Grid>

        {suggesting && (
          <Grid size={12}>
            <Typography variant="caption" color="text.secondary">
              جاري تحليل وصف الحركة...
            </Typography>
          </Grid>
        )}
        {suggestion && (
          <Grid size={12}>
            <Card
              variant="outlined"
              sx={{
                borderRadius: 2,
                borderColor: suggestion.requiresConfirmation ? 'warning.main' : 'success.main',
                bgcolor: suggestion.requiresConfirmation ? 'rgba(217, 119, 6, 0.06)' : 'rgba(16, 185, 129, 0.06)'
              }}
            >
              <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                <Stack spacing={0.75}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography fontWeight={700}>اقتراح ذكي · {Math.round(suggestion.confidence * 100)}%</Typography>
                    <Button size="small" variant="outlined" onClick={applySuggestion}>
                      تطبيق الاقتراح
                    </Button>
                  </Stack>
                  <Typography variant="body2">{suggestion.explanation}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    القسم: <b>{suggestion.suggestedExpenseCategory}</b> · النوع: <b>{suggestion.suggestedEntryKind}</b> · الوحدة:{' '}
                    <b>{suggestion.suggestedUnit || 'غير محددة'}</b>
                  </Typography>
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        )}

        <Grid size={{ xs: 12, sm: 6 }}>
          <FormControl fullWidth size="small">
            <InputLabel>المورد أو المقاول</InputLabel>
            <Select value={form.supplierId} label="المورد أو المقاول" onChange={(event) => update('supplierId', event.target.value)}>
              <MenuItem value="">بدون مورد محدد</MenuItem>
              {suppliers
                .filter((supplier) => supplier.isActive)
                .map((supplier) => (
                  <MenuItem key={supplier.id} value={supplier.id}>
                    {supplier.name} · {supplier.supplierType || 'غير محدد'}
                  </MenuItem>
                ))}
            </Select>
          </FormControl>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField
            fullWidth
            size="small"
            required={form.movementType === 'مصروفات'}
            label="بند المصروف"
            value={form.expenseCategory}
            onChange={(event) => update('expenseCategory', event.target.value)}
            placeholder="مثال: نقل، أسمنت، عمالة"
          />
        </Grid>

        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" label="الوحدة" value={form.unit} onChange={(event) => update('unit', event.target.value)} />
        </Grid>
        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" type="number" label="الكمية" value={form.quantity} onChange={(event) => update('quantity', event.target.value)} />
        </Grid>
        <Grid size={{ xs: 4 }}>
          <TextField fullWidth size="small" type="number" label="سعر الوحدة" value={form.unitPrice} onChange={(event) => update('unitPrice', event.target.value)} />
        </Grid>

        <Grid size={{ xs: 12, sm: 6 }}>
          <TextField
            fullWidth
            size="small"
            type="number"
            label="المبلغ المباشر"
            value={form.amount}
            onChange={(event) => update('amount', event.target.value)}
            helperText="استخدمه إذا لم تكن الحركة كمية × سعر وحدة"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Box
            sx={{
              p: 1.5,
              borderRadius: 2,
              border: '1px solid',
              borderColor: 'primary.light',
              bgcolor: 'rgba(30, 136, 229, 0.06)'
            }}
          >
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
              الإجمالي المحسوب
            </Typography>
            <Typography variant="h3" color="primary.main" sx={{ fontWeight: 900, fontVariantNumeric: 'tabular-nums' }}>
              {formatWorkMoney(calculatedAmount)}
            </Typography>
          </Box>
        </Grid>

        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" label="دُفع إلى" value={form.paidTo} onChange={(event) => update('paidTo', event.target.value)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" label="طريقة الدفع" value={form.paymentMethod} onChange={(event) => update('paymentMethod', event.target.value)} />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <TextField fullWidth size="small" label="المرجع" value={form.reference} onChange={(event) => update('reference', event.target.value)} />
        </Grid>

        <Grid size={12}>
          <TextField fullWidth size="small" label="تفاصيل إضافية" multiline minRows={2} value={form.details} onChange={(event) => update('details', event.target.value)} />
        </Grid>

        <Grid size={12}>
          <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1.5}>
            <FormControlLabel control={<Checkbox checked={form.isPaid} onChange={(event) => update('isPaid', event.target.checked)} />} label="تم الدفع" />
            <Button component="label" size="small" variant="outlined" startIcon={<IconPaperclip size={16} />} sx={{ borderRadius: 2 }}>
              {attachment ? `المرفق: ${attachment.name}` : 'إرفاق فاتورة أو إثبات (اختياري)'}
              <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx" onChange={(event) => setAttachment(event.target.files?.[0] || null)} />
            </Button>
          </Stack>
        </Grid>

        <Grid size={12}>
          <Stack direction="row" justifyContent="flex-end" spacing={1.25} sx={{ pt: 1 }}>
            <Button onClick={() => setCreateOpen(false)} disabled={saving} color="inherit" sx={{ borderRadius: 2, fontWeight: 700 }}>
              إلغاء
            </Button>
            <Button
              type="submit"
              disabled={saving}
              variant="contained"
              color="secondary"
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <IconReceipt size={18} />}
              sx={{ borderRadius: 2, fontWeight: 700, px: 3 }}
            >
              {saving ? 'جاري الحفظ...' : 'تسجيل الحركة'}
            </Button>
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );

  return (
    <WorkSectionShell
      project={project}
      activeSection="movements"
      title="حركة الموقع اليومية"
      addButtonLabel="تسجيل حركة جديدة"
      canCreate={canCreate}
      onOpenCreate={() => setCreateOpen(true)}
      onRefresh={load}
      loading={loading}
      message={message}
      onCloseMessage={() => setMessage(null)}
      totalAmount={totalAmount}
      totalCount={movements.length}
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
      createModalTitle="تسجيل حركة موقع جديدة"
      onCloseCreateModal={() => !saving && setCreateOpen(false)}
      createModalContent={createForm}
    >
      <TableContainer sx={{ maxHeight: 580, overflowX: 'auto' }}>
        <Table stickyHeader size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 800 }}>التاريخ</TableCell>
              <TableCell sx={{ fontWeight: 800 }}>الوصف والبند</TableCell>
              <TableCell sx={{ fontWeight: 800 }}>النوع</TableCell>
              <TableCell sx={{ fontWeight: 800 }}>المورد / الدفع</TableCell>
              <TableCell align="right" sx={{ fontWeight: 800 }}>
                المبلغ
              </TableCell>
              <TableCell align="center" sx={{ fontWeight: 800 }}>
                الحالة
              </TableCell>
              <TableCell align="center" sx={{ fontWeight: 800 }}>
                الإجراءات
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {paginated.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 5 }}>
                  <Typography color="text.secondary" fontWeight={600}>
                    {search || statusFilter !== 'all' ? 'لا توجد حركات مطابقة للفلاتر الحالية.' : 'لا توجد حركات مسجلة حتى الآن.'}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              paginated.map((movement) => (
                <TableRow key={movement.id} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{new Date(movement.movementDate).toLocaleDateString('ar-EG')}</TableCell>
                  <TableCell sx={{ maxWidth: 320 }}>
                    <Typography variant="body2" fontWeight={700}>
                      {movement.description}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {movement.expenseCategory || 'بدون بند'}
                      {movement.reference ? ` · ${movement.reference}` : ''}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Chip size="small" variant="outlined" label={movement.movementType} sx={{ fontWeight: 600 }} />
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{movement.supplierName || 'بدون مورد'}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {movement.isPaid ? `مدفوع · ${movement.paymentMethod || 'نقدي'}` : 'غير مدفوع'}
                    </Typography>
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                    {formatWorkMoney(movement.totalAmount)}
                  </TableCell>
                  <TableCell align="center">
                    <WorkStatusChip status={movement.status} />
                  </TableCell>
                  <TableCell align="center" sx={{ whiteSpace: 'nowrap' }}>
                    <Stack direction="row" spacing={0.5} justifyContent="center">
                      <Button component={Link} to={`/emg/site-movements/${movement.id}`} size="small" startIcon={<IconEye size={14} />}>
                        التفاصيل
                      </Button>
                      <Button size="small" color="error" onClick={() => void deleteMovement(movement)} startIcon={<IconTrash size={14} />}>حذف</Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </WorkSectionShell>
  );
}
