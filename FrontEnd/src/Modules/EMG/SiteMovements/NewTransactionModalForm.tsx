import { FormEvent, useMemo, useState } from 'react';
import {
  Autocomplete,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Collapse,
  Divider,
  Grid,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconArrowDown,
  IconArrowUp,
  IconCamera,
  IconCheck,
  IconCircleCheck,
  IconCircleMinus,
  IconFileTypePdf,
  IconHelmet,
  IconPhoto,
  IconPlus,
  IconReceipt,
  IconTrash,
  IconTruckDelivery,
  IconUpload,
  IconUserPlus,
  IconX
} from '@tabler/icons-react';
import { EmgContractor, EmgEntrySuggestion, EmgProject, EmgSupplier } from 'api/emg';
import { formatFinancialAmount } from '../FinancialSummary';

export type InvoiceLine = {
  id: string;
  description: string;
  unit: string;
  quantity: string;
  unitPrice: string;
  amount: string;
};

export type PaymentMode = 'cash' | 'credit' | 'partial';

export type InvoiceHeaderForm = {
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

export const APPROVED_WORK_CATEGORIES = [
  'أعمال الحفر والردم',
  'أعمال الخرسانة العادية',
  'أعمال الخرسانة المسلحة',
  'أعمال الحدادة والنجارة',
  'أعمال المباني',
  'أعمال المحارة',
  'أعمال العزل',
  'أعمال تأسيس السباكة',
  'أعمال تأسيس الكهرباء',
  'أعمال السيراميك والأرضيات',
  'أعمال الدهانات',
  'توريد خامات ومواد بناء',
  'مصروفات ونثريات الموقع'
] as const;

const COMMON_UNITS = ['قطعة', 'متر', 'م2', 'م3', 'طن', 'شكارة', 'برميل', 'لفة', 'نقلة', 'يومية', 'ساعة', 'مقطوعية', 'جنيه'];
const ADD_NEW_SENTINEL = '__EMG_ADD_NEW__';

type PartyOption = { id: string; name: string; subtitle?: string; isAddNew?: boolean };

type NewTransactionModalFormProps = {
  project: EmgProject;
  header: InvoiceHeaderForm;
  lines: InvoiceLine[];
  attachments: File[];
  activeSuppliers: EmgSupplier[];
  activeContractors: EmgContractor[];
  workCategories: string[];
  canCreateSupplier: boolean;
  saving: boolean;
  suggesting: boolean;
  suggestion: EmgEntrySuggestion | null;
  grandTotal: number;
  computeLineTotal: (line: InvoiceLine) => number;
  updateHeader: <K extends keyof InvoiceHeaderForm>(key: K, value: InvoiceHeaderForm[K]) => void;
  updateLine: (lineId: string, key: keyof InvoiceLine, value: string) => void;
  addLine: () => void;
  removeLine: (lineId: string) => void;
  handleAddFiles: (files: FileList | null) => void;
  onRemoveAttachment: (index: number) => void;
  ensureWorkCategory: () => Promise<void>;
  applySuggestion: () => void;
  onClose: () => void;
  onSubmit: (event: FormEvent, keepOpen: boolean) => Promise<void>;
  // Inline supplier creation
  inlineSupplierOpen: boolean;
  setInlineSupplierOpen: (open: boolean) => void;
  inlineSupplierName: string;
  setInlineSupplierName: (val: string) => void;
  inlineSupplierType: string;
  setInlineSupplierType: (val: string) => void;
  inlineSupplierPhone: string;
  setInlineSupplierPhone: (val: string) => void;
  creatingSupplier: boolean;
  onCreateInlineSupplier: () => Promise<void>;
  // Inline contractor creation
  inlineContractorOpen: boolean;
  setInlineContractorOpen: (open: boolean) => void;
  inlineContractorName: string;
  setInlineContractorName: (val: string) => void;
  inlineContractorPhone: string;
  setInlineContractorPhone: (val: string) => void;
  creatingContractor: boolean;
  onCreateInlineContractor: () => Promise<void>;
};

export default function NewTransactionModalForm({
  project,
  header,
  lines,
  attachments,
  activeSuppliers,
  activeContractors,
  workCategories,
  canCreateSupplier,
  saving,
  suggesting,
  suggestion,
  grandTotal,
  computeLineTotal,
  updateHeader,
  updateLine,
  addLine,
  removeLine,
  handleAddFiles,
  onRemoveAttachment,
  ensureWorkCategory,
  applySuggestion,
  onClose,
  onSubmit,
  inlineSupplierOpen,
  setInlineSupplierOpen,
  inlineSupplierName,
  setInlineSupplierName,
  inlineSupplierType,
  setInlineSupplierType,
  inlineSupplierPhone,
  setInlineSupplierPhone,
  creatingSupplier,
  onCreateInlineSupplier,
  inlineContractorOpen,
  setInlineContractorOpen,
  inlineContractorName,
  setInlineContractorName,
  inlineContractorPhone,
  setInlineContractorPhone,
  creatingContractor,
  onCreateInlineContractor
}: NewTransactionModalFormProps) {
  const theme = useTheme();
  const isDark = theme.palette.mode === 'dark';
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  // Simple entry mode (single description + direct amount) vs multi-line invoice items
  const [entryMode, setEntryMode] = useState<'invoice' | 'simple'>('invoice');

  // Merged 13 approved default categories + server categories
  const mergedCategories = useMemo(() => {
    const set = new Set<string>([...APPROVED_WORK_CATEGORIES, ...workCategories.filter(Boolean)]);
    return Array.from(set);
  }, [workCategories]);

  // Supplier dropdown options with built-in "أخرى — إضافة جديد"
  const supplierOptions = useMemo<PartyOption[]>(() => {
    const base: PartyOption[] = activeSuppliers.map((s) => ({
      id: String(s.id),
      name: s.name,
      subtitle: s.supplierType || undefined
    }));
    if (canCreateSupplier) {
      base.push({ id: ADD_NEW_SENTINEL, name: 'أخرى — إضافة جديد', isAddNew: true });
    }
    return base;
  }, [activeSuppliers, canCreateSupplier]);

  // Contractor dropdown options with built-in "أخرى — إضافة جديد"
  const contractorOptions = useMemo<PartyOption[]>(() => {
    const base: PartyOption[] = activeContractors.map((c) => ({
      id: String(c.id),
      name: c.name
    }));
    if (canCreateSupplier) {
      base.push({ id: ADD_NEW_SENTINEL, name: 'أخرى — إضافة جديد', isAddNew: true });
    }
    return base;
  }, [activeContractors, canCreateSupplier]);

  const selectedSupplierOption = useMemo(
    () => supplierOptions.find((o) => !o.isAddNew && o.id === header.supplierId) || null,
    [supplierOptions, header.supplierId]
  );
  const selectedContractorOption = useMemo(
    () => contractorOptions.find((o) => !o.isAddNew && o.id === header.contractorId) || null,
    [contractorOptions, header.contractorId]
  );

  // Direction mapping: صادر <-> مصروفات, وارد <-> وارد
  const direction: 'outgoing' | 'incoming' = header.movementType === 'وارد' ? 'incoming' : 'outgoing';
  const handleDirectionChange = (next: 'outgoing' | 'incoming') => {
    updateHeader('movementType', next === 'incoming' ? 'وارد' : 'مصروفات');
  };

  // Live payment summary calculations (display only — canonical rules preserved)
  const paidAmount = useMemo(() => {
    if (header.paymentMode === 'cash') return grandTotal;
    if (header.paymentMode === 'credit') return 0;
    const partial = Number(header.partialPaidAmount || 0);
    return Number.isFinite(partial) && partial > 0 ? Math.min(partial, grandTotal) : 0;
  }, [header.paymentMode, header.partialPaidAmount, grandTotal]);

  const remainingAmount = Math.max(0, grandTotal - paidAmount);

  // Surface tokens aligned with Berry light & dark modes + approved mockups
  const surfaceBg = isDark ? '#0f172a' : '#ffffff';
  const cardBg = isDark ? '#162033' : '#f8fafc';
  const summaryBg = isDark ? '#17263f' : '#eff6ff';
  const summaryBorder = isDark ? 'rgba(59, 130, 246, 0.35)' : '#bfdbfe';
  const fieldBorder = isDark ? 'rgba(148, 163, 184, 0.24)' : 'rgba(15, 23, 42, 0.16)';
  const selectedCardBg = isDark ? '#1b3560' : '#eff6ff';
  const selectedCardBorder = '#2563eb';
  const pillInputSx = {
    '& .MuiOutlinedInput-root': {
      borderRadius: 99,
      bgcolor: isDark ? '#131d31' : '#ffffff',
      '& fieldset': { borderColor: fieldBorder },
      '&:hover fieldset': { borderColor: '#3b82f6' }
    }
  };

  const counterpartyCards: Array<{
    value: InvoiceHeaderForm['counterpartyType'];
    label: string;
    icon: React.ReactNode;
  }> = [
      { value: 'مورد', label: 'مورد', icon: <IconTruckDelivery size={22} stroke={1.8} /> },
      { value: 'مقاول', label: 'مقاول', icon: <IconHelmet size={22} stroke={1.8} /> },
      { value: 'بدون طرف', label: 'بدون طرف', icon: <IconCircleMinus size={22} stroke={1.8} /> }
    ];

  const handleSelectCounterparty = (value: InvoiceHeaderForm['counterpartyType']) => {
    updateHeader('counterpartyType', value);
    if (value !== 'مورد') {
      updateHeader('supplierId', '');
      setInlineSupplierOpen(false);
    }
    if (value !== 'مقاول') {
      updateHeader('contractorId', '');
      setInlineContractorOpen(false);
    }
  };

  const primaryLine = lines[0];

  return (
    <Box
      component="form"
      onSubmit={(e) => void onSubmit(e, false)}
      sx={{
        bgcolor: surfaceBg,
        color: 'text.primary',
        display: 'flex',
        flexDirection: 'column',
        minHeight: isMobile ? '100dvh' : 'auto'
      }}
    >
      {/* Approved Modal Header */}
      <Box
        sx={{
          px: { xs: 2, sm: 3 },
          py: 2,
          borderBottom: '1px solid',
          borderColor: 'divider',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 1.5
        }}
      >
        <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0 }}>
          <Box
            sx={{
              width: 38,
              height: 38,
              borderRadius: 2,
              display: 'grid',
              placeItems: 'center',
              bgcolor: isDark ? 'rgba(37, 99, 235, 0.2)' : 'rgba(37, 99, 235, 0.1)',
              color: '#3b82f6',
              flexShrink: 0
            }}
          >
            <IconReceipt size={20} />
          </Box>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="h4" sx={{ fontWeight: 850, lineHeight: 1.25 }}>
              تسجيل حركة جديدة
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 600 }} noWrap>
              {project.name}
            </Typography>
          </Box>
        </Stack>
        <IconButton onClick={onClose} disabled={saving} aria-label="إغلاق" size="small" sx={{ color: 'text.secondary' }}>
          <IconX size={20} />
        </IconButton>
      </Box>

      {/* Main Scrollable Form Body */}
      <Box sx={{ px: { xs: 2, sm: 3 }, py: 2.25, flex: 1, overflowY: 'auto' }}>
        <Stack spacing={2.25}>
          {/* Top Row: Transaction Date + Segmented Direction Control (صادر / وارد) */}
          <Grid container spacing={1.75} alignItems="flex-end">
            <Grid size={{ xs: 12, sm: 3 }}>
              <TextField select fullWidth size="small" label="العملة" value={header.currency} onChange={e => updateHeader('currency', e.target.value)} sx={pillInputSx} SelectProps={{ native: true }}>
                <option value="EGP">EGP</option><option value="USD">USD</option>
              </TextField>
            </Grid>
            <Grid size={{ xs: 12, sm: 7 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.75 }}>
                تاريخ الحركة
              </Typography>
              <TextField
                fullWidth
                size="small"
                required
                type="date"
                value={header.movementDate}
                onChange={(e) => updateHeader('movementDate', e.target.value)}
                sx={pillInputSx}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 5 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.75 }}>
                نوع الحركة
              </Typography>
              <Box
                role="radiogroup"
                aria-label="نوع الحركة"
                sx={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: 0.75,
                  p: 0.5,
                  borderRadius: 99,
                  bgcolor: cardBg,
                  border: '1px solid',
                  borderColor: fieldBorder
                }}
              >
                <Button
                  type="button"
                  size="small"
                  onClick={() => handleDirectionChange('outgoing')}
                  startIcon={<IconArrowUp size={15} />}
                  sx={{
                    borderRadius: 99,
                    minHeight: 36,
                    fontWeight: 800,
                    bgcolor: direction === 'outgoing' ? (isDark ? '#25324a' : '#1e293b') : 'transparent',
                    color: direction === 'outgoing' ? '#ffffff' : 'text.secondary',
                    '&:hover': {
                      bgcolor: direction === 'outgoing' ? (isDark ? '#25324a' : '#1e293b') : 'action.hover'
                    }
                  }}
                >
                  صادر
                </Button>
                <Button
                  type="button"
                  size="small"
                  onClick={() => handleDirectionChange('incoming')}
                  startIcon={<IconArrowDown size={15} />}
                  sx={{
                    borderRadius: 99,
                    minHeight: 36,
                    fontWeight: 800,
                    bgcolor: direction === 'incoming' ? '#15803d' : 'transparent',
                    color: direction === 'incoming' ? '#ffffff' : 'text.secondary',
                    '&:hover': {
                      bgcolor: direction === 'incoming' ? '#15803d' : 'action.hover'
                    }
                  }}
                >
                  وارد
                </Button>
              </Box>
            </Grid>
          </Grid>

          {/* Counterparty Type: 3 Prominent Selectable Cards in One Row */}
          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.85 }}>
              نوع الطرف
            </Typography>
            <Grid container spacing={1.25}>
              {counterpartyCards.map((item) => {
                const selected = header.counterpartyType === item.value;
                return (
                  <Grid key={item.value} size={4}>
                    <Box
                      role="button"
                      tabIndex={0}
                      aria-pressed={selected}
                      onClick={() => handleSelectCounterparty(item.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          handleSelectCounterparty(item.value);
                        }
                      }}
                      sx={{
                        cursor: 'pointer',
                        borderRadius: 2.5,
                        py: { xs: 1.4, sm: 1.75 },
                        px: 1,
                        border: '1.5px solid',
                        borderColor: selected ? selectedCardBorder : fieldBorder,
                        bgcolor: selected ? selectedCardBg : cardBg,
                        color: selected ? (isDark ? '#ffffff' : '#1d4ed8') : 'text.primary',
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: 0.6,
                        position: 'relative',
                        transition: 'all 150ms ease',
                        userSelect: 'none',
                        '&:hover': {
                          borderColor: selected ? selectedCardBorder : '#3b82f6'
                        }
                      }}
                    >
                      <Box sx={{ color: selected ? '#3b82f6' : 'text.secondary', display: 'flex' }}>{item.icon}</Box>
                      <Typography variant="subtitle2" sx={{ fontWeight: 800, fontSize: { xs: '0.82rem', sm: '0.9rem' } }}>
                        {item.label}
                      </Typography>
                      {selected && (
                        <Box
                          sx={{
                            position: 'absolute',
                            top: 6,
                            left: 6,
                            color: '#3b82f6',
                            display: 'flex'
                          }}
                        >
                          <IconCircleCheck size={16} />
                        </Box>
                      )}
                    </Box>
                  </Grid>
                );
              })}
            </Grid>
          </Box>

          {/* Work Category + Mutually Exclusive Supplier/Contractor Dropdown (Side-by-side on Desktop, Stacked on Tablet/Mobile) */}
          <Grid container spacing={1.5}>
            <Grid size={{ xs: 12, md: header.counterpartyType === 'بدون طرف' ? 12 : 6 }}>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.65 }}>
                تصنيف الأعمال
              </Typography>
              <Autocomplete
                freeSolo
                fullWidth
                size="small"
                options={mergedCategories}
                value={header.workCategory || header.expenseCategory || ''}
                onInputChange={(_, value) => {
                  updateHeader('workCategory', value);
                  updateHeader('expenseCategory', value);
                }}
                onChange={(_, value) => {
                  const next = typeof value === 'string' ? value : '';
                  updateHeader('workCategory', next);
                  updateHeader('expenseCategory', next);
                }}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    placeholder="اختر تصنيف الأعمال أو اكتب تصنيفاً جديداً..."
                    onBlur={() => void ensureWorkCategory()}
                    sx={pillInputSx}
                  />
                )}
              />
            </Grid>

            {header.counterpartyType === 'مورد' && (
              <Grid size={{ xs: 12, md: 6 }}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.65 }}>
                  المورد
                </Typography>
                <Autocomplete
                  fullWidth
                  size="small"
                  options={supplierOptions}
                  value={selectedSupplierOption}
                  getOptionLabel={(option) => (option.subtitle ? `${option.name} · ${option.subtitle}` : option.name)}
                  isOptionEqualToValue={(a, b) => a.id === b.id}
                  onChange={(_, val) => {
                    if (val?.isAddNew) {
                      setInlineSupplierOpen(true);
                      return;
                    }
                    updateHeader('supplierId', val ? val.id : '');
                  }}
                  renderOption={(props, option) => {
                    const { key, ...rest } = props;
                    return (
                      <li key={key} {...rest}>
                        {option.isAddNew ? (
                          <Stack direction="row" spacing={1} alignItems="center" sx={{ color: 'primary.main', fontWeight: 800 }}>
                            <IconUserPlus size={16} />
                            <span>{option.name}</span>
                          </Stack>
                        ) : (
                          <Box>
                            <Typography variant="body2" sx={{ fontWeight: 700 }}>
                              {option.name}
                            </Typography>
                            {option.subtitle && (
                              <Typography variant="caption" color="text.secondary">
                                {option.subtitle}
                              </Typography>
                            )}
                          </Box>
                        )}
                      </li>
                    );
                  }}
                  renderInput={(params) => <TextField {...params} placeholder="ابحث عن المورد..." sx={pillInputSx} />}
                />
              </Grid>
            )}

            {header.counterpartyType === 'مقاول' && (
              <Grid size={{ xs: 12, md: 6 }}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.65 }}>
                  المقاول
                </Typography>
                <Autocomplete
                  fullWidth
                  size="small"
                  options={contractorOptions}
                  value={selectedContractorOption}
                  getOptionLabel={(option) => option.name}
                  isOptionEqualToValue={(a, b) => a.id === b.id}
                  onChange={(_, val) => {
                    if (val?.isAddNew) {
                      setInlineContractorOpen(true);
                      return;
                    }
                    updateHeader('contractorId', val ? val.id : '');
                  }}
                  renderOption={(props, option) => {
                    const { key, ...rest } = props;
                    return (
                      <li key={key} {...rest}>
                        {option.isAddNew ? (
                          <Stack direction="row" spacing={1} alignItems="center" sx={{ color: 'primary.main', fontWeight: 800 }}>
                            <IconUserPlus size={16} />
                            <span>{option.name}</span>
                          </Stack>
                        ) : (
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>
                            {option.name}
                          </Typography>
                        )}
                      </li>
                    );
                  }}
                  renderInput={(params) => <TextField {...params} placeholder="اختر المقاول..." sx={pillInputSx} />}
                />
              </Grid>
            )}
          </Grid>

          {/* Inline Supplier Creation (preserves all unsaved form state) */}
          <Collapse in={inlineSupplierOpen}>
            <Card variant="outlined" sx={{ borderRadius: 2.5, bgcolor: cardBg, borderColor: selectedCardBorder }}>
              <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.25 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                    إضافة مورد جديد (مع الحفاظ على بيانات الحركة)
                  </Typography>
                  <IconButton size="small" onClick={() => setInlineSupplierOpen(false)} aria-label="إغلاق">
                    <IconX size={16} />
                  </IconButton>
                </Stack>
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
                      onClick={() => void onCreateInlineSupplier()}
                      sx={{ minHeight: 40, fontWeight: 800, borderRadius: 2 }}
                    >
                      {creatingSupplier ? '...' : 'إضافة'}
                    </Button>
                  </Grid>
                </Grid>
              </CardContent>
            </Card>
          </Collapse>

          {/* Inline Contractor Creation (preserves all unsaved form state) */}
          <Collapse in={inlineContractorOpen}>
            <Card variant="outlined" sx={{ borderRadius: 2.5, bgcolor: cardBg, borderColor: selectedCardBorder }}>
              <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.25 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                    إضافة مقاول جديد (مع الحفاظ على بيانات الحركة)
                  </Typography>
                  <IconButton size="small" onClick={() => setInlineContractorOpen(false)} aria-label="إغلاق">
                    <IconX size={16} />
                  </IconButton>
                </Stack>
                <Grid container spacing={1.25} alignItems="center">
                  <Grid size={{ xs: 12, sm: 5 }}>
                    <TextField
                      fullWidth
                      size="small"
                      label="اسم المقاول *"
                      value={inlineContractorName}
                      onChange={(e) => setInlineContractorName(e.target.value)}
                    />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 5 }}>
                    <TextField
                      fullWidth
                      size="small"
                      label="رقم الهاتف"
                      value={inlineContractorPhone}
                      onChange={(e) => setInlineContractorPhone(e.target.value)}
                    />
                  </Grid>
                  <Grid size={{ xs: 12, sm: 2 }}>
                    <Button
                      fullWidth
                      variant="contained"
                      size="small"
                      disabled={creatingContractor || !inlineContractorName.trim()}
                      onClick={() => void onCreateInlineContractor()}
                      sx={{ minHeight: 40, fontWeight: 800, borderRadius: 2 }}
                    >
                      {creatingContractor ? '...' : 'إضافة'}
                    </Button>
                  </Grid>
                </Grid>
              </CardContent>
            </Card>
          </Collapse>

          <Divider />

          {/* Invoice Items OR Simplified Single-Amount Mode */}
          <Box>
            <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ mb: 1.25 }}>
              <Stack direction="row" spacing={1} alignItems="center">
                <Typography variant="subtitle1" sx={{ fontWeight: 850 }}>
                  {entryMode === 'simple' ? 'بيان الحركة' : 'بنود الفاتورة'}
                </Typography>
                {lines.length === 1 && (
                  <Button
                    type="button"
                    size="small"
                    variant="text"
                    onClick={() => setEntryMode((m) => (m === 'invoice' ? 'simple' : 'invoice'))}
                    sx={{ fontSize: '0.75rem', fontWeight: 700, minHeight: 28, px: 1 }}
                  >
                    {entryMode === 'invoice' ? 'إدخال مبلغ مباشر' : 'تفصيل بالبنود'}
                  </Button>
                )}
              </Stack>
              <Button
                type="button"
                size="small"
                variant="outlined"
                startIcon={<IconPlus size={15} />}
                onClick={() => {
                  setEntryMode('invoice');
                  addLine();
                }}
                sx={{ borderRadius: 2, fontWeight: 700, borderColor: fieldBorder, color: 'text.primary' }}
              >
                إضافة بند
              </Button>
            </Stack>

            {entryMode === 'simple' && lines.length === 1 && primaryLine ? (
              <Stack spacing={1.5}>
                <TextField
                  fullWidth
                  size="small"
                  required
                  placeholder="وصف الحركة..."
                  value={primaryLine.description}
                  onChange={(e) => updateLine(primaryLine.id, 'description', e.target.value)}
                  sx={pillInputSx}
                />
                <Box>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.65 }}>
                    المبلغ ({header.currency})
                  </Typography>
                  <TextField
                    fullWidth
                    size="small"
                    type="number"
                    placeholder="0.00"
                    inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                    value={primaryLine.amount}
                    onChange={(e) => {
                      updateLine(primaryLine.id, 'amount', e.target.value);
                      updateLine(primaryLine.id, 'quantity', '');
                      updateLine(primaryLine.id, 'unitPrice', '');
                    }}
                    sx={pillInputSx}
                  />
                </Box>
              </Stack>
            ) : (
              <Stack spacing={1.25}>
                {lines.map((line, index) => {
                  const lineTotal = computeLineTotal(line);
                  return (
                    <Box
                      key={line.id}
                      sx={{
                        p: { xs: 1.5, sm: 1.75 },
                        borderRadius: 2.5,
                        bgcolor: cardBg,
                        border: '1px solid',
                        borderColor: fieldBorder
                      }}
                    >
                      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                        <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                          وصف البند {lines.length > 1 ? `#${index + 1}` : ''}
                        </Typography>
                        <Stack direction="row" spacing={1} alignItems="center">
                          {lineTotal > 0 && (
                            <Typography variant="caption" sx={{ fontWeight: 850, color: '#3b82f6', fontVariantNumeric: 'tabular-nums' }}>
                              {formatFinancialAmount(lineTotal)} {header.currency}
                            </Typography>
                          )}
                          {lines.length > 1 && (
                            <IconButton size="small" color="error" onClick={() => removeLine(line.id)} aria-label="حذف البند">
                              <IconTrash size={15} />
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
                            placeholder="مثال: توريد خامات سباكة"
                            value={line.description}
                            onChange={(e) => updateLine(line.id, 'description', e.target.value)}
                            sx={pillInputSx}
                          />
                        </Grid>
                        <Grid size={{ xs: 4 }}>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.4 }}>
                            سعر الوحدة
                          </Typography>
                          <TextField
                            fullWidth
                            size="small"
                            type="number"
                            placeholder="0"
                            inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                            value={line.unitPrice}
                            onChange={(e) => updateLine(line.id, 'unitPrice', e.target.value)}
                            sx={pillInputSx}
                          />
                        </Grid>
                        <Grid size={{ xs: 4 }}>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.4 }}>
                            الكمية
                          </Typography>
                          <TextField
                            fullWidth
                            size="small"
                            type="number"
                            placeholder="0"
                            inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                            value={line.quantity}
                            onChange={(e) => updateLine(line.id, 'quantity', e.target.value)}
                            sx={pillInputSx}
                          />
                        </Grid>
                        <Grid size={{ xs: 4 }}>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.4 }}>
                            الوحدة
                          </Typography>
                          <TextField
                            select
                            fullWidth
                            size="small"
                            value={COMMON_UNITS.includes(line.unit) ? line.unit : line.unit || 'قطعة'}
                            onChange={(e) => updateLine(line.id, 'unit', e.target.value)}
                            sx={pillInputSx}
                          >
                            {COMMON_UNITS.map((u) => (
                              <MenuItem key={u} value={u}>
                                {u}
                              </MenuItem>
                            ))}
                            {line.unit && !COMMON_UNITS.includes(line.unit) && (
                              <MenuItem value={line.unit}>{line.unit}</MenuItem>
                            )}
                          </TextField>
                        </Grid>
                      </Grid>
                    </Box>
                  );
                })}
              </Stack>
            )}
          </Box>

          {/* Smart Suggestion Banner */}
          {suggesting && (
            <Typography variant="caption" color="text.secondary">
              جاري تحليل وصف البند...
            </Typography>
          )}
          {suggestion && (
            <Box
              sx={{
                p: 1.25,
                borderRadius: 2,
                border: '1px solid',
                borderColor: 'success.main',
                bgcolor: isDark ? 'rgba(16, 185, 129, 0.1)' : 'rgba(16, 185, 129, 0.06)',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                gap: 1
              }}
            >
              <Box>
                <Typography variant="caption" sx={{ fontWeight: 800, display: 'block' }}>
                  اقتراح ذكي · {Math.round(suggestion.confidence * 100)}%
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {suggestion.suggestedExpenseCategory} · {suggestion.suggestedEntryKind}
                </Typography>
              </Box>
              <Button type="button" size="small" variant="outlined" color="success" onClick={applySuggestion}>
                تطبيق
              </Button>
            </Box>
          )}

          {/* Bottom Split: Payment Mode + Unified Attachments (Right) | Unified Invoice Summary (Left) */}
          <Grid container spacing={1.75} alignItems="stretch">
            {/* Payment Mode & Unified Attachments */}
            <Grid size={{ xs: 12, md: 6 }}>
              <Stack spacing={1.5} sx={{ height: '100%' }}>
                <Box>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', fontWeight: 700, mb: 0.75 }}>
                    حالة الدفع
                  </Typography>
                  <Box
                    role="radiogroup"
                    aria-label="حالة الدفع"
                    sx={{
                      display: 'grid',
                      gridTemplateColumns: 'repeat(3, 1fr)',
                      gap: 0.65,
                      p: 0.5,
                      borderRadius: 99,
                      bgcolor: cardBg,
                      border: '1px solid',
                      borderColor: fieldBorder
                    }}
                  >
                    {(
                      [
                        { value: 'cash', label: 'نقدي' },
                        { value: 'credit', label: 'آجل' },
                        { value: 'partial', label: 'جزئي' }
                      ] as const
                    ).map((opt) => {
                      const active = header.paymentMode === opt.value;
                      return (
                        <Button
                          key={opt.value}
                          type="button"
                          size="small"
                          onClick={() => updateHeader('paymentMode', opt.value)}
                          sx={{
                            borderRadius: 99,
                            minHeight: 34,
                            fontWeight: 800,
                            bgcolor: active ? (isDark ? '#25324a' : '#1e293b') : 'transparent',
                            color: active ? '#ffffff' : 'text.secondary',
                            '&:hover': {
                              bgcolor: active ? (isDark ? '#25324a' : '#1e293b') : 'action.hover'
                            }
                          }}
                        >
                          {opt.label}
                        </Button>
                      );
                    })}
                  </Box>
                </Box>

                {header.paymentMode === 'partial' && (
                  <TextField
                    fullWidth
                    size="small"
                    type="number"
                    label={`المبلغ المدفوع الآن (${header.currency})`}
                    inputProps={{ inputMode: 'decimal', min: 0, step: 'any' }}
                    value={header.partialPaidAmount}
                    onChange={(e) => updateHeader('partialPaidAmount', e.target.value)}
                    sx={pillInputSx}
                  />
                )}

                {/* Unified Attachment Area */}
                <Box
                  sx={{
                    flex: 1,
                    p: 1.75,
                    borderRadius: 2.5,
                    border: '1.5px dashed',
                    borderColor: fieldBorder,
                    bgcolor: cardBg,
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    textAlign: 'center',
                    gap: 1
                  }}
                >
                  <Box sx={{ color: '#3b82f6', display: 'flex' }}>
                    {isMobile ? <IconCamera size={24} /> : <IconUpload size={22} />}
                  </Box>
                  <Box>
                    <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                      {isMobile ? 'إضافة صور أو مرفقات' : 'إضافة مرفقات'}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      صور أو PDF — اختياري
                    </Typography>
                  </Box>
                  <Stack direction="row" spacing={1} flexWrap="wrap" justifyContent="center" useFlexGap>
                    <Button
                      component="label"
                      size="small"
                      variant="outlined"
                      startIcon={<IconCamera size={15} />}
                      sx={{ borderRadius: 99, fontWeight: 700, fontSize: '0.75rem' }}
                    >
                      الكاميرا
                      <input hidden type="file" accept="image/*" capture="environment" onChange={(e) => handleAddFiles(e.target.files)} />
                    </Button>
                    <Button
                      component="label"
                      size="small"
                      variant="outlined"
                      startIcon={<IconPhoto size={15} />}
                      sx={{ borderRadius: 99, fontWeight: 700, fontSize: '0.75rem' }}
                    >
                      صور / PDF
                      <input
                        hidden
                        multiple
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png,.webp"
                        onChange={(e) => handleAddFiles(e.target.files)}
                      />
                    </Button>
                  </Stack>

                  {attachments.length > 0 && (
                    <Stack direction="row" spacing={0.75} flexWrap="wrap" justifyContent="center" useFlexGap sx={{ mt: 0.5, width: '100%' }}>
                      {attachments.map((file, idx) => (
                        <Chip
                          key={`${file.name}-${idx}`}
                          size="small"
                          icon={file.type === 'application/pdf' ? <IconFileTypePdf size={14} /> : <IconPhoto size={14} />}
                          label={file.name}
                          onDelete={() => onRemoveAttachment(idx)}
                          sx={{ maxWidth: 200 }}
                        />
                      ))}
                    </Stack>
                  )}
                </Box>
              </Stack>
            </Grid>

            {/* Unified Invoice Summary Card */}
            <Grid size={{ xs: 12, md: 6 }}>
              <Box
                sx={{
                  height: '100%',
                  p: 2,
                  borderRadius: 2.5,
                  bgcolor: summaryBg,
                  border: '1px solid',
                  borderColor: summaryBorder,
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  gap: 1.5
                }}
              >
                <Box>
                  <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, display: 'block' }}>
                    {entryMode === 'simple' ? 'إجمالي الحركة' : 'إجمالي الفاتورة'}
                  </Typography>
                  <Typography
                    variant="h2"
                    sx={{
                      mt: 0.5,
                      fontWeight: 900,
                      color: isDark ? '#60a5fa' : '#1d4ed8',
                      fontVariantNumeric: 'tabular-nums',
                      fontSize: { xs: '1.5rem', sm: '1.75rem' }
                    }}
                  >
                    {formatFinancialAmount(grandTotal)} {header.currency}
                  </Typography>
                </Box>

                <Divider sx={{ borderColor: summaryBorder }} />

                <Stack spacing={1}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 700 }}>
                      المدفوع
                    </Typography>
                    <Typography variant="subtitle1" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums' }}>
                      {formatFinancialAmount(paidAmount)}
                    </Typography>
                  </Stack>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography variant="body2" color="text.secondary" sx={{ fontWeight: 700 }}>
                      المتبقي
                    </Typography>
                    <Typography
                      variant="subtitle1"
                      sx={{
                        fontWeight: 850,
                        fontVariantNumeric: 'tabular-nums',
                        color: remainingAmount > 0 ? '#f59e0b' : 'success.main'
                      }}
                    >
                      {formatFinancialAmount(remainingAmount)}
                    </Typography>
                  </Stack>
                </Stack>
              </Box>
            </Grid>
          </Grid>
        </Stack>
      </Box>

      {/* Sticky Accessible Save Actions Footer (never obstructed by Berry gear or mobile bottom nav) */}
      <Box
        sx={{
          px: { xs: 2, sm: 3 },
          py: 2,
          pb: { xs: 'calc(16px + env(safe-area-inset-bottom, 0px))', sm: 2 },
          borderTop: '1px solid',
          borderColor: 'divider',
          bgcolor: surfaceBg,
          position: isMobile ? 'sticky' : 'static',
          bottom: 0,
          zIndex: 5
        }}
      >
        <Grid container spacing={1.25}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              fullWidth
              type="submit"
              disabled={saving}
              variant="contained"
              startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <IconCheck size={18} />}
              sx={{
                minHeight: 46,
                borderRadius: 2.25,
                fontWeight: 850,
                fontSize: '0.95rem',
                bgcolor: '#2563eb',
                color: '#ffffff',
                boxShadow: '0 6px 16px rgba(37, 99, 235, 0.32)',
                '&:hover': { bgcolor: '#1d4ed8' }
              }}
            >
              {saving ? 'جاري الحفظ...' : 'حفظ الحركة'}
            </Button>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Button
              fullWidth
              type="button"
              disabled={saving}
              variant="outlined"
              onClick={(e) => void onSubmit(e, true)}
              sx={{
                minHeight: 46,
                borderRadius: 2.25,
                fontWeight: 800,
                fontSize: '0.92rem',
                borderColor: fieldBorder,
                color: 'text.primary',
                '&:hover': { borderColor: '#3b82f6', bgcolor: 'action.hover' }
              }}
            >
              حفظ وإضافة أخرى
            </Button>
          </Grid>
        </Grid>
      </Box>
    </Box>
  );
}
