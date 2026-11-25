import { useCallback, useEffect, useMemo, useState } from 'react';
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
  Grid,
  InputAdornment,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography
} from '@mui/material';
import {
  IconAlertTriangle,
  IconBuildingSkyscraper,
  IconCheck,
  IconChecklist,
  IconExternalLink,
  IconHistory,
  IconRefresh,
  IconSearch,
  IconShieldCheck
} from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import {
  approveEmgProjectImport,
  EmgProject,
  EmgWorkflowReviewItem,
  executeEmgWorkflow,
  getEmgProjects,
  getEmgWorkflowReviewQueue,
  rejectEmgProjectImport
} from 'api/emg';
import {
  HistoricalSettlementReview,
  listHistoricalSettlementReviews,
  recognizeHistoricalSettlement,
  reviewHistoricalSettlement
} from 'api/reviewedworkbook';
import { localizeCurrency } from '../reportLabels';

const labels: Record<string, string> = {
  'site-movement': 'حركة الموقع اليومية',
  'advance-payment': 'دفعة تحت الحساب',
  excavation: 'أعمال الحفر',
  concrete: 'أعمال الخرسانات',
  steel: 'توريد الحديد',
  finishing: 'أعمال التشطيبات',
  'supplier-supply': 'توريدة مورد',
  'supplier-payment': 'دفعة مورد'
};

const settlementLabels: Record<string, string> = {
  Payment: 'دفعة تاريخية',
  Funding: 'تمويل تاريخي',
  Allocation: 'توزيع تاريخي',
  OpeningBalance: 'رصيد افتتاحي',
  Adjustment: 'تسوية تاريخية',
  Expense: 'مصروف تاريخي',
  Liability: 'التزام تاريخي',
  Transaction: 'حركة تاريخية',
  Unresolved: 'غير محسوم',
  Summary: 'ملخص مرجعي',
  DerivedValue: 'قيمة مشتقة'
};

const detailPath = (item: EmgWorkflowReviewItem) => {
  if (item.entityType === 'site-movement') return `/emg/site-movements/${item.entityId}`;
  if (item.entityType === 'advance-payment') return `/emg/advances/${item.entityId}`;
  if (item.entityType === 'supplier-supply' || item.entityType === 'supplier-payment') return `/emg/projects/${item.projectId}`;
  return `/emg/projects/${item.projectId}/${item.entityType}`;
};

export default function ReviewCenter() {
  const { user } = useAuth();
  const [items, setItems] = useState<EmgWorkflowReviewItem[]>([]);
  const [pendingProjects, setPendingProjects] = useState<EmgProject[]>([]);
  const [filter, setFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [activeTab, setActiveTab] = useState(0);
  const [loading, setLoading] = useState(true);
  const [projectBusy, setProjectBusy] = useState<number | null>(null);
  const [workflowBusy, setWorkflowBusy] = useState(false);
  const [rejectProject, setRejectProject] = useState<EmgProject | null>(null);
  const [rejectSupply, setRejectSupply] = useState<EmgWorkflowReviewItem | null>(null);
  const [rejectNotes, setRejectNotes] = useState('');
  const [settlements, setSettlements] = useState<HistoricalSettlementReview[]>([]);
  const [settlementBusy, setSettlementBusy] = useState<number | null>(null);
  const [settlementDrafts, setSettlementDrafts] = useState<Record<number, { obligationId: string; obligationAmount: string; party: string }>>({});
  const [message, setMessage] = useState<{ severity: 'success' | 'error'; text: string } | null>(null);

  const canApproveProject = user?.permissions?.includes('project-imports.approve') ?? false;
  const canRejectProject = user?.permissions?.includes('project-imports.reject') ?? false;
  const canReviewImports = canApproveProject || canRejectProject;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [workflowItems, projects] = await Promise.all([
        getEmgWorkflowReviewQueue(),
        canReviewImports ? getEmgProjects() : Promise.resolve([])
      ]);
      setItems(workflowItems);
      setPendingProjects(projects.filter((project) => project.status === 'تحت المراجعة'));
      if (canReviewImports && projects.length) {
        const reviews = await Promise.all(projects.map((project) => listHistoricalSettlementReviews(project.id).catch(() => [])));
        setSettlements(reviews.flat());
      } else {
        setSettlements([]);
      }
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.message || 'تعذر تحميل قائمة المراجعة.' });
    } finally {
      setLoading(false);
    }
  }, [canReviewImports]);

  useEffect(() => {
    void load();
  }, [load]);

  const distinctTypes = useMemo(() => Array.from(new Set(items.map((item) => item.entityType))), [items]);

  const visible = useMemo(() => {
    const query = filter.trim().toLowerCase();
    return items.filter((item) => {
      if (typeFilter !== 'all' && item.entityType !== typeFilter) return false;
      if (!query) return true;
      return `${item.projectName} ${item.description} ${labels[item.entityType] || item.entityType}`.toLowerCase().includes(query);
    });
  }, [filter, items, typeFilter]);

  const attentionCount = useMemo(
    () => items.filter((item) => !item.hasAttachment || item.blockingChecks.length > 0).length,
    [items]
  );

  const run = async (item: EmgWorkflowReviewItem, action: string, notes?: string) => {
    if (workflowBusy) return;
    setWorkflowBusy(true);
    try {
      await executeEmgWorkflow(item.entityType, item.entityId, action, notes);
      if (action === 'رفض') {
        setRejectSupply(null);
        setRejectNotes('');
      }
      setMessage({ severity: 'success', text: `تم تنفيذ «${action}» على ${labels[item.entityType] || 'الحركة'} بنجاح.` });
      await load();
    } catch (error: any) {
      setMessage({
        severity: 'error',
        text: error?.response?.data?.checks?.[0] || error?.response?.data?.message || error?.message || 'تعذر تنفيذ الإجراء.'
      });
      if (action === 'رفض') {
        setRejectSupply(null);
        setRejectNotes('');
      }
    } finally {
      setWorkflowBusy(false);
    }
  };

  const closeRejection = () => {
    if (projectBusy !== null || workflowBusy) return;
    setRejectProject(null);
    setRejectSupply(null);
    setRejectNotes('');
  };

  const approveProject = async (project: EmgProject) => {
    setProjectBusy(project.id);
    try {
      await approveEmgProjectImport(project.id, 'تمت مراجعة ترحيل المشروع من مركز المراجعة');
      setMessage({ severity: 'success', text: `تم تفعيل المشروع «${project.name}». الحركات المسودة تستكمل دورة الاعتماد الخاصة بها.` });
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر اعتماد ترحيل المشروع.' });
    } finally {
      setProjectBusy(null);
    }
  };

  const rejectPendingProject = async () => {
    if (!rejectProject || !rejectNotes.trim()) return;
    setProjectBusy(rejectProject.id);
    try {
      await rejectEmgProjectImport(rejectProject.id, rejectNotes.trim());
      setMessage({ severity: 'success', text: `تم رفض ترحيل المشروع «${rejectProject.name}» وتسجيل السبب.` });
      setRejectProject(null);
      setRejectNotes('');
      await load();
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر رفض ترحيل المشروع.' });
    } finally {
      setProjectBusy(null);
    }
  };

  const reviewSettlement = async (review: HistoricalSettlementReview, recordId: number, decision: 'confirm' | 'reject') => {
    if (settlementBusy !== null) return;
    setSettlementBusy(recordId);
    try {
      const evidence = decision === 'confirm' ? 'تمت مطابقة مصدر الحركة والمرجع في مراجعة المحاسب.' : 'لا يوجد دليل كافٍ على العلاقة المالية.';
      const draft = settlementDrafts[recordId];
      const updated = await reviewHistoricalSettlement(
        review.batchId,
        review.version,
        recordId,
        decision,
        evidence,
        draft?.obligationId ? Number(draft.obligationId) : undefined,
        draft?.obligationAmount ? Number(draft.obligationAmount) : undefined,
        draft?.party || undefined
      );
      setSettlements((current) => current.map((item) => (item.batchId === updated.batchId ? updated : item)));
      setMessage({ severity: 'success', text: decision === 'confirm' ? 'تم تأكيد العلاقة للمراجعة فقط.' : 'تم إبقاء الحركة قيد المراجعة.' });
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر حفظ قرار التسوية.' });
    } finally {
      setSettlementBusy(null);
    }
  };

  const recognizeSettlement = async (review: HistoricalSettlementReview, recordId: number) => {
    if (settlementBusy !== null) return;
    setSettlementBusy(recordId);
    try {
      const updated = await recognizeHistoricalSettlement(review.batchId, recordId);
      setSettlements((current) => current.map((item) => (item.batchId === updated.batchId ? updated : item)));
      setMessage({ severity: 'success', text: 'تم إنشاء مسودة الدفع التشغيلي المرتبطة بالمصدر. يجب اعتمادها وترحيلها من دورة الدفع العادية.' });
    } catch (error: any) {
      setMessage({ severity: 'error', text: error?.response?.data?.message || error?.message || 'تعذر إنشاء مسودة الدفع التشغيلي.' });
    } finally {
      setSettlementBusy(null);
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      {/* Executive Page Header */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={2}
        sx={{ mb: 3 }}
      >
        <Box>
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            مركز المراجعة والاعتماد
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            كل الحركات والمشاريع والتسويات التي تنتظر قرار الاعتماد أو الترحيل مع فحص المرفقات والتحذيرات
          </Typography>
        </Box>
        <Button
          variant="contained"
          color="secondary"
          startIcon={<IconRefresh size={17} />}
          onClick={() => void load()}
          disabled={loading}
          sx={{ borderRadius: 2, fontWeight: 700 }}
        >
          {loading ? 'جاري التحديث...' : 'تحديث القائمة'}
        </Button>
      </Stack>

      {message && (
        <Alert severity={message.severity} onClose={() => setMessage(null)} sx={{ mb: 2.5, borderRadius: 2 }}>
          {message.text}
        </Alert>
      )}

      {/* Tier 1: Berry Signature Summary KPIs */}
      {loading ? (
        <Stack spacing={2.5}>
          <Grid container spacing={2}>
            {[0, 1, 2, 3].map((i) => (
              <Grid key={i} size={{ xs: 12, sm: 6, lg: 3 }}>
                <Skeleton variant="rounded" height={106} sx={{ borderRadius: 3 }} />
              </Grid>
            ))}
          </Grid>
          <Skeleton variant="rounded" height={340} sx={{ borderRadius: 3 }} />
        </Stack>
      ) : (
        <>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card
                sx={{
                  height: '100%',
                  borderRadius: 3,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                  boxShadow: '0 10px 24px rgba(13, 71, 161, 0.22)'
                }}
              >
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
                        حركات بانتظار القرار
                      </Typography>
                      <Typography variant="h2" sx={{ color: '#fff', fontWeight: 900, mt: 0.5 }}>
                        {items.length}
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 46, height: 46 }}>
                      <IconChecklist size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card
                sx={{
                  height: '100%',
                  borderRadius: 3,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #311b92 0%, #7e57c2 100%)',
                  boxShadow: '0 10px 24px rgba(49, 27, 146, 0.22)'
                }}
              >
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
                        مشاريع تحت المراجعة
                      </Typography>
                      <Typography variant="h2" sx={{ color: '#fff', fontWeight: 900, mt: 0.5 }}>
                        {pendingProjects.length}
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 46, height: 46 }}>
                      <IconBuildingSkyscraper size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card variant="outlined" sx={{ height: '100%', borderRadius: 3 }}>
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                        تسويات تاريخية
                      </Typography>
                      <Typography variant="h2" sx={{ fontWeight: 900, mt: 0.5, color: settlements.length ? 'warning.dark' : 'text.primary' }}>
                        {settlements.length}
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(217, 119, 6, 0.14)', color: 'warning.dark', width: 46, height: 46 }}>
                      <IconHistory size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
              <Card variant="outlined" sx={{ height: '100%', borderRadius: 3 }}>
                <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                        تنبيهات ومرفقات ناقصة
                      </Typography>
                      <Typography variant="h2" sx={{ fontWeight: 900, mt: 0.5, color: attentionCount ? 'warning.dark' : 'success.main' }}>
                        {attentionCount}
                      </Typography>
                    </Box>
                    <Avatar
                      sx={{
                        bgcolor: attentionCount ? 'rgba(217, 119, 6, 0.14)' : 'rgba(16, 185, 129, 0.14)',
                        color: attentionCount ? 'warning.dark' : 'success.main',
                        width: 46,
                        height: 46
                      }}
                    >
                      <IconAlertTriangle size={24} />
                    </Avatar>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Tier 2: Tabbed Review Workspace */}
          <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden' }}>
            <Box sx={{ px: 2, borderBottom: 1, borderColor: 'divider' }}>
              <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)} variant="scrollable" scrollButtons="auto">
                <Tab label={`حركات التشغيل (${items.length})`} />
                <Tab label={`مشاريع مستوردة (${pendingProjects.length})`} />
                <Tab label={`التسويات التاريخية (${settlements.length})`} />
              </Tabs>
            </Box>

            {/* Tab 0: Operational Workflow Queue */}
            {activeTab === 0 && (
              <Box sx={{ p: 2.5 }}>
                <Stack
                  direction={{ xs: 'column', md: 'row' }}
                  spacing={1.5}
                  alignItems={{ xs: 'stretch', md: 'center' }}
                  justifyContent="space-between"
                  sx={{ mb: 2.5 }}
                >
                  <TextField
                    size="small"
                    placeholder="ابحث بالمشروع أو البيان أو نوع الحركة..."
                    value={filter}
                    onChange={(event) => setFilter(event.target.value)}
                    sx={{ minWidth: { xs: '100%', md: 320 } }}
                    InputProps={{
                      startAdornment: (
                        <InputAdornment position="start">
                          <IconSearch size={17} />
                        </InputAdornment>
                      )
                    }}
                  />
                  {distinctTypes.length > 1 && (
                    <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
                      <Chip
                        clickable
                        size="small"
                        label="الكل"
                        color={typeFilter === 'all' ? 'primary' : 'default'}
                        variant={typeFilter === 'all' ? 'filled' : 'outlined'}
                        onClick={() => setTypeFilter('all')}
                        sx={{ fontWeight: 700 }}
                      />
                      {distinctTypes.map((type) => (
                        <Chip
                          key={type}
                          clickable
                          size="small"
                          label={labels[type] || type}
                          color={typeFilter === type ? 'secondary' : 'default'}
                          variant={typeFilter === type ? 'filled' : 'outlined'}
                          onClick={() => setTypeFilter(type)}
                          sx={{ fontWeight: 700 }}
                        />
                      ))}
                    </Stack>
                  )}
                </Stack>

                {visible.length === 0 ? (
                  <Alert severity="success" icon={<IconCheck />} sx={{ borderRadius: 2 }}>
                    لا توجد حركات معلقة متاحة للمراجعة حاليًا.
                  </Alert>
                ) : (
                  <Grid container spacing={2}>
                    {visible.map((item) => (
                      <Grid key={`${item.entityType}-${item.entityId}`} size={{ xs: 12, md: 6, xl: 4 }}>
                        <Card
                          variant="outlined"
                          sx={{
                            height: '100%',
                            borderRadius: 2.5,
                            borderColor: item.blockingChecks.length ? 'warning.main' : 'divider',
                            transition: 'box-shadow 180ms ease',
                            '&:hover': { boxShadow: '0 8px 22px rgba(15, 23, 42, 0.08)' }
                          }}
                        >
                          <CardContent>
                            <Stack spacing={1.25}>
                              <Stack direction="row" justifyContent="space-between" alignItems="flex-start" gap={1}>
                                <Stack spacing={0.25}>
                                  <Typography variant="h4" sx={{ fontWeight: 800 }}>
                                    {labels[item.entityType] || item.entityType}
                                  </Typography>
                                  <Typography variant="body2" color="text.secondary">
                                    {item.projectName}
                                  </Typography>
                                </Stack>
                                <Chip
                                  size="small"
                                  label={item.status}
                                  color={item.status === 'معتمد' ? 'success' : 'warning'}
                                  sx={{ fontWeight: 700 }}
                                />
                              </Stack>

                              <Typography variant="body2">{item.description || 'بدون بيان'}</Typography>
                              <Typography variant="h4" color="primary.main" sx={{ fontWeight: 800, fontVariantNumeric: 'tabular-nums' }}>
                                {item.amount.toLocaleString('ar-EG')} ج.م
                              </Typography>

                              {item.entityType === 'supplier-supply' && item.status === 'مقبول فنياً' && (
                                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                                  بانتظار الاعتماد المالي
                                </Typography>
                              )}

                              {!item.hasAttachment && (
                                <Alert severity="warning" icon={<IconAlertTriangle size={17} />} sx={{ py: 0.25, borderRadius: 2 }}>
                                  لا يوجد مرفق مؤيد لهذه الحركة.
                                </Alert>
                              )}
                              {item.blockingChecks.length > 0 && (
                                <Alert severity="error" icon={<IconShieldCheck size={17} />} sx={{ py: 0.25, borderRadius: 2 }}>
                                  <Stack spacing={0.25}>
                                    {item.blockingChecks.map((check) => (
                                      <Typography key={check} variant="caption">
                                        {check}
                                      </Typography>
                                    ))}
                                  </Stack>
                                </Alert>
                              )}
                              {item.warnings.length > 0 && (
                                <Typography variant="caption" color="warning.dark">
                                  {item.warnings.join('، ')}
                                </Typography>
                              )}

                              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ pt: 0.5 }}>
                                <Button
                                  component={Link}
                                  to={detailPath(item)}
                                  size="small"
                                  variant="outlined"
                                  endIcon={<IconExternalLink size={15} />}
                                  sx={{ borderRadius: 2, fontWeight: 700 }}
                                >
                                  فتح التفاصيل
                                </Button>
                                {item.canAcceptTechnically && (
                                  <Button size="small" variant="contained" disabled={workflowBusy} onClick={() => void run(item, 'قبول فني')} sx={{ borderRadius: 2, fontWeight: 700 }}>
                                    قبول فني
                                  </Button>
                                )}
                                {item.canApproveFinancially && (
                                  <Button size="small" color="success" variant="contained" disabled={workflowBusy} onClick={() => void run(item, 'اعتماد مالي')} sx={{ borderRadius: 2, fontWeight: 700 }}>
                                    اعتماد مالي
                                  </Button>
                                )}
                                {item.entityType !== 'supplier-supply' && item.canApprove && (
                                  <Button size="small" variant="contained" disabled={workflowBusy} onClick={() => void run(item, 'اعتماد')} sx={{ borderRadius: 2, fontWeight: 700 }}>
                                    اعتماد
                                  </Button>
                                )}
                                {item.canPost && (
                                  <Button size="small" color="success" variant="contained" disabled={workflowBusy} onClick={() => void run(item, 'ترحيل')} sx={{ borderRadius: 2, fontWeight: 700 }}>
                                    ترحيل
                                  </Button>
                                )}
                                {item.canReject && (
                                  <Button size="small" color="error" disabled={workflowBusy} onClick={() => setRejectSupply(item)} sx={{ borderRadius: 2, fontWeight: 700 }}>
                                    رفض التوريدة
                                  </Button>
                                )}
                              </Stack>
                            </Stack>
                          </CardContent>
                        </Card>
                      </Grid>
                    ))}
                  </Grid>
                )}
              </Box>
            )}

            {/* Tab 1: Pending Imported Projects */}
            {activeTab === 1 && (
              <Box sx={{ p: 2.5 }}>
                {pendingProjects.length === 0 ? (
                  <Alert severity="success" icon={<IconCheck />} sx={{ borderRadius: 2 }}>
                    لا توجد مشاريع مستوردة بانتظار الاعتماد.
                  </Alert>
                ) : (
                  <Grid container spacing={2}>
                    {pendingProjects.map((project) => (
                      <Grid key={project.id} size={{ xs: 12, md: 6, xl: 4 }}>
                        <Card variant="outlined" sx={{ height: '100%', borderRadius: 2.5, borderColor: 'warning.main' }}>
                          <CardContent>
                            <Stack spacing={1.25}>
                              <Stack direction="row" justifyContent="space-between" alignItems="flex-start" gap={1}>
                                <BoxPlaceholder project={project} />
                                <Chip size="small" label="تحت المراجعة" color="warning" sx={{ fontWeight: 700 }} />
                              </Stack>
                              <Typography variant="body2" color="text.secondary">
                                الكود: {project.code}
                              </Typography>
                              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                                <Button
                                  component={Link}
                                  to={`/emg/projects/${project.id}`}
                                  size="small"
                                  variant="outlined"
                                  endIcon={<IconExternalLink size={15} />}
                                  sx={{ borderRadius: 2, fontWeight: 700 }}
                                >
                                  فتح المشروع
                                </Button>
                                {canApproveProject && (
                                  <Button
                                    size="small"
                                    variant="contained"
                                    color="success"
                                    disabled={projectBusy === project.id}
                                    onClick={() => void approveProject(project)}
                                    sx={{ borderRadius: 2, fontWeight: 700 }}
                                  >
                                    تفعيل المشروع
                                  </Button>
                                )}
                                {canRejectProject && (
                                  <Button
                                    size="small"
                                    variant="outlined"
                                    color="error"
                                    disabled={projectBusy === project.id}
                                    onClick={() => setRejectProject(project)}
                                    sx={{ borderRadius: 2, fontWeight: 700 }}
                                  >
                                    رفض
                                  </Button>
                                )}
                              </Stack>
                            </Stack>
                          </CardContent>
                        </Card>
                      </Grid>
                    ))}
                  </Grid>
                )}
              </Box>
            )}

            {/* Tab 2: Historical Settlements */}
            {activeTab === 2 && (
              <Box sx={{ p: 2.5 }}>
                {settlements.length === 0 ? (
                  <Alert severity="success" icon={<IconCheck />} sx={{ borderRadius: 2 }}>
                    لا توجد تسويات تاريخية بانتظار المراجعة.
                  </Alert>
                ) : (
                  <Stack spacing={1.5}>
                    <Alert severity="info" sx={{ borderRadius: 2 }}>
                      تأكيد العلاقة لا ينشئ قيدًا. عند إتاحة بيئة QA المعتمدة فقط يمكن إنشاء مسودة دفع تشغيلية، ثم تمر عبر دورة الدفع العادية.
                    </Alert>
                    {settlements.map((review) => (
                      <Card key={review.batchId} variant="outlined" sx={{ borderRadius: 2.5 }}>
                        <CardContent>
                          <Stack spacing={1.25}>
                            <Stack direction="row" justifyContent="space-between" alignItems="center" gap={1}>
                              <Typography fontWeight={800}>{review.sourceFileName}</Typography>
                              <Chip size="small" label={`دفعة #${review.batchId} · ${review.batchStatus}`} color="warning" sx={{ fontWeight: 700 }} />
                            </Stack>
                            {review.records.map((record) => (
                              <Stack
                                key={record.recordId}
                                direction={{ xs: 'column', md: 'row' }}
                                spacing={1.25}
                                alignItems={{ md: 'center' }}
                                sx={{ borderTop: '1px solid', borderColor: 'divider', pt: 1.25 }}
                              >
                                <Box sx={{ flex: 1 }}>
                                  <Typography fontWeight={700}>
                                    {settlementLabels[record.businessEventType] || record.businessEventType} ·{' '}
                                    {record.amount?.toLocaleString('ar-EG') || 'بدون قيمة'} {localizeCurrency(record.currency)}
                                  </Typography>
                                  <Typography variant="body2" color="text.secondary">
                                    {record.sheetName} / {record.sourceCoordinate} · {record.stream}
                                    {record.party ? ` · ${record.party}` : ''}
                                  </Typography>
                                  <Typography variant="caption" color="text.secondary">
                                    الحالة: {record.semanticValidationState} · {record.reviewReason || 'تحتاج مراجعة محاسبية'}
                                  </Typography>
                                  {record.operationalPaymentId && (
                                    <Box sx={{ mt: 0.5 }}>
                                      <Chip
                                        size="small"
                                        color="success"
                                        label={`دفع تشغيلي #${record.operationalPaymentId} · ${record.operationalPaymentStatus || 'مرتبط'}`}
                                      />
                                    </Box>
                                  )}
                                </Box>
                                {!record.isReferenceOnly && !record.isDuplicateCandidate && (
                                  <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                                    {record.businessEventType === 'Payment' && (
                                      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ minWidth: { md: 420 } }}>
                                        <TextField
                                          size="small"
                                          label="معرّف التوريدة"
                                          value={settlementDrafts[record.recordId]?.obligationId || ''}
                                          onChange={(event) =>
                                            setSettlementDrafts((current) => ({
                                              ...current,
                                              [record.recordId]: {
                                                obligationId: event.target.value,
                                                obligationAmount: current[record.recordId]?.obligationAmount || '',
                                                party: current[record.recordId]?.party || ''
                                              }
                                            }))
                                          }
                                        />
                                        <TextField
                                          size="small"
                                          label="قيمة الالتزام"
                                          value={settlementDrafts[record.recordId]?.obligationAmount || ''}
                                          onChange={(event) =>
                                            setSettlementDrafts((current) => ({
                                              ...current,
                                              [record.recordId]: {
                                                obligationId: current[record.recordId]?.obligationId || '',
                                                obligationAmount: event.target.value,
                                                party: current[record.recordId]?.party || ''
                                              }
                                            }))
                                          }
                                        />
                                        <TextField
                                          size="small"
                                          label="المورد"
                                          value={settlementDrafts[record.recordId]?.party || ''}
                                          onChange={(event) =>
                                            setSettlementDrafts((current) => ({
                                              ...current,
                                              [record.recordId]: {
                                                obligationId: current[record.recordId]?.obligationId || '',
                                                obligationAmount: current[record.recordId]?.obligationAmount || '',
                                                party: event.target.value
                                              }
                                            }))
                                          }
                                        />
                                      </Stack>
                                    )}
                                    <Button
                                      size="small"
                                      variant="contained"
                                      color="success"
                                      disabled={settlementBusy === record.recordId}
                                      onClick={() => void reviewSettlement(review, record.recordId, 'confirm')}
                                      sx={{ borderRadius: 2, fontWeight: 700 }}
                                    >
                                      تأكيد العلاقة
                                    </Button>
                                    {record.businessEventType === 'Payment' &&
                                      record.linkedObligationId &&
                                      !record.operationalPaymentId &&
                                      record.semanticValidationState === 'SETTLEMENT_CONFIRMED' && (
                                        <Button
                                          size="small"
                                          variant="outlined"
                                          color="primary"
                                          disabled={settlementBusy === record.recordId}
                                          onClick={() => void recognizeSettlement(review, record.recordId)}
                                          sx={{ borderRadius: 2, fontWeight: 700 }}
                                        >
                                          إنشاء مسودة دفع تشغيلية
                                        </Button>
                                      )}
                                    <Button
                                      size="small"
                                      variant="outlined"
                                      color="warning"
                                      disabled={settlementBusy === record.recordId}
                                      onClick={() => void reviewSettlement(review, record.recordId, 'reject')}
                                      sx={{ borderRadius: 2, fontWeight: 700 }}
                                    >
                                      إبقاء غير محسوم
                                    </Button>
                                  </Stack>
                                )}
                              </Stack>
                            ))}
                          </Stack>
                        </CardContent>
                      </Card>
                    ))}
                  </Stack>
                )}
              </Box>
            )}
          </MainCard>
        </>
      )}

      <Dialog open={Boolean(rejectProject || rejectSupply)} onClose={closeRejection} fullWidth maxWidth="sm">
        <DialogTitle>{rejectSupply ? 'رفض التوريدة' : 'رفض ترحيل المشروع'}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            {rejectSupply
              ? 'سيُسجّل السبب وتبدأ التوريدة المراجعة الفنية من جديد عند إعادة إرسالها.'
              : 'سيتم تسجيل سبب الرفض في سجل المراجعة ولن يتم تفعيل المشروع.'}
          </Typography>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            label="سبب الرفض"
            value={rejectNotes}
            onChange={(event) => setRejectNotes(event.target.value)}
            slotProps={{ htmlInput: { maxLength: 2000 } }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={closeRejection} disabled={projectBusy !== null || workflowBusy}>
            إلغاء
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => {
              if (rejectSupply) void run(rejectSupply, 'رفض', rejectNotes.trim());
              else void rejectPendingProject();
            }}
            disabled={projectBusy !== null || workflowBusy || !rejectNotes.trim()}
          >
            تأكيد الرفض
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function BoxPlaceholder({ project }: { project: EmgProject }) {
  return (
    <Stack spacing={0.25}>
      <Typography variant="h4" sx={{ fontWeight: 800 }}>
        {project.name}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {project.clientName || 'بدون عميل محدد'}
      </Typography>
    </Stack>
  );
}
