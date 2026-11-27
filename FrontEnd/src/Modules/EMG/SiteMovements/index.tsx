import useProjectWork from '../useProjectWork';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconExternalLink,
  IconEye,
  IconTrash,
  IconX
} from '@tabler/icons-react';
import {
  createEmgSiteMovementBatch,
  createEmgSupplierInvoice,
  deleteEmgSupplierInvoice,
  createEmgSupplier,
  deleteEmgTransaction,
  EmgEntrySuggestion,
  EmgSiteMovement,
  getEmgSiteMovements,
  getEmgContractors,
  createEmgContractor,
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
import NewTransactionModalForm, { InvoiceHeaderForm, InvoiceLine, PaymentMode } from './NewTransactionModalForm';

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

  const { project, projectError, records: movements, historical, warnings, loading, load } = useProjectWork(id, 'movements', getEmgSiteMovements);
  const [contractors, setContractors] = useState<EmgContractor[]>([]);
  const [workCategories, setWorkCategories] = useState<string[]>([]);
  const [header, setHeader] = useState<InvoiceHeaderForm>(createInitialHeader);
  const [lines, setLines] = useState<InvoiceLine[]>([createEmptyLine()]);
  const [attachments, setAttachments] = useState<File[]>([]);
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
  const [inlineContractorOpen, setInlineContractorOpen] = useState(false);
  const [inlineContractorName, setInlineContractorName] = useState('');
  const [inlineContractorPhone, setInlineContractorPhone] = useState('');
  const [creatingContractor, setCreatingContractor] = useState(false);

  // Clickable invoice / transaction quick drawer
  const [drawerMovement, setDrawerMovement] = useState<EmgSiteMovement | null>(null);

  // Table search, filter & pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  const activeSuppliers = useMemo(() => cachedSuppliers.filter((s) => s.isActive), [cachedSuppliers]);
  const activeContractors = useMemo(() => contractors.filter((s) => s.isActive), [contractors]);

  useEffect(() => {
    setPage(0);
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

  const handleCreateInlineContractor = async () => {
    if (!inlineContractorName.trim()) return;
    setCreatingContractor(true);
    try {
      const created = await createEmgContractor({
        name: inlineContractorName.trim(),
        phone: inlineContractorPhone.trim() || null,
        taxNumber: null,
        notes: null
      });
      setContractors((current) => [...current.filter((item) => item.id !== created.id), created]);
      updateHeader('contractorId', String(created.id));
      setInlineContractorName('');
      setInlineContractorPhone('');
      setInlineContractorOpen(false);
      setMessage({ severity: 'success', text: `تمت إضافة المقاول "${created.name}" واختياره مع الحفاظ على بيانات الحركة.` });
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر إضافة المقاول.' });
    } finally {
      setCreatingContractor(false);
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
          ? ` | دفع جزئي مسجل: ${header.partialPaidAmount} ${header.currency}`
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
          initialPayment, currency: header.currency, paymentMethod: header.paymentMode === 'credit' ? 'آجل' : header.paymentMethod || 'نقدي',
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
        const created = await createEmgSiteMovementBatch({
          movements: validLines.map((line, i) => {
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
          })
        });
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
  const totalAmount = useMemo(() => movements.filter(m => m.currency === 'EGP').reduce((sum, m) => sum + m.totalAmount, 0), [movements]);
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
  if (!project) return <Alert severity="error" action={<Button onClick={load}>إعادة المحاولة</Button>}>{projectError}</Alert>;

  const createForm = (
    <NewTransactionModalForm
      project={project}
      header={header}
      lines={lines}
      attachments={attachments}
      activeSuppliers={activeSuppliers}
      activeContractors={activeContractors}
      workCategories={workCategories}
      canCreateSupplier={canCreateSupplier}
      saving={saving}
      suggesting={suggesting}
      suggestion={suggestion}
      grandTotal={grandTotal}
      computeLineTotal={computeLineTotal}
      updateHeader={updateHeader}
      updateLine={updateLine}
      addLine={addLine}
      removeLine={removeLine}
      handleAddFiles={handleAddFiles}
      onRemoveAttachment={(idx) => setAttachments((prev) => prev.filter((_, i) => i !== idx))}
      ensureWorkCategory={ensureWorkCategory}
      applySuggestion={applySuggestion}
      onClose={() => !saving && setCreateOpen(false)}
      onSubmit={submitInvoice}
      inlineSupplierOpen={inlineSupplierOpen}
      setInlineSupplierOpen={setInlineSupplierOpen}
      inlineSupplierName={inlineSupplierName}
      setInlineSupplierName={setInlineSupplierName}
      inlineSupplierType={inlineSupplierType}
      setInlineSupplierType={setInlineSupplierType}
      inlineSupplierPhone={inlineSupplierPhone}
      setInlineSupplierPhone={setInlineSupplierPhone}
      creatingSupplier={creatingSupplier}
      onCreateInlineSupplier={handleCreateInlineSupplier}
      inlineContractorOpen={inlineContractorOpen}
      setInlineContractorOpen={setInlineContractorOpen}
      inlineContractorName={inlineContractorName}
      setInlineContractorName={setInlineContractorName}
      inlineContractorPhone={inlineContractorPhone}
      setInlineContractorPhone={setInlineContractorPhone}
      creatingContractor={creatingContractor}
      onCreateInlineContractor={handleCreateInlineContractor}
    />
  );

  return (
    <>
      <WorkSectionShell
        project={project}
        activeSection="movements"
        title="حركات الموقع اليومية"
        addButtonLabel="تسجيل حركة جديدة"
        canCreate={canCreate}
        onOpenCreate={() => setCreateOpen(true)}
        onRefresh={load}
        loading={loading}
        detailWarnings={warnings}
        message={message}
        onCloseMessage={() => setMessage(null)}
        historical={historical}
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
        createModalTitle="تسجيل حركة جديدة"
        onCloseCreateModal={() => !saving && setCreateOpen(false)}
        createModalContent={createForm}
        customModalLayout
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
                            {formatEmgMoney(movement.totalAmount, movement.currency)}
                          </Typography>
                        </Stack>

                        <Stack direction="row" justifyContent="space-between" alignItems="center">
                          <Typography variant="caption" color="text.secondary">
                            {new Date(movement.movementDate).toLocaleDateString('ar-EG-u-nu-latn')} · {movement.supplierName || 'بدون مورد'} ·{' '}
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
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>{new Date(movement.movementDate).toLocaleDateString('ar-EG-u-nu-latn')}</TableCell>
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
                        {formatEmgMoney(movement.totalAmount, movement.currency)}
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
                {formatEmgMoney(drawerMovement.totalAmount, drawerMovement.currency)}
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
                ['التاريخ', new Date(drawerMovement.movementDate).toLocaleDateString('ar-EG-u-nu-latn')],
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
