import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Collapse,
  Divider,
  Drawer,
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
  ToggleButton,
  ToggleButtonGroup,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconCamera,
  IconExternalLink,
  IconEye,
  IconPaperclip,
  IconPlus,
  IconReceipt,
  IconTrash,
  IconUserPlus,
  IconX
} from '@tabler/icons-react';
import {
  createEmgSiteMovementBatch,
  createEmgSupplierInvoice,
  deleteEmgSupplierInvoice,
  createEmgSupplier,
  deleteEmgTransaction,
  EmgEntrySuggestion,
  EmgProject,
  EmgSiteMovement,
  EmgSupplier,
  getEmgProject,
  getEmgSiteMovements,
  getEmgContractors,
  EmgContractor,
  getEmgWorkCategories,
  createEmgWorkCategory,
  suggestEmgEntry,
  uploadEmgAttachment
} from 'api/emg';
import useAuth from 'hooks/useAuth';
import { invalidateEmgCache, useEmgSuppliersCache } from 'hooks/useEmgData';
import { formatEmgMoney, getLocalIsoDate } from 'utils/emgFormatting';
import WorkSectionShell, { WorkSectionSkeleton, WorkStatusChip } from '../WorkSectionShell';

type InvoiceLine = {
  id: string;
  description: string;
  unit: string;
  quantity: string;
  unitPrice: string;
  amount: string;
};

type PaymentMode = 'cash' | 'credit' | 'partial';

type InvoiceHeaderForm = {
  invoiceNumber: string;
  movementType: string;
  movementDate: string;
  expenseCategory: string;
  supplierId: string;
  contractorId: string;
  counterpartyType: 'مورد' | 'مقاول' | 'بدون طرف';
  currency: string;
  fundingSource: string;
  recipient: string;
  workCategory: string;
  paidTo: string;
  paymentMethod: string;
  paymentSource: string;
  paymentMode: PaymentMode;
  partialPaidAmount: string;
  details: string;
};

function generateInvoiceNumber(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  const seq = String(now.getHours()).padStart(2, '0') + String(now.getMinutes()).padStart(2, '0') + String(Math.floor(Math.random() * 90 + 10));
  return `INV-${y}${m}${d}-${seq}`;
}

function createEmptyLine(): InvoiceLine {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    description: '',
    unit: '',
    quantity: '',
    unitPrice: '',
    amount: ''
  };
}

function createInitialHeader(): InvoiceHeaderForm {
  return {
    invoiceNumber: generateInvoiceNumber(),
    movementType: 'مصروفات',
    movementDate: getLocalIsoDate(),
    expenseCategory: '',
    supplierId: '',
    contractorId: '',
    counterpartyType: 'بدون طرف',
    currency: 'EGP',
    fundingSource: '',
    recipient: '',
    workCategory: '',
    paidTo: '',
    paymentMethod: 'نقدي',
    paymentSource: 'مكتب',
    paymentMode: 'cash',
    partialPaidAmount: '',
    details: ''
  };
}

function computeLineTotal(line: InvoiceLine): number {
  const qty = Number(line.quantity);
  const price = Number(line.unitPrice);
  if (qty > 0 && price >= 0 && line.unitPrice !== '') return qty * price;
  const direct = Number(line.amount);
  return Number.isFinite(direct) && direct > 0 ? direct : 0;
}

export default function EmgSiteMovements() {
  const { projectId } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const theme = useTheme();
  const downMD = useMediaQuery(theme.breakpoints.down('md'));
  const { user } = useAuth();
  const canCreate = Boolean(user?.permissions?.includes('site-movements.create'));
  const canCreateSupplier = Boolean(user?.permissions?.includes('suppliers.create') || user?.permissions?.includes('suppliers.manage'));
  const id = Number(projectId);

  const { data: cachedSuppliers = [], mutate: mutateSuppliers } = useEmgSuppliersCache(true);

  const [project, setProject] = useState<EmgProject | null>(null);
  const [movements, setMovements] = useState<EmgSiteMovement[]>([]);
  const [contractors, setContractors] = useState<EmgContractor[]>([]);
  const [workCategories, setWorkCategories] = useState<string[]>([]);
  const [header, setHeader] = useState<InvoiceHeaderForm>(createInitialHeader);
  const [lines, setLines] = useState<InvoiceLine[]>([createEmptyLine()]);
  const [attachments, setAttachments] = useState<File[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [message, setMessage] = useState<{ severity: 'success' | 'warning' | 'error'; text: string } | null>(null);
  const [suggestion, setSuggestion] = useState<EmgEntrySuggestion | null>(null);
  const [suggesting, setSuggesting] = useState(false);

  // Inline supplier creation (preserves all unsaved form input!)
  const [inlineSupplierOpen, setInlineSupplierOpen] = useState(false);
  const [inlineSupplierName, setInlineSupplierName] = useState('');
  const [inlineSupplierType, setInlineSupplierType] = useState('خامات ومواد بناء');
  const [inlineSupplierPhone, setInlineSupplierPhone] = useState('');
  const [creatingSupplier, setCreatingSupplier] = useState(false);

  // Clickable invoice / transaction quick drawer
  const [drawerMovement, setDrawerMovement] = useState<EmgSiteMovement | null>(null);

  // Table search, filter & pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  const activeSuppliers = useMemo(() => cachedSuppliers.filter((s) => s.isActive), [cachedSuppliers]);
  const activeContractors = useMemo(() => contractors.filter((s) => s.isActive), [contractors]);

  const load = () => {
    setLoading(true);
    Promise.all([getEmgProject(id), getEmgSiteMovements(id)])
      .then(([projectData, movementData]) => {
        setProject(projectData);
        setMovements(movementData);
      })
      .catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل حركة الموقع.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (Number.isInteger(id)) load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => { getEmgContractors().then(setContractors).catch(() => setContractors([])); }, []);
  useEffect(() => {
    if (!Number.isInteger(id)) return;
    getEmgWorkCategories(id, 'الموقع').then((items) => setWorkCategories(items.map((item) => item.name))).catch(() => setWorkCategories([]));
  }, [id]);

  const ensureWorkCategory = async () => {
    const name = header.workCategory.trim();
    if (!name || workCategories.some((item) => item.toLocaleLowerCase() === name.toLocaleLowerCase())) return;
    try {
      const created = await createEmgWorkCategory({ projectId: id, name, classification: 'الموقع' });
      setWorkCategories((current) => [...current, created.name].sort((a, b) => a.localeCompare(b)));
    } catch {
      // The server remains authoritative; a duplicate or missing permission
      // must not block saving the operational record itself.
    }
  };

  // Auto-open entry sheet if navigated from bottom-nav "+" FAB or Engineer quick CTA
  useEffect(() => {
    if (canCreate && searchParams.get('quickAdd') === '1') {
      setCreateOpen(true);
      const next = new URLSearchParams(searchParams);
      next.delete('quickAdd');
      setSearchParams(next, { replace: true });
    }
  }, [canCreate, searchParams, setSearchParams]);

  // Smart suggestion based on first line description
  const primaryDescription = lines[0]?.description || '';
  useEffect(() => {
    if (!createOpen || primaryDescription.trim().length < 3) {
      setSuggestion(null);
      return;
    }
    const timer = window.setTimeout(async () => {
      setSuggesting(true);
      try {
        const result = await suggestEmgEntry({
          projectId: id,
          description: primaryDescription,
            movementType: header.movementType,
          unit: lines[0]?.unit || null,
          supplierName: activeSuppliers.find((item) => String(item.id) === header.supplierId)?.name || null,
          quantity: lines[0]?.quantity ? Number(lines[0].quantity) : null,
          unitPrice: lines[0]?.unitPrice ? Number(lines[0].unitPrice) : null,
          amount: lines[0]?.amount ? Number(lines[0].amount) : null
        });
        setSuggestion(result);
      } catch {
        setSuggestion(null);
      } finally {
        setSuggesting(false);
      }
    }, 500);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [createOpen, primaryDescription, header.movementType, header.supplierId, id]);

  const grandTotal = useMemo(() => lines.reduce((sum, line) => sum + computeLineTotal(line), 0), [lines]);

  const updateHeader = <K extends keyof InvoiceHeaderForm>(key: K, value: InvoiceHeaderForm[K]) => {
    setHeader((current) => ({ ...current, [key]: value }));
  };

  const updateLine = (lineId: string, key: keyof InvoiceLine, value: string) => {
    setLines((current) => current.map((line) => (line.id === lineId ? { ...line, [key]: value } : line)));
  };

  const addLine = () => setLines((current) => [...current, createEmptyLine()]);
  const removeLine = (lineId: string) => {
    setLines((current) => (current.length > 1 ? current.filter((l) => l.id !== lineId) : current));
  };

  const handleAddFiles = (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;
    const incoming = Array.from(fileList);
    setAttachments((prev) => [...prev, ...incoming]);
  };

  const handleCreateInlineSupplier = async () => {
    if (!inlineSupplierName.trim()) return;
    setCreatingSupplier(true);
    try {
      const created = await createEmgSupplier({
        name: inlineSupplierName.trim(),
        supplierType: inlineSupplierType.trim() || 'خامات ومواد بناء',
        phone: inlineSupplierPhone.trim() || null
      });
      await mutateSuppliers();
      await invalidateEmgCache('emg/suppliers');
      updateHeader('supplierId', String(created.id));
      setInlineSupplierName('');
      setInlineSupplierPhone('');
      setInlineSupplierOpen(false);
      setMessage({ severity: 'success', text: `تمت إضافة المورد "${created.name}" واختياره مع الحفاظ على بيانات الفاتورة.` });
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر إضافة المورد.' });
    } finally {
      setCreatingSupplier(false);
    }
  };

  const applySuggestion = () => {
    if (!suggestion) return;
    const matchedSupplier = suggestion.matchedSupplierName
      ? activeSuppliers.find((item) => item.name === suggestion.matchedSupplierName)
      : undefined;
    setHeader((current) => ({
      ...current,
      movementType: suggestion.suggestedMovementType || current.movementType,
      expenseCategory: suggestion.suggestedExpenseCategory || current.expenseCategory,
      supplierId: matchedSupplier ? String(matchedSupplier.id) : current.supplierId
    }));
    if (suggestion.suggestedUnit) {
      setLines((prev) => prev.map((l, idx) => (idx === 0 && !l.unit ? { ...l, unit: suggestion.suggestedUnit } : l)));
    }
  };

  const submitInvoice = async (event: FormEvent, keepOpen = false) => {
    event.preventDefault();
    const validLines = lines.filter((line) => line.description.trim().length > 0);
    if (validLines.length === 0) {
      setMessage({ severity: 'error', text: 'يرجى إدخال وصف لبند واحد على الأقل.' });
      return;
    }

    setSaving(true);
    setMessage(null);
    try {
      const paymentDetailsSuffix =
        header.paymentMode === 'partial' && header.partialPaidAmount
          ? ` | دفع جزئي مسجل: ${header.partialPaidAmount} ج.م`
          : header.paymentMode === 'credit'
            ? ' | آجل'
            : '';
      const combinedDetails = `${header.details.trim()}${paymentDetailsSuffix}`.trim() || null;

      const createdMovements: EmgSiteMovement[] = [];
      let attachmentEntityType = 'site-movement';
      let attachmentEntityId: number | null = null;
      if (header.supplierId) {
        const total = validLines.reduce((sum, line) => sum + computeLineTotal(line), 0);
        const initialPayment = header.paymentMode === 'cash'
          ? total
          : header.paymentMode === 'partial' ? Number(header.partialPaidAmount || 0) : 0;
        const invoice = await createEmgSupplierInvoice({
          supplierId: Number(header.supplierId), projectId: id, invoiceDate: header.movementDate,
          lines: validLines.map((line) => ({
            description: line.description.trim(), unit: line.unit.trim(), quantity: Number(line.quantity),
            unitPrice: Number(line.unitPrice), supplyType: header.expenseCategory || 'خامات'
          })),
          initialPayment, paymentMethod: header.paymentMode === 'credit' ? 'آجل' : header.paymentMethod || 'نقدي',
          paymentReference: header.invoiceNumber || null, notes: combinedDetails,
          movementDescription: header.details.trim() || (header.paymentSource === 'مالك مباشر' ? 'دفع فاتورة مباشرة من المالك' : validLines[0].description.trim()),
          paymentSource: header.paymentSource,
          expenseCategory: header.expenseCategory || null, paidTo: header.paidTo || null, details: combinedDetails
        });
        attachmentEntityType = 'supplier-invoice';
        attachmentEntityId = invoice.id;
        if (invoice.siteMovementId) {
          const created = await getEmgSiteMovements(id);
          const linked = created.find((movement) => movement.id === invoice.siteMovementId);
          if (linked) createdMovements.push(linked);
        }
      } else {
        const created = await createEmgSiteMovementBatch({ movements: validLines.map((line, i) => {
          const refCode = validLines.length > 1 ? `${header.invoiceNumber}-${i + 1}` : header.invoiceNumber;
          return {
            projectId: id, supplierId: header.counterpartyType === 'مورد' && header.supplierId ? Number(header.supplierId) : null,
            contractorId: header.counterpartyType === 'مقاول' && header.contractorId ? Number(header.contractorId) : null,
            counterpartyType: header.counterpartyType, currency: header.currency, fundingSource: header.fundingSource || null,
            recipient: header.recipient || null, workCategory: header.workCategory || null, movementType: header.movementType,
            description: line.description.trim(), unit: line.unit || null,
            quantity: line.quantity ? Number(line.quantity) : null, unitPrice: line.unitPrice ? Number(line.unitPrice) : null,
            amount: line.amount ? Number(line.amount) : null, movementDate: header.movementDate,
            reference: refCode || null, details: combinedDetails,
            expenseCategory: header.movementType === 'مصروفات' ? header.expenseCategory : null,
            paidTo: header.paidTo || null, paymentMethod: header.paymentMethod || 'نقدي', isPaid: header.paymentMode !== 'credit'
          };
        }) });
        createdMovements.push(...created);
        attachmentEntityId = created[0]?.id ?? null;
      }

      let uploadFailures = 0;
      if (attachments.length > 0 && attachmentEntityId) {
        for (const file of attachments) {
          try {
            await uploadEmgAttachment(attachmentEntityType, attachmentEntityId, file);
          } catch {
            uploadFailures += 1;
          }
        }
      }

      const countLabel = createdMovements.length > 1 ? `تم تسجيل ${createdMovements.length} بنود وترحيلها مباشرة بنجاح.` : 'تم تسجيل حركة الموقع وترحيلها مباشرة بنجاح.';
      setMessage(
        uploadFailures > 0
          ? { severity: 'warning', text: `${countLabel} تعذر رفع ${uploadFailures} مرفق؛ يمكنك إعادة الرفع من تفاصيل الحركة.` }
          : { severity: 'success', text: countLabel }
      );

      setLines([createEmptyLine()]);
      setAttachments([]);
      setSuggestion(null);
      if (keepOpen) {
        // Preserve sticky defaults (date, supplier, category, paymentMethod) for rapid entry
        setHeader((prev) => ({
          ...prev,
          invoiceNumber: generateInvoiceNumber(),
          partialPaidAmount: '',
          details: ''
        }));
      } else {
        setHeader(createInitialHeader());
        setCreateOpen(false);
      }
      load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر تسجيل الحركة. راجع البيانات المطلوبة.' });
    } finally {
      setSaving(false);
    }
  };

  const deleteMovement = async (movement: EmgSiteMovement) => {
    if (!window.confirm('هل تريد حذف هذه الحركة نهائيًا؟')) return;
    setMessage(null);
    try {
      if (movement.invoiceId) await deleteEmgSupplierInvoice(movement.invoiceId);
      else await deleteEmgTransaction('site-movement', movement.id);
      setMessage({ severity: 'success', text: 'تم حذف الحركة نهائيًا.' });
      if (drawerMovement?.id === movement.id) setDrawerMovement(null);
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

  const selectedSupplierObj = activeSuppliers.find((s) => String(s.id) === header.supplierId) || null;
  const selectedContractorObj = activeContractors.find((s) => String(s.id) === header.contractorId) || null;

  const createForm = (
    <Box component="form" onSubmit={(e) => void submitInvoice(e, false)}>
      <Stack spacing={2.25}>
        {/* Invoice Header */}
        <Grid container spacing={1.75}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <TextField
              fullWidth
              size="small"
              label="رقم الفاتورة (تلقائي)"
              value={header.invoiceNumber}
              helperText="يُنشئ النظام الرقم النهائي عند الحفظ"
              InputProps={{ readOnly: true }}
            />
          </Grid>
          <Grid size={{ xs: 6, sm: 4 }}>
            <FormControl fullWidth size="small">
              <InputLabel>نوع الحركة</InputLabel>
              <Select value={header.movementType} label="نوع الحركة" onChange={(e) => updateHeader('movementType', e.target.value)}>
                <MenuItem value="مصروفات">مصروفات</MenuItem>
                <MenuItem value="وارد">وارد</MenuItem>
                <MenuItem value="صادر">صادر</MenuItem>
                <MenuItem value="توريدات">توريدات</MenuItem>
                <MenuItem value="عمالة">عمالة</MenuItem>
                <MenuItem value="معدات">معدات</MenuItem>
                <MenuItem value="مستجدات">مستجدات</MenuItem>
              </Select>
            </FormControl>
          </Grid>
          <Grid size={{ xs: 6, sm: 4 }}>
            <TextField
              fullWidth
              size="small"
              required
              type="date"
              label="التاريخ"
              value={header.movementDate}
              onChange={(e) => updateHeader('movementDate', e.target.value)}
              InputLabelProps={{ shrink: true }}
            />
          </Grid>

          <Grid size={{ xs: 12, sm: 3 }}>
            <FormControl fullWidth size="small">
              <InputLabel>الطرف</InputLabel>
              <Select value={header.counterpartyType} label="الطرف" onChange={(e) => {
                const value = e.target.value as InvoiceHeaderForm['counterpartyType'];
                updateHeader('counterpartyType', value);
                if (value !== 'مورد') updateHeader('supplierId', '');
                if (value !== 'مقاول') updateHeader('contractorId', '');
              }}>
                <MenuItem value="بدون طرف">بدون طرف</MenuItem>
                <MenuItem value="مورد">مورد</MenuItem>
                <MenuItem value="مقاول">مقاول</MenuItem>
              </Select>
            </FormControl>
          </Grid>

          {/* Exclusive supplier/contractor selector */}
          <Grid size={{ xs: 12, sm: 7 }}>
            <Stack direction="row" spacing={1} alignItems="center">
              {header.counterpartyType === 'مورد' ? <Autocomplete
                fullWidth
                size="small"
                options={activeSuppliers}
                value={selectedSupplierObj}
                getOptionLabel={(option) => `${option.name}${option.supplierType ? ` · ${option.supplierType}` : ''}`}
                onChange={(_, val) => updateHeader('supplierId', val ? String(val.id) : '')}
                renderInput={(params) => <TextField {...params} label="المورد" placeholder="ابحث عن مورد..." />}
              /> : header.counterpartyType === 'مقاول' ? <Autocomplete
                fullWidth
                size="small"
                options={activeContractors}
                value={selectedContractorObj}
                getOptionLabel={(option) => option.name}
                onChange={(_, val) => updateHeader('contractorId', val ? String(val.id) : '')}
                renderInput={(params) => <TextField {...params} label="المقاول" placeholder="ابحث عن مقاول..." />}
              /> : <TextField fullWidth size="small" label="الطرف" value="بدون طرف" InputProps={{ readOnly: true }} />}
              {canCreateSupplier && (
                <Button
                  size="small"
                  variant="outlined"
                  onClick={() => setInlineSupplierOpen((prev) => !prev)}
                  startIcon={<IconUserPlus size={16} />}
                  sx={{ whiteSpace: 'nowrap', minHeight: 40, borderRadius: 2 }}
                >
                  مورد جديد
                </Button>
              )}
            </Stack>
          </Grid>

          <Grid size={{ xs: 12, sm: 5 }}>
            <TextField
              fullWidth
              size="small"
              required={header.movementType === 'مصروفات'}
              label="بند المصروف"
              value={header.expenseCategory}
              onChange={(e) => updateHeader('expenseCategory', e.target.value)}
              placeholder="مثال: نقل، أسمنت، عمالة"
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <FormControl fullWidth size="small"><InputLabel>مصدر الدفع</InputLabel>
              <Select value={header.paymentSource} label="مصدر الدفع" onChange={(e) => updateHeader('paymentSource', e.target.value)}>
                <MenuItem value="مكتب">المكتب</MenuItem><MenuItem value="مالك مباشر">دفع مباشر من المالك</MenuItem>
              </Select>
            </FormControl>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}><TextField fullWidth size="small" label="مصدر التمويل" value={header.fundingSource} onChange={(e) => updateHeader('fundingSource', e.target.value)} /></Grid>
          <Grid size={{ xs: 12, sm: 4 }}><TextField fullWidth size="small" label="المستلم" value={header.recipient} onChange={(e) => updateHeader('recipient', e.target.value)} /></Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Autocomplete
              freeSolo
              options={workCategories}
              value={header.workCategory}
              onInputChange={(_, value) => updateHeader('workCategory', value)}
              onChange={(_, value) => updateHeader('workCategory', typeof value === 'string' ? value : '')}
              renderInput={(params) => <TextField {...params} fullWidth size="small" label="التصنيف" onBlur={() => void ensureWorkCategory()} />}
            />
          </Grid>
        </Grid>

        {/* Inline Supplier Creation Panel — preserves all form inputs */}
        <Collapse in={inlineSupplierOpen}>
          <Card variant="outlined" sx={{ borderRadius: 2.5, bgcolor: 'action.hover' }}>
            <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 800, mb: 1.25 }}>
                إضافة مورد سريع (مع الاحتفاظ ببيانات الفاتورة الحالية)
              </Typography>
              <Grid container spacing={1.25} alignItems="center">
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="اسم المورد *"
                    value={inlineSupplierName}
                    onChange={(e) => setInlineSupplierName(e.target.value)}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 3 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="التصنيف"
                    value={inlineSupplierType}
                    onChange={(e) => setInlineSupplierType(e.target.value)}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 3 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="رقم الهاتف"
                    value={inlineSupplierPhone}
                    onChange={(e) => setInlineSupplierPhone(e.target.value)}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 2 }}>
                  <Button
                    fullWidth
                    variant="contained"
                    size="small"
                    disabled={creatingSupplier || !inlineSupplierName.trim()}
                    onClick={() => void handleCreateInlineSupplier()}
                    sx={{ minHeight: 40, fontWeight: 700 }}
                  >
                    {creatingSupplier ? '...' : 'إضافة'}
                  </Button>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Collapse>

        {/* Multi-Line Invoice Items */}
        <Box>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
              بنود الفاتورة ({lines.length})
            </Typography>
            <Button size="small" variant="outlined" startIcon={<IconPlus size={15} />} onClick={addLine} sx={{ borderRadius: 2, fontWeight: 700 }}>
              إضافة بند آخر
            </Button>
          </Stack>

          <Stack spacing={1.25}>
            {lines.map((line, index) => (
              <Card key={line.id} variant="outlined" sx={{ borderRadius: 2.5 }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                    <Chip size="small" label={`بند #${index + 1}`} color="primary" variant="outlined" sx={{ fontWeight: 700 }} />
                    <Stack direction="row" spacing={1} alignItems="center">
                      <Typography variant="subtitle2" color="primary.main" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums' }}>
                        {formatEmgMoney(computeLineTotal(line))}
                      </Typography>
                      {lines.length > 1 && (
                        <IconButton size="small" color="error" onClick={() => removeLine(line.id)} aria-label="حذف البند">
                          <IconX size={16} />
                        </IconButton>
                      )}
                    </Stack>
                  </Stack>

                  <Grid container spacing={1.25}>
                    <Grid size={{ xs: 12 }}>
                      <TextField
                        fullWidth
                        size="small"
                        required
                        label="وصف البند"
                        value={line.description}
                        onChange={(e) => updateLine(line.id, 'description', e.target.value)}
                      />
                    </Grid>
                    <Grid size={{ xs: 4, sm: 3 }}>
                      <TextField fullWidth size="small" label="الوحدة" value={line.unit} onChange={(e) => updateLine(line.id, 'unit', e.target.value)} />
                    </Grid>
                    <Grid size={{ xs: 4, sm: 3 }}>
                      <TextField
                        fullWidth
                        size="small"
                        type="number"
                        inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                        label="الكمية"
                        value={line.quantity}
                        onChange={(e) => updateLine(line.id, 'quantity', e.target.value)}
                      />
                    </Grid>
                    <Grid size={{ xs: 4, sm: 3 }}>
                      <TextField
                        fullWidth
                        size="small"
                        type="number"
                        inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                        label="سعر الوحدة"
                        value={line.unitPrice}
                        onChange={(e) => updateLine(line.id, 'unitPrice', e.target.value)}
                      />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 3 }}>
                      <TextField
                        fullWidth
                        size="small"
                        type="number"
                        inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                        label="أو مبلغ مباشر"
                        value={line.amount}
                        onChange={(e) => updateLine(line.id, 'amount', e.target.value)}
                      />
                    </Grid>
                  </Grid>
                </CardContent>
              </Card>
            ))}
          </Stack>
        </Box>

        {/* Smart Suggestion */}
        {suggesting && (
          <Typography variant="caption" color="text.secondary">
            جاري تحليل وصف البند...
          </Typography>
        )}
        {suggestion && (
          <Card variant="outlined" sx={{ borderRadius: 2, borderColor: 'success.main', bgcolor: 'rgba(16, 185, 129, 0.06)' }}>
            <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                <Box>
                  <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                    اقتراح ذكي · {Math.round(suggestion.confidence * 100)}%
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {suggestion.suggestedExpenseCategory} · {suggestion.suggestedEntryKind}
                  </Typography>
                </Box>
                <Button size="small" variant="outlined" onClick={applySuggestion}>
                  تطبيق
                </Button>
              </Stack>
            </CardContent>
          </Card>
        )}

        {/* Payment Mode (Cash / Credit / Partial) & Grand Total */}
        <Grid container spacing={1.75} alignItems="center">
          <Grid size={{ xs: 12, sm: 7 }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, display: 'block', mb: 0.75 }}>
              حالة السداد
            </Typography>
            <ToggleButtonGroup
              fullWidth
              exclusive
              size="small"
              value={header.paymentMode}
              onChange={(_, val: PaymentMode | null) => val && updateHeader('paymentMode', val)}
            >
              <ToggleButton value="cash" sx={{ fontWeight: 700 }}>
                نقدي (مدفوع)
              </ToggleButton>
              <ToggleButton value="credit" sx={{ fontWeight: 700 }}>
                آجل
              </ToggleButton>
              <ToggleButton value="partial" sx={{ fontWeight: 700 }}>
                دفع جزئي
              </ToggleButton>
            </ToggleButtonGroup>
          </Grid>
          <Grid size={{ xs: 12, sm: 5 }}>
            <Box sx={{ p: 1.5, borderRadius: 2.5, border: '1px solid', borderColor: 'primary.light', bgcolor: 'rgba(30, 136, 229, 0.06)' }}>
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                إجمالي الفاتورة ({lines.length} {lines.length === 1 ? 'بند' : 'بنود'})
              </Typography>
              <Typography variant="h3" color="primary.main" sx={{ fontWeight: 900, fontVariantNumeric: 'tabular-nums' }}>
                {formatEmgMoney(grandTotal)}
              </Typography>
            </Box>
          </Grid>

          {header.paymentMode === 'partial' && (
            <Grid size={{ xs: 12, sm: 6 }}>
              <TextField
                fullWidth
                size="small"
                type="number"
                inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                label="المبلغ المدفوع الآن (ج.م)"
                value={header.partialPaidAmount}
                onChange={(e) => updateHeader('partialPaidAmount', e.target.value)}
                helperText="يتم توثيق الجزء المسدد مع تفاصيل الحركة"
              />
            </Grid>
          )}
          <Grid size={{ xs: 12, sm: header.paymentMode === 'partial' ? 6 : 6 }}>
            <TextField fullWidth size="small" label="دُفع إلى" value={header.paidTo} onChange={(e) => updateHeader('paidTo', e.target.value)} />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <TextField
              fullWidth
              size="small"
              label="طريقة الدفع"
              value={header.paymentMethod}
              onChange={(e) => updateHeader('paymentMethod', e.target.value)}
              disabled={header.paymentMode === 'credit'}
            />
          </Grid>
          <Grid size={{ xs: 12 }}>
            <TextField fullWidth size="small" multiline minRows={2} label="ملاحظات إضافية" value={header.details} onChange={(e) => updateHeader('details', e.target.value)} />
          </Grid>
        </Grid>

        {/* Camera Capture + Multiple Optional Attachments */}
        <Box>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
            <Button component="label" variant="outlined" startIcon={<IconCamera size={18} />} sx={{ borderRadius: 2, minHeight: 42 }}>
              التقاط صورة الفاتورة بالكاميرا
              <input hidden type="file" accept="image/*" capture="environment" onChange={(e) => handleAddFiles(e.target.files)} />
            </Button>
            <Button component="label" variant="outlined" startIcon={<IconPaperclip size={18} />} sx={{ borderRadius: 2, minHeight: 42 }}>
              إرفاق ملفات (متعدد اختياري)
              <input
                hidden
                multiple
                type="file"
                accept=".pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx"
                onChange={(e) => handleAddFiles(e.target.files)}
              />
            </Button>
          </Stack>
          {attachments.length > 0 && (
            <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap sx={{ mt: 1.25 }}>
              {attachments.map((file, idx) => (
                <Chip
                  key={`${file.name}-${idx}`}
                  size="small"
                  label={file.name}
                  onDelete={() => setAttachments((prev) => prev.filter((_, i) => i !== idx))}
                />
              ))}
            </Stack>
          )}
        </Box>

        <Divider />

        {/* Submit Actions */}
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="flex-end" spacing={1.25}>
          <Button onClick={() => setCreateOpen(false)} disabled={saving} color="inherit" sx={{ borderRadius: 2, fontWeight: 700, minHeight: 44 }}>
            إلغاء
          </Button>
          <Button
            type="button"
            disabled={saving}
            variant="outlined"
            color="primary"
            onClick={(e) => void submitInvoice(e, true)}
            sx={{ borderRadius: 2, fontWeight: 700, minHeight: 44 }}
          >
            حفظ وإضافة حركة أخرى
          </Button>
          <Button
            type="submit"
            disabled={saving}
            variant="contained"
            color="secondary"
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <IconReceipt size={18} />}
            sx={{ borderRadius: 2, fontWeight: 700, px: 3, minHeight: 44 }}
          >
            {saving ? 'جاري الحفظ والترحيل...' : 'حفظ وترحيل الفاتورة'}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );

  return (
    <>
      <WorkSectionShell
        project={project}
        activeSection="movements"
        title="حركات الموقع اليومية"
        addButtonLabel="إضافة حركة / فاتورة"
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
        createModalTitle="تسجيل حركة موقع / فاتورة جديدة"
        onCloseCreateModal={() => !saving && setCreateOpen(false)}
        createModalContent={createForm}
      >
        {/* Mobile-First Transaction Cards (< md) — Zero Horizontal Scrolling */}
        {downMD ? (
          <Box sx={{ p: 1.5 }}>
            {paginated.length === 0 ? (
              <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center', fontWeight: 600 }}>
                {search || statusFilter !== 'all' ? 'لا توجد حركات مطابقة للفلاتر الحالية.' : 'لا توجد حركات مسجلة حتى الآن.'}
              </Typography>
            ) : (
              <Stack spacing={1.25}>
                {paginated.map((movement) => (
                  <Card key={movement.id} variant="outlined" sx={{ borderRadius: 2.5 }}>
                    <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                      <Stack spacing={1}>
                        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                          <Box sx={{ minWidth: 0 }}>
                            <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                              {movement.description}
                            </Typography>
                            <Stack direction="row" spacing={0.75} alignItems="center" flexWrap="wrap" useFlexGap sx={{ mt: 0.35 }}>
                              <Chip size="small" variant="outlined" label={movement.movementType} sx={{ height: 22, fontWeight: 700 }} />
                              {movement.reference && (
                                <Button
                                  size="small"
                                  variant="text"
                                  onClick={() => setDrawerMovement(movement)}
                                  sx={{ p: 0, minWidth: 0, fontWeight: 800, fontSize: '0.78rem' }}
                                >
                                  فاتورة #{movement.reference}
                                </Button>
                              )}
                            </Stack>
                          </Box>
                          <Typography variant="subtitle1" color="primary.main" sx={{ fontWeight: 900, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                            {formatEmgMoney(movement.totalAmount)}
                          </Typography>
                        </Stack>

                        <Stack direction="row" justifyContent="space-between" alignItems="center">
                          <Typography variant="caption" color="text.secondary">
                            {new Date(movement.movementDate).toLocaleDateString('ar-EG')} · {movement.supplierName || 'بدون مورد'} ·{' '}
                            {movement.isPaid ? movement.paymentMethod || 'نقدي' : 'آجل'}
                          </Typography>
                          <WorkStatusChip status={movement.status} />
                        </Stack>

                        <Stack direction="row" spacing={1} sx={{ pt: 0.5 }}>
                          <Button
                            fullWidth
                            size="small"
                            variant="outlined"
                            startIcon={<IconEye size={15} />}
                            onClick={() => setDrawerMovement(movement)}
                            sx={{ borderRadius: 2, minHeight: 36, fontWeight: 700 }}
                          >
                            عرض سريع
                          </Button>
                          <Button
                            component={Link}
                            to={`/emg/site-movements/${movement.id}`}
                            fullWidth
                            size="small"
                            variant="contained"
                            color="primary"
                            sx={{ borderRadius: 2, minHeight: 36, fontWeight: 700 }}
                          >
                            التفاصيل والتصحيح
                          </Button>
                        </Stack>
                      </Stack>
                    </CardContent>
                  </Card>
                ))}
              </Stack>
            )}
          </Box>
        ) : (
          /* Desktop Table */
          <TableContainer sx={{ maxHeight: 580, overflowX: 'auto' }}>
            <Table stickyHeader size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 800 }}>التاريخ</TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>رقم الفاتورة / المرجع</TableCell>
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
                    <TableCell colSpan={8} align="center" sx={{ py: 5 }}>
                      <Typography color="text.secondary" fontWeight={600}>
                        {search || statusFilter !== 'all' ? 'لا توجد حركات مطابقة للفلاتر الحالية.' : 'لا توجد حركات مسجلة حتى الآن.'}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ) : (
                  paginated.map((movement) => (
                    <TableRow key={movement.id} hover>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{new Date(movement.movementDate).toLocaleDateString('ar-EG')}</TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        <Button
                          size="small"
                          variant="text"
                          onClick={() => setDrawerMovement(movement)}
                          sx={{ fontWeight: 800, p: 0.25, minWidth: 0 }}
                        >
                          {movement.reference || `#${movement.id}`}
                        </Button>
                      </TableCell>
                      <TableCell sx={{ maxWidth: 300 }}>
                        <Typography variant="body2" fontWeight={700}>
                          {movement.description}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {movement.expenseCategory || 'بدون بند'}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Chip size="small" variant="outlined" label={movement.movementType} sx={{ fontWeight: 600 }} />
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{movement.supplierName || 'بدون مورد'}</Typography>
                        <Typography variant="caption" color="text.secondary">
                          {movement.isPaid ? `مدفوع · ${movement.paymentMethod || 'نقدي'}` : 'آجل'}
                        </Typography>
                      </TableCell>
                      <TableCell align="right" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                        {formatEmgMoney(movement.totalAmount)}
                      </TableCell>
                      <TableCell align="center">
                        <WorkStatusChip status={movement.status} />
                      </TableCell>
                      <TableCell align="center" sx={{ whiteSpace: 'nowrap' }}>
                        <Stack direction="row" spacing={0.5} justifyContent="center">
                          <Button component={Link} to={`/emg/site-movements/${movement.id}`} size="small" startIcon={<IconEye size={14} />}>
                            التفاصيل
                          </Button>
                          <Button size="small" color="error" onClick={() => void deleteMovement(movement)} startIcon={<IconTrash size={14} />}>
                            حذف
                          </Button>
                        </Stack>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </WorkSectionShell>

      {/* Clickable Invoice / Transaction Quick-Details Drawer */}
      <Drawer
        anchor={downMD ? 'bottom' : 'left'}
        open={Boolean(drawerMovement)}
        onClose={() => setDrawerMovement(null)}
        PaperProps={{
          sx: {
            width: downMD ? '100%' : 420,
            maxHeight: downMD ? '82vh' : '100vh',
            borderRadius: downMD ? '18px 18px 0 0' : 0,
            p: 2.5
          }
        }}
      >
        {drawerMovement && (
          <Stack spacing={2}>
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Box>
                <Typography variant="h4" sx={{ fontWeight: 850 }}>
                  تفاصيل الفاتورة / الحركة
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  مرجع: {drawerMovement.reference || `#${drawerMovement.id}`}
                </Typography>
              </Box>
              <IconButton onClick={() => setDrawerMovement(null)} aria-label="إغلاق">
                <IconX size={20} />
              </IconButton>
            </Stack>

            <Divider />

            <Box sx={{ p: 2, borderRadius: 2.5, bgcolor: 'action.hover' }}>
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                إجمالي الحركة
              </Typography>
              <Typography variant="h2" color="primary.main" sx={{ fontWeight: 900, fontVariantNumeric: 'tabular-nums' }}>
                {formatEmgMoney(drawerMovement.totalAmount)}
              </Typography>
              <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                <WorkStatusChip status={drawerMovement.status} />
                <Chip size="small" variant="outlined" label={drawerMovement.movementType} />
                <Chip size="small" variant="outlined" label={drawerMovement.isPaid ? drawerMovement.paymentMethod || 'نقدي' : 'آجل'} />
              </Stack>
            </Box>

            <Stack spacing={1.1}>
              {[
                ['الوصف', drawerMovement.description],
                ['البند', drawerMovement.expenseCategory || '—'],
                ['المورد', drawerMovement.supplierName || 'بدون مورد'],
                ['التاريخ', new Date(drawerMovement.movementDate).toLocaleDateString('ar-EG')],
                ['الكمية × السعر', drawerMovement.quantity ? `${drawerMovement.quantity} ${drawerMovement.unit || ''} × ${drawerMovement.unitPrice ?? 0}` : 'مبلغ مباشر'],
                ['دُفع إلى', drawerMovement.paidTo || '—'],
                ['التفاصيل', drawerMovement.details || '—']
              ].map(([label, val]) => (
                <Stack key={label} direction="row" justifyContent="space-between" spacing={2}>
                  <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 700 }}>
                    {label}
                  </Typography>
                  <Typography variant="body2" sx={{ fontWeight: 600, textAlign: 'end' }}>
                    {val}
                  </Typography>
                </Stack>
              ))}
            </Stack>

            <Divider />

            <Stack direction="row" spacing={1.25}>
              <Button
                component={Link}
                to={`/emg/site-movements/${drawerMovement.id}`}
                fullWidth
                variant="contained"
                startIcon={<IconExternalLink size={17} />}
                sx={{ borderRadius: 2, minHeight: 44, fontWeight: 700 }}
              >
                فتح الصفحة الكاملة والتصحيح
              </Button>
              <Button
                variant="outlined"
                color="error"
                onClick={() => void deleteMovement(drawerMovement)}
                sx={{ borderRadius: 2, minHeight: 44, fontWeight: 700 }}
              >
                حذف
              </Button>
            </Stack>
          </Stack>
        )}
      </Drawer>
    </>
  );
}
