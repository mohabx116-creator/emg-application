import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  Grid,
  IconButton,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Skeleton,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconArrowRight,
  IconBuilding,
  IconDownload,
  IconFileDescription,
  IconPlus,
  IconRefresh,
  IconSearch,
  IconTrash,
  IconUserPlus,
  IconX
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import {
  createEmgContract,
  createEmgContractItem,
  createEmgContractor,
  createEmgContractorDraft,
  downloadEmgContractorDraft,
  getEmgContractItems,
  getEmgContractorContracts,
  getEmgContractorStatements,
  getEmgContractors,
  getEmgEligibleContractorWork,
  type EmgContract,
  type EmgContractItem,
  type EmgContractor,
  type EmgContractorStatement,
  type EmgEligibleContractorWork
} from 'api/emg';
import { useEmgProjectsCache } from 'hooks/useEmgData';
import { formatEmgMoney, getLocalIsoDate } from 'utils/emgFormatting';

type DraftLineInput = {
  id: string;
  contractItemId: number;
  workKey: string;
  sourceEntityType: string;
  sourceEntityId: number;
  quantity: string;
  unitRate: number;
  sourceReference: string;
};

function createEmptyDraftLine(): DraftLineInput {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
    contractItemId: 0,
    workKey: '',
    sourceEntityType: 'site-movement',
    sourceEntityId: 0,
    quantity: '',
    unitRate: 0,
    sourceReference: ''
  };
}

export default function ContractorStatements() {
  const theme = useTheme();
  const downSM = useMediaQuery(theme.breakpoints.down('sm'));
  const downMD = useMediaQuery(theme.breakpoints.down('md'));
  const { data: projects = [] } = useEmgProjectsCache(true);

  const [contractors, setContractors] = useState<EmgContractor[]>([]);
  const [allContracts, setAllContracts] = useState<EmgContract[]>([]);
  const [statements, setStatements] = useState<EmgContractorStatement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  // List filters
  const [search, setSearch] = useState('');
  const [projectFilter, setProjectFilter] = useState<number | ''>('');

  // Selected contractor detail view
  const [selectedContractorId, setSelectedContractorId] = useState<number | null>(null);
  const [detailTab, setDetailTab] = useState(0);

  // Detail sub-data for selected contract
  const [selectedContractId, setSelectedContractId] = useState<number>(0);
  const [contractItems, setContractItems] = useState<EmgContractItem[]>([]);
  const [eligibleWork, setEligibleWork] = useState<EmgEligibleContractorWork[]>([]);

  // Dialogs
  const [addContractorOpen, setAddContractorOpen] = useState(false);
  const [newContractorName, setNewContractorName] = useState('');
  const [newContractorPhone, setNewContractorPhone] = useState('');

  const [addContractOpen, setAddContractOpen] = useState(false);
  const [newContractProjectId, setNewContractProjectId] = useState<number | ''>('');
  const [newContractNumber, setNewContractNumber] = useState('');

  const [newItemDesc, setNewItemDesc] = useState('');
  const [newItemUnit, setNewItemUnit] = useState('');
  const [newItemQty, setNewItemQty] = useState('');
  const [newItemRate, setNewItemRate] = useState('');

  // Multi-line statement creation dialog
  const [createStmtOpen, setCreateStmtOpen] = useState(false);
  const [stmtContractId, setStmtContractId] = useState<number>(0);
  const [stmtPeriodFrom, setStmtPeriodFrom] = useState(getLocalIsoDate());
  const [stmtPeriodTo, setStmtPeriodTo] = useState(getLocalIsoDate());
  const [stmtLines, setStmtLines] = useState<DraftLineInput[]>([createEmptyDraftLine()]);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      setError('');
      const [c, contractsList, s] = await Promise.all([
        getEmgContractors(),
        getEmgContractorContracts(),
        getEmgContractorStatements()
      ]);
      setContractors(c);
      setAllContracts(contractsList);
      setStatements(s);
    } catch {
      setError('تعذر تحميل بيانات كشوف المقاولين.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  // Load items & eligible work whenever active contract changes
  const activeContractIdForLoad = createStmtOpen ? stmtContractId : selectedContractId;
  useEffect(() => {
    if (!activeContractIdForLoad) {
      setContractItems([]);
      setEligibleWork([]);
      return;
    }
    void Promise.all([getEmgContractItems(activeContractIdForLoad), getEmgEligibleContractorWork(activeContractIdForLoad)])
      .then(([loadedItems, loadedWork]) => {
        setContractItems(loadedItems);
        setEligibleWork(loadedWork);
      })
      .catch(() => setError('تعذر تحميل بنود العقد أو الأعمال المتاحة.'));
  }, [activeContractIdForLoad]);

  // Contractor account summaries (strictly aggregating real backend statement values)
  const contractorAccounts = useMemo(() => {
    const q = search.trim().toLowerCase();
    return contractors
      .map((contractor) => {
        const cContracts = allContracts.filter(
          (c) => c.contractorId === contractor.id && (!projectFilter || c.projectId === projectFilter)
        );
        const cStatements = statements.filter(
          (s) => s.contractorId === contractor.id && (!projectFilter || s.projectId === projectFilter)
        );
        const projectNames = Array.from(
          new Set([...cContracts.map((c) => c.projectName), ...cStatements.map((s) => s.projectName)].filter(Boolean))
        );
        const totalEntitlement = cStatements.reduce((sum, s) => sum + (s.grossAmount || 0), 0);
        const totalPaid = cStatements.reduce((sum, s) => sum + (s.previousPaymentsAmount || 0), 0);
        const outstandingBalance = cStatements.reduce((sum, s) => sum + (s.netAmount || 0), 0);
        return {
          contractor,
          contracts: cContracts,
          statements: cStatements,
          projectNames,
          totalEntitlement,
          totalPaid,
          outstandingBalance
        };
      })
      .filter((row) => {
        if (projectFilter && row.contracts.length === 0 && row.statements.length === 0) return false;
        if (!q) return true;
        return `${row.contractor.name} ${row.projectNames.join(' ')} ${row.contractor.phone || ''}`.toLowerCase().includes(q);
      });
  }, [contractors, allContracts, statements, search, projectFilter]);

  const selectedAccount = useMemo(
    () => contractorAccounts.find((a) => a.contractor.id === selectedContractorId) || null,
    [contractorAccounts, selectedContractorId]
  );

  // Auto-select first contract when opening a contractor
  useEffect(() => {
    if (selectedAccount && selectedAccount.contracts.length > 0) {
      if (!selectedAccount.contracts.some((c) => c.id === selectedContractId)) {
        setSelectedContractId(selectedAccount.contracts[0].id);
      }
    } else {
      setSelectedContractId(0);
    }
  }, [selectedAccount, selectedContractId]);

  const handleAddContractor = async () => {
    if (!newContractorName.trim()) return;
    setSaving(true);
    try {
      setError('');
      const created = await createEmgContractor({ name: newContractorName.trim(), phone: newContractorPhone.trim() || undefined });
      setNewContractorName('');
      setNewContractorPhone('');
      setAddContractorOpen(false);
      setMessage(`تمت إضافة المقاول "${created.name}" بنجاح.`);
      await load();
      setSelectedContractorId(created.id);
    } catch {
      setError('تعذر إضافة المقاول. تحقق من الصلاحيات.');
    } finally {
      setSaving(false);
    }
  };

  const handleAddContract = async () => {
    if (!selectedAccount || !newContractProjectId || !newContractNumber.trim()) return;
    setSaving(true);
    try {
      setError('');
      const created = await createEmgContract(selectedAccount.contractor.id, {
        projectId: Number(newContractProjectId),
        contractNumber: newContractNumber.trim(),
        currency: 'EGP'
      });
      setNewContractNumber('');
      setAddContractOpen(false);
      setMessage(`تم إنشاء العقد رقم ${created.contractNumber} بنجاح.`);
      await load();
      setSelectedContractId(created.id);
    } catch {
      setError('تعذر إنشاء العقد. تحقق من الصلاحيات.');
    } finally {
      setSaving(false);
    }
  };

  const handleAddContractItem = async () => {
    if (!selectedContractId || !newItemDesc.trim() || !newItemUnit.trim() || !newItemQty || !newItemRate) return;
    setSaving(true);
    try {
      setError('');
      await createEmgContractItem(selectedContractId, {
        description: newItemDesc.trim(),
        unit: newItemUnit.trim(),
        approvedQuantity: Number(newItemQty),
        unitRate: Number(newItemRate),
        sourceReference: 'تم إدخاله من شاشة كشوف المقاولين'
      });
      setNewItemDesc('');
      setNewItemUnit('');
      setNewItemQty('');
      setNewItemRate('');
      setContractItems(await getEmgContractItems(selectedContractId));
      setMessage('تمت إضافة بند العمل للعقد بنجاح.');
    } catch {
      setError('تعذر إضافة بند العقد.');
    } finally {
      setSaving(false);
    }
  };

  const openCreateStatement = () => {
    const firstContractId = selectedContractId || selectedAccount?.contracts[0]?.id || 0;
    setStmtContractId(firstContractId);
    setStmtLines([createEmptyDraftLine()]);
    setCreateStmtOpen(true);
  };

  const updateStmtLine = (lineId: string, patch: Partial<DraftLineInput>) => {
    setStmtLines((prev) => prev.map((line) => (line.id === lineId ? { ...line, ...patch } : line)));
  };

  const handleSelectWorkForLine = (lineId: string, key: string) => {
    const picked = eligibleWork.find((item) => `${item.sourceEntityType}:${item.sourceEntityId}` === key);
    if (!picked) {
      updateStmtLine(lineId, { workKey: '' });
      return;
    }
    updateStmtLine(lineId, {
      workKey: key,
      sourceEntityType: picked.sourceEntityType,
      sourceEntityId: picked.sourceEntityId,
      quantity: String(picked.quantity),
      sourceReference: picked.sourceReference || `${picked.sourceEntityType}:${picked.sourceEntityId}`
    });
  };

  const handleCreateStatement = async () => {
    if (!selectedAccount || !stmtContractId) return;
    const contract = selectedAccount.contracts.find((c) => c.id === stmtContractId);
    if (!contract) return;

    const preparedLines = stmtLines
      .filter((line) => line.contractItemId > 0 && line.sourceEntityId > 0 && Number(line.quantity) > 0)
      .map((line) => {
        const item = contractItems.find((ci) => ci.id === line.contractItemId);
        return {
          contractItemId: line.contractItemId,
          sourceEntityType: line.sourceEntityType,
          sourceEntityId: line.sourceEntityId,
          quantity: Number(line.quantity),
          unitRate: item?.unitRate ?? line.unitRate,
          sourceReference: line.sourceReference || `${line.sourceEntityType}:${line.sourceEntityId}`
        };
      });

    if (preparedLines.length === 0) {
      setError('يرجى اختيار بند عقد وسجل عمل وكمية صالحة لسطر واحد على الأقل.');
      return;
    }

    setSaving(true);
    try {
      setError('');
      await createEmgContractorDraft({
        projectId: contract.projectId,
        contractorId: selectedAccount.contractor.id,
        contractId: contract.id,
        periodFrom: new Date(stmtPeriodFrom).toISOString(),
        periodTo: new Date(stmtPeriodTo).toISOString(),
        currency: contract.currency || 'EGP',
        sourceReference: `كشف مقاول · ${preparedLines.length} بنود`,
        lines: preparedLines
      });
      setCreateStmtOpen(false);
      setMessage(`تم إنشاء كشف المقاول بعدد ${preparedLines.length} بند بنجاح.`);
      await load();
    } catch {
      setError('تعذر إنشاء الكشف. تأكد من عدم تكرار سجل عمل مستخدم وعدم تجاوز الكمية المعتمدة.');
    } finally {
      setSaving(false);
    }
  };

  const downloadStatement = async (id: number) => {
    const blob = await downloadEmgContractorDraft(id);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `contractor-statement-${id}.xlsx`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  // ======================== DETAIL VIEW ========================
  if (selectedAccount) {
    const { contractor, contracts: cContracts, statements: cStatements, totalEntitlement, totalPaid, outstandingBalance } = selectedAccount;

    return (
      <Box sx={{ pb: 4 }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={1.5} sx={{ mb: 2.5 }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Button
              variant="outlined"
              color="inherit"
              size="small"
              startIcon={<IconArrowRight size={17} />}
              onClick={() => {
                setSelectedContractorId(null);
                setDetailTab(0);
              }}
              sx={{ borderRadius: 2, fontWeight: 700 }}
            >
              قائمة المقاولين
            </Button>
            <Box>
              <Typography variant="h2" sx={{ fontWeight: 850 }}>
                {contractor.name}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {selectedAccount.projectNames.join(' · ') || 'لا توجد عقود مرتبطة بعد'}
                {contractor.phone ? ` · ${contractor.phone}` : ''}
              </Typography>
            </Box>
          </Stack>

          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Button variant="outlined" startIcon={<IconPlus size={16} />} onClick={() => setAddContractOpen(true)} sx={{ borderRadius: 2, fontWeight: 700 }}>
              عقد جديد
            </Button>
            <Button
              variant="contained"
              color="secondary"
              startIcon={<IconFileDescription size={17} />}
              onClick={openCreateStatement}
              disabled={cContracts.length === 0}
              sx={{ borderRadius: 2, fontWeight: 700 }}
            >
              إنشاء كشف جديد
            </Button>
          </Stack>
        </Stack>

        {error && (
          <Alert severity="error" onClose={() => setError('')} sx={{ mb: 2, borderRadius: 2 }}>
            {error}
          </Alert>
        )}
        {message && (
          <Alert severity="success" onClose={() => setMessage('')} sx={{ mb: 2, borderRadius: 2 }}>
            {message}
          </Alert>
        )}

        {/* Account Summary KPI Strip */}
        <Grid container spacing={2} sx={{ mb: 2.5 }}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <MainCard sx={{ borderRadius: 3 }}>
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                إجمالي المستحق (الأعمال)
              </Typography>
              <Typography variant="h3" sx={{ fontWeight: 900, mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>
                {formatEmgMoney(totalEntitlement)}
              </Typography>
            </MainCard>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <MainCard sx={{ borderRadius: 3 }}>
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                إجمالي المدفوع
              </Typography>
              <Typography variant="h3" color="success.main" sx={{ fontWeight: 900, mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>
                {formatEmgMoney(totalPaid)}
              </Typography>
            </MainCard>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <MainCard sx={{ borderRadius: 3 }}>
              <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                الرصيد المتبقي
              </Typography>
              <Typography variant="h3" color={outstandingBalance > 0 ? 'error.main' : 'primary.main'} sx={{ fontWeight: 900, mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>
                {formatEmgMoney(outstandingBalance)}
              </Typography>
            </MainCard>
          </Grid>
        </Grid>

        {/* 4 Clean Tabs */}
        <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden' }}>
          <Tabs
            value={detailTab}
            onChange={(_, v) => setDetailTab(v)}
            variant="scrollable"
            scrollButtons="auto"
            sx={{ borderBottom: 1, borderColor: 'divider', px: 1.5 }}
          >
            <Tab label="نظرة عامة على الحساب" sx={{ fontWeight: 700 }} />
            <Tab label={`الكشوف (${cStatements.length})`} sx={{ fontWeight: 700 }} />
            <Tab label={`العقود وبنود الأعمال (${cContracts.length})`} sx={{ fontWeight: 700 }} />
            <Tab label="المدفوعات" sx={{ fontWeight: 700 }} />
          </Tabs>

          <Box sx={{ p: { xs: 2, sm: 2.5 } }}>
            {/* TAB 0: Account Overview */}
            {detailTab === 0 && (
              <Grid container spacing={2}>
                <Grid size={{ xs: 12, md: 6 }}>
                  <Card variant="outlined" sx={{ borderRadius: 2.5 }}>
                    <CardContent>
                      <Typography variant="h4" sx={{ fontWeight: 800, mb: 1.5 }}>
                        بيانات الحساب
                      </Typography>
                      <Stack spacing={1}>
                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">المقاول</Typography>
                          <Typography fontWeight={700}>{contractor.name}</Typography>
                        </Stack>
                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">الهاتف</Typography>
                          <Typography fontWeight={600}>{contractor.phone || '—'}</Typography>
                        </Stack>
                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">عدد العقود</Typography>
                          <Typography fontWeight={700}>{cContracts.length}</Typography>
                        </Stack>
                        <Stack direction="row" justifyContent="space-between">
                          <Typography color="text.secondary">عدد الكشوف المسجلة</Typography>
                          <Typography fontWeight={700}>{cStatements.length}</Typography>
                        </Stack>
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>
                <Grid size={{ xs: 12, md: 6 }}>
                  <Card variant="outlined" sx={{ borderRadius: 2.5 }}>
                    <CardContent>
                      <Typography variant="h4" sx={{ fontWeight: 800, mb: 1.5 }}>
                        المشروعات المرتبطة
                      </Typography>
                      {selectedAccount.projectNames.length === 0 ? (
                        <Typography color="text.secondary">لا توجد مشروعات مرتبطة بعد. ابدأ بإنشاء عقد للمقاول.</Typography>
                      ) : (
                        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                          {selectedAccount.projectNames.map((name) => (
                            <Chip key={name} icon={<IconBuilding size={15} />} label={name} variant="outlined" sx={{ fontWeight: 700 }} />
                          ))}
                        </Stack>
                      )}
                    </CardContent>
                  </Card>
                </Grid>
              </Grid>
            )}

            {/* TAB 1: Statements */}
            {detailTab === 1 && (
              <Stack spacing={1.5}>
                {cStatements.length === 0 ? (
                  <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                    لا توجد كشوف مسجلة لهذا المقاول بعد.
                  </Typography>
                ) : (
                  cStatements.map((stmt) => (
                    <Card key={stmt.id} variant="outlined" sx={{ borderRadius: 2.5 }}>
                      <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
                        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1.5}>
                          <Box>
                            <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
                              <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                                كشف #{stmt.id} · {stmt.projectName}
                              </Typography>
                              <Chip size="small" label={`عقد ${stmt.contractNumber}`} variant="outlined" sx={{ fontWeight: 700 }} />
                              <Chip size="small" label={`${stmt.lines.length} بنود`} color="primary" variant="outlined" />
                            </Stack>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                              الإجمالي: <b>{formatEmgMoney(stmt.grossAmount)}</b> · مدفوع سابق: <b>{formatEmgMoney(stmt.previousPaymentsAmount)}</b> · صافي:{' '}
                              <b>{formatEmgMoney(stmt.netAmount)}</b>
                            </Typography>
                          </Box>
                          <Button size="small" variant="outlined" startIcon={<IconDownload size={16} />} onClick={() => void downloadStatement(stmt.id)} sx={{ borderRadius: 2, fontWeight: 700 }}>
                            تصدير Excel
                          </Button>
                        </Stack>
                      </CardContent>
                    </Card>
                  ))
                )}
              </Stack>
            )}

            {/* TAB 2: Contracts & Work Items */}
            {detailTab === 2 && (
              <Stack spacing={2.25}>
                {cContracts.length === 0 ? (
                  <Alert severity="info">لا توجد عقود لهذا المقاول بعد. اضغط «عقد جديد» للبدء.</Alert>
                ) : (
                  <>
                    <FormControl size="small" sx={{ maxWidth: 360 }}>
                      <InputLabel>العقد المحدد</InputLabel>
                      <Select value={selectedContractId || ''} label="العقد المحدد" onChange={(e) => setSelectedContractId(Number(e.target.value))}>
                        {cContracts.map((c) => (
                          <MenuItem key={c.id} value={c.id}>
                            {c.contractNumber} · {c.projectName}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>

                    <Card variant="outlined" sx={{ borderRadius: 2.5 }}>
                      <CardContent>
                        <Typography variant="subtitle1" sx={{ fontWeight: 800, mb: 1.5 }}>
                          إضافة بند عمل جديد للعقد
                        </Typography>
                        <Grid container spacing={1.25} alignItems="center">
                          <Grid size={{ xs: 12, sm: 4 }}>
                            <TextField fullWidth size="small" label="وصف البند" value={newItemDesc} onChange={(e) => setNewItemDesc(e.target.value)} />
                          </Grid>
                          <Grid size={{ xs: 4, sm: 2 }}>
                            <TextField fullWidth size="small" label="الوحدة" value={newItemUnit} onChange={(e) => setNewItemUnit(e.target.value)} />
                          </Grid>
                          <Grid size={{ xs: 4, sm: 2 }}>
                            <TextField fullWidth size="small" type="number" label="الكمية" value={newItemQty} onChange={(e) => setNewItemQty(e.target.value)} />
                          </Grid>
                          <Grid size={{ xs: 4, sm: 2 }}>
                            <TextField fullWidth size="small" type="number" label="السعر" value={newItemRate} onChange={(e) => setNewItemRate(e.target.value)} />
                          </Grid>
                          <Grid size={{ xs: 12, sm: 2 }}>
                            <Button
                              fullWidth
                              variant="contained"
                              disabled={saving || !newItemDesc || !newItemUnit || !newItemQty || !newItemRate}
                              onClick={() => void handleAddContractItem()}
                              sx={{ minHeight: 40, fontWeight: 700 }}
                            >
                              إضافة
                            </Button>
                          </Grid>
                        </Grid>
                      </CardContent>
                    </Card>

                    <TableContainer sx={{ overflowX: 'auto' }}>
                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell sx={{ fontWeight: 800 }}>البند</TableCell>
                            <TableCell sx={{ fontWeight: 800 }}>الكمية المعتمدة</TableCell>
                            <TableCell sx={{ fontWeight: 800 }}>المستخدم</TableCell>
                            <TableCell sx={{ fontWeight: 800 }}>المتبقي</TableCell>
                            <TableCell align="right" sx={{ fontWeight: 800 }}>
                              سعر الوحدة
                            </TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {contractItems.length === 0 ? (
                            <TableRow>
                              <TableCell colSpan={5} align="center" sx={{ py: 3 }}>
                                لا توجد بنود مضافة لهذا العقد.
                              </TableCell>
                            </TableRow>
                          ) : (
                            contractItems.map((item) => (
                              <TableRow key={item.id} hover>
                                <TableCell sx={{ fontWeight: 700 }}>{item.description}</TableCell>
                                <TableCell>
                                  {item.approvedQuantity} {item.unit}
                                </TableCell>
                                <TableCell>{item.usedQuantity}</TableCell>
                                <TableCell sx={{ fontWeight: 700, color: item.remainingQuantity > 0 ? 'success.main' : 'text.secondary' }}>
                                  {item.remainingQuantity} {item.unit}
                                </TableCell>
                                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                                  {formatEmgMoney(item.unitRate)}
                                </TableCell>
                              </TableRow>
                            ))
                          )}
                        </TableBody>
                      </Table>
                    </TableContainer>
                  </>
                )}
              </Stack>
            )}

            {/* TAB 3: Payments */}
            {detailTab === 3 && (
              <Stack spacing={1.5}>
                {cStatements.length === 0 ? (
                  <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                    لا توجد مدفوعات أو خصومات مسجلة على كشوف هذا المقاول حتى الآن.
                  </Typography>
                ) : (
                  <TableContainer sx={{ overflowX: 'auto' }}>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell sx={{ fontWeight: 800 }}>الكشف</TableCell>
                          <TableCell sx={{ fontWeight: 800 }}>المشروع</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 800 }}>
                            إجمالي الأعمال
                          </TableCell>
                          <TableCell align="right" sx={{ fontWeight: 800 }}>
                            المدفوع
                          </TableCell>
                          <TableCell align="right" sx={{ fontWeight: 800 }}>
                            الخصومات
                          </TableCell>
                          <TableCell align="right" sx={{ fontWeight: 800 }}>
                            الصافي المستحق
                          </TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {cStatements.map((stmt) => (
                          <TableRow key={stmt.id} hover>
                            <TableCell sx={{ fontWeight: 700 }}>#{stmt.id}</TableCell>
                            <TableCell>{stmt.projectName}</TableCell>
                            <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                              {formatEmgMoney(stmt.grossAmount)}
                            </TableCell>
                            <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', color: 'success.main', fontWeight: 700 }}>
                              {formatEmgMoney(stmt.previousPaymentsAmount)}
                            </TableCell>
                            <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                              {formatEmgMoney(stmt.deductionsAmount)}
                            </TableCell>
                            <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 800 }}>
                              {formatEmgMoney(stmt.netAmount)}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </TableContainer>
                )}
              </Stack>
            )}
          </Box>
        </MainCard>

        {/* Dialog: Create Contract */}
        <Dialog open={addContractOpen} onClose={() => setAddContractOpen(false)} fullWidth maxWidth="xs" fullScreen={downSM}>
          <DialogTitle>إنشاء عقد جديد للمقاول</DialogTitle>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ pt: 1 }}>
              <FormControl fullWidth size="small">
                <InputLabel>المشروع</InputLabel>
                <Select value={newContractProjectId} label="المشروع" onChange={(e) => setNewContractProjectId(Number(e.target.value))}>
                  {projects.map((p) => (
                    <MenuItem key={p.id} value={p.id}>
                      {p.name} ({p.code})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField fullWidth size="small" label="رقم العقد" value={newContractNumber} onChange={(e) => setNewContractNumber(e.target.value)} />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setAddContractOpen(false)}>إلغاء</Button>
            <Button variant="contained" disabled={saving || !newContractProjectId || !newContractNumber.trim()} onClick={() => void handleAddContract()}>
              حفظ العقد
            </Button>
          </DialogActions>
        </Dialog>

        {/* Dialog: Multi-Line Statement Creation */}
        <Dialog open={createStmtOpen} onClose={() => setCreateStmtOpen(false)} fullWidth maxWidth="md" fullScreen={downSM}>
          <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="h3" sx={{ fontWeight: 800 }}>
              إنشاء كشف مقاول (متعدد البنود)
            </Typography>
            <IconButton onClick={() => setCreateStmtOpen(false)} size="small">
              <IconX size={20} />
            </IconButton>
          </DialogTitle>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ pt: 0.5 }}>
              <Grid container spacing={1.5}>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <FormControl fullWidth size="small">
                    <InputLabel>العقد</InputLabel>
                    <Select value={stmtContractId || ''} label="العقد" onChange={(e) => setStmtContractId(Number(e.target.value))}>
                      {cContracts.map((c) => (
                        <MenuItem key={c.id} value={c.id}>
                          {c.contractNumber} · {c.projectName}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <TextField fullWidth size="small" type="date" label="من تاريخ" value={stmtPeriodFrom} onChange={(e) => setStmtPeriodFrom(e.target.value)} InputLabelProps={{ shrink: true }} />
                </Grid>
                <Grid size={{ xs: 6, sm: 3 }}>
                  <TextField fullWidth size="small" type="date" label="إلى تاريخ" value={stmtPeriodTo} onChange={(e) => setStmtPeriodTo(e.target.value)} InputLabelProps={{ shrink: true }} />
                </Grid>
              </Grid>

              <Divider />

              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                  بنود الأعمال بالكشف ({stmtLines.length})
                </Typography>
                <Button size="small" variant="outlined" startIcon={<IconPlus size={15} />} onClick={() => setStmtLines((prev) => [...prev, createEmptyDraftLine()])}>
                  إضافة سطر عمل
                </Button>
              </Stack>

              {stmtLines.map((line, idx) => (
                <Card key={line.id} variant="outlined" sx={{ borderRadius: 2 }}>
                  <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                      <Chip size="small" label={`سطر #${idx + 1}`} color="primary" variant="outlined" />
                      {stmtLines.length > 1 && (
                        <IconButton size="small" color="error" onClick={() => setStmtLines((prev) => prev.filter((l) => l.id !== line.id))}>
                          <IconTrash size={16} />
                        </IconButton>
                      )}
                    </Stack>
                    <Grid container spacing={1.25}>
                      <Grid size={{ xs: 12, sm: 4 }}>
                        <FormControl fullWidth size="small">
                          <InputLabel>بند العقد</InputLabel>
                          <Select
                            value={line.contractItemId || ''}
                            label="بند العقد"
                            onChange={(e) => {
                              const cid = Number(e.target.value);
                              const item = contractItems.find((ci) => ci.id === cid);
                              updateStmtLine(line.id, { contractItemId: cid, unitRate: item?.unitRate ?? 0 });
                            }}
                          >
                            {contractItems.map((ci) => (
                              <MenuItem key={ci.id} value={ci.id}>
                                {ci.description} (متبقي {ci.remainingQuantity} {ci.unit})
                              </MenuItem>
                            ))}
                          </Select>
                        </FormControl>
                      </Grid>
                      <Grid size={{ xs: 12, sm: 5 }}>
                        <FormControl fullWidth size="small">
                          <InputLabel>سجل العمل المنفّذ</InputLabel>
                          <Select value={line.workKey} label="سجل العمل المنفّذ" onChange={(e) => handleSelectWorkForLine(line.id, String(e.target.value))}>
                            {eligibleWork
                              .filter((w) => !w.alreadyUsed)
                              .map((w) => (
                                <MenuItem key={`${w.sourceEntityType}:${w.sourceEntityId}`} value={`${w.sourceEntityType}:${w.sourceEntityId}`}>
                                  {w.description} · {w.quantity} {w.unit}
                                </MenuItem>
                              ))}
                          </Select>
                        </FormControl>
                      </Grid>
                      <Grid size={{ xs: 12, sm: 3 }}>
                        <TextField
                          fullWidth
                          size="small"
                          type="number"
                          label="الكمية المقاسة"
                          value={line.quantity}
                          onChange={(e) => updateStmtLine(line.id, { quantity: e.target.value })}
                        />
                      </Grid>
                    </Grid>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateStmtOpen(false)} disabled={saving}>
              إلغاء
            </Button>
            <Button variant="contained" color="secondary" disabled={saving || !stmtContractId} onClick={() => void handleCreateStatement()}>
              {saving ? 'جاري الإنشاء...' : 'حفظ وإنشاء الكشف'}
            </Button>
          </DialogActions>
        </Dialog>
      </Box>
    );
  }

  // ======================== CONTRACTOR ACCOUNT LIST ========================
  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={2} sx={{ mb: 2.5 }}>
        <Box>
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            كشوف وحسابات المقاولين
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.35 }}>
            استعراض حسابات المقاولين، المستحقات، المدفوعات، والأرصدة المتبقية وإنشاء الكشوف
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<IconRefresh size={16} />} onClick={() => void load()} sx={{ borderRadius: 2, fontWeight: 700 }}>
            تحديث
          </Button>
          <Button variant="contained" startIcon={<IconUserPlus size={17} />} onClick={() => setAddContractorOpen(true)} sx={{ borderRadius: 2, fontWeight: 700 }}>
            مقاول جديد
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError('')} sx={{ mb: 2, borderRadius: 2 }}>
          {error}
        </Alert>
      )}
      {message && (
        <Alert severity="success" onClose={() => setMessage('')} sx={{ mb: 2, borderRadius: 2 }}>
          {message}
        </Alert>
      )}

      <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden' }}>
        <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'action.hover' }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25}>
            <TextField
              size="small"
              placeholder="ابحث باسم المقاول أو المشروع..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              sx={{ flex: 1, bgcolor: 'background.paper', borderRadius: 1 }}
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <IconSearch size={17} />
                  </InputAdornment>
                )
              }}
            />
            <FormControl size="small" sx={{ minWidth: { xs: '100%', sm: 220 }, bgcolor: 'background.paper', borderRadius: 1 }}>
              <InputLabel>تصفية حسب المشروع</InputLabel>
              <Select value={projectFilter} label="تصفية حسب المشروع" onChange={(e) => setProjectFilter(e.target.value ? Number(e.target.value) : '')}>
                <MenuItem value="">كل المشروعات</MenuItem>
                {projects.map((p) => (
                  <MenuItem key={p.id} value={p.id}>
                    {p.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
        </Box>

        {loading ? (
          <Stack spacing={1.5} sx={{ p: 2 }}>
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} variant="rounded" height={60} />
            ))}
          </Stack>
        ) : contractorAccounts.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 5, textAlign: 'center' }}>
            لا توجد حسابات مقاولين مطابقة.
          </Typography>
        ) : downMD ? (
          <Stack spacing={1.25} sx={{ p: 1.5 }}>
            {contractorAccounts.map((row) => (
              <Card key={row.contractor.id} variant="outlined" sx={{ borderRadius: 2.5 }}>
                <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                  <Stack spacing={1}>
                    <Stack direction="row" justifyContent="space-between" alignItems="center">
                      <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                        {row.contractor.name}
                      </Typography>
                      <Chip size="small" label={`${row.statements.length} كشوف`} variant="outlined" />
                    </Stack>
                    <Typography variant="caption" color="text.secondary">
                      {row.projectNames.join(' · ') || 'بدون مشروع محدد'}
                    </Typography>
                    <Grid container spacing={1}>
                      <Grid size={4}>
                        <Typography variant="caption" color="text.secondary">
                          المستحق
                        </Typography>
                        <Typography variant="body2" fontWeight={800}>
                          {formatEmgMoney(row.totalEntitlement, '')}
                        </Typography>
                      </Grid>
                      <Grid size={4}>
                        <Typography variant="caption" color="text.secondary">
                          المدفوع
                        </Typography>
                        <Typography variant="body2" fontWeight={800} color="success.main">
                          {formatEmgMoney(row.totalPaid, '')}
                        </Typography>
                      </Grid>
                      <Grid size={4}>
                        <Typography variant="caption" color="text.secondary">
                          المتبقي
                        </Typography>
                        <Typography variant="body2" fontWeight={800} color={row.outstandingBalance > 0 ? 'error.main' : 'primary.main'}>
                          {formatEmgMoney(row.outstandingBalance, '')}
                        </Typography>
                      </Grid>
                    </Grid>
                    <Button fullWidth variant="outlined" size="small" onClick={() => setSelectedContractorId(row.contractor.id)} sx={{ borderRadius: 2, fontWeight: 700 }}>
                      فتح حساب المقاول
                    </Button>
                  </Stack>
                </CardContent>
              </Card>
            ))}
          </Stack>
        ) : (
          <TableContainer sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 800 }}>اسم المقاول</TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>المشروع</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                    إجمالي المستحق
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                    إجمالي المدفوع
                  </TableCell>
                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                    الرصيد المتبقي
                  </TableCell>
                  <TableCell align="center" sx={{ fontWeight: 800 }}>
                    الحساب
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {contractorAccounts.map((row) => (
                  <TableRow key={row.contractor.id} hover sx={{ cursor: 'pointer' }} onClick={() => setSelectedContractorId(row.contractor.id)}>
                    <TableCell>
                      <Stack direction="row" spacing={1.25} alignItems="center">
                        <Avatar sx={{ width: 32, height: 32, bgcolor: 'primary.light', color: 'primary.dark', fontSize: '0.85rem', fontWeight: 800 }}>
                          {row.contractor.name.slice(0, 1)}
                        </Avatar>
                        <Box>
                          <Typography variant="body2" sx={{ fontWeight: 800 }}>
                            {row.contractor.name}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {row.contracts.length} عقود · {row.statements.length} كشوف
                          </Typography>
                        </Box>
                      </Stack>
                    </TableCell>
                    <TableCell>{row.projectNames.join('، ') || '—'}</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>
                      {formatEmgMoney(row.totalEntitlement)}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 700, color: 'success.main', fontVariantNumeric: 'tabular-nums' }}>
                      {formatEmgMoney(row.totalPaid)}
                    </TableCell>
                    <TableCell align="right" sx={{ fontWeight: 800, color: row.outstandingBalance > 0 ? 'error.main' : 'primary.main', fontVariantNumeric: 'tabular-nums' }}>
                      {formatEmgMoney(row.outstandingBalance)}
                    </TableCell>
                    <TableCell align="center">
                      <Button size="small" variant="outlined" sx={{ borderRadius: 2, fontWeight: 700 }}>
                        فتح الحساب
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </MainCard>

      {/* Dialog: Add Contractor */}
      <Dialog open={addContractorOpen} onClose={() => setAddContractorOpen(false)} fullWidth maxWidth="xs" fullScreen={downSM}>
        <DialogTitle>إضافة مقاول جديد</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField fullWidth required size="small" label="اسم المقاول" value={newContractorName} onChange={(e) => setNewContractorName(e.target.value)} />
            <TextField fullWidth size="small" label="رقم الهاتف (اختياري)" value={newContractorPhone} onChange={(e) => setNewContractorPhone(e.target.value)} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddContractorOpen(false)}>إلغاء</Button>
          <Button variant="contained" disabled={saving || !newContractorName.trim()} onClick={() => void handleAddContractor()}>
            حفظ المقاول
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
