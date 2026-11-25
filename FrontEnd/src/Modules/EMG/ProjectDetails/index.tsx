import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert,
  Avatar,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  InputAdornment,
  Skeleton,
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
import {
  IconArrowLeft,
  IconArrowRight,
  IconBuilding,
  IconChartBar,
  IconCircleCheck,
  IconDownload,
  IconFileAnalytics,
  IconMapPin,
  IconReceipt,
  IconSearch,
  IconTool,
  IconUser
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import {
  approveEmgProjectImport,
  downloadEmgProjectWorkbook,
  EmgOperationalAudit,
  EmgProject,
  EmgProjectReport,
  getEmgProject,
  getEmgProjectAudit,
  getEmgProjectReport,
  rejectEmgProjectImport
} from 'api/emg';
import useAuth from 'hooks/useAuth';
import useConfig from 'hooks/useConfig';
import { hasPermission } from '../roleAccess';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;
const formatDate = (value: string) => new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium' }).format(new Date(value));

const workSections = [
  { path: 'movements', permission: 'site-movements.view', ar: 'حركة الموقع اليومية', en: 'Site Movements' },
  { path: 'excavation', permission: 'excavation.view', ar: 'أعمال الحفر', en: 'Excavation' },
  { path: 'concrete', permission: 'concrete.view', ar: 'الخرسانات', en: 'Concrete' },
  { path: 'steel', permission: 'steel.view', ar: 'الحديد', en: 'Steel' },
  { path: 'finishing', permission: 'finishing.view', ar: 'التشطيبات', en: 'Finishing' }
];

function GradientKpiCard({
  label,
  value,
  icon,
  variant = 'blue'
}: {
  label: string;
  value: string;
  icon: React.ReactNode;
  variant?: 'blue' | 'purple';
}) {
  const gradient =
    variant === 'purple'
      ? 'linear-gradient(135deg, #311b92 0%, #5e35b1 55%, #7e57c2 100%)'
      : 'linear-gradient(135deg, #0d47a1 0%, #1565c0 55%, #1e88e5 100%)';

  return (
    <Card
      sx={{
        height: '100%',
        borderRadius: 3,
        color: '#fff',
        background: gradient,
        boxShadow: variant === 'purple' ? '0 10px 24px rgba(49, 27, 146, 0.22)' : '0 10px 24px rgba(13, 71, 161, 0.22)'
      }}
    >
      <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.5}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
              {label}
            </Typography>
            <Typography variant="h3" sx={{ mt: 0.6, color: '#fff', fontWeight: 900, fontVariantNumeric: 'tabular-nums' }}>
              {value}
            </Typography>
          </Box>
          <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 44, height: 44, flexShrink: 0 }}>{icon}</Avatar>
        </Stack>
      </CardContent>
    </Card>
  );
}

function SolidMetricCard({
  label,
  value,
  tone,
  icon
}: {
  label: string;
  value: string;
  tone: 'error' | 'warning';
  icon: React.ReactNode;
}) {
  const accent = tone === 'error' ? 'error.main' : 'warning.dark';
  const softBg = tone === 'error' ? 'rgba(239, 68, 68, 0.12)' : 'rgba(217, 119, 6, 0.14)';

  return (
    <Card variant="outlined" sx={{ height: '100%', borderRadius: 3, bgcolor: 'background.paper' }}>
      <CardContent sx={{ p: 2.25, '&:last-child': { pb: 2.25 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.5}>
          <Box sx={{ minWidth: 0 }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
              {label}
            </Typography>
            <Typography variant="h3" sx={{ mt: 0.6, color: accent, fontWeight: 900, fontVariantNumeric: 'tabular-nums' }}>
              {value}
            </Typography>
          </Box>
          <Avatar sx={{ bgcolor: softBg, color: accent, width: 44, height: 44, flexShrink: 0 }}>{icon}</Avatar>
        </Stack>
      </CardContent>
    </Card>
  );
}

function ProjectSkeleton() {
  return (
    <Stack spacing={2.5} sx={{ pb: 4 }}>
      <Skeleton variant="text" width={200} height={24} />
      <Skeleton variant="rounded" height={175} sx={{ borderRadius: 3 }} />
      <Grid container spacing={2}>
        {[0, 1, 2, 3].map((i) => (
          <Grid key={i} size={{ xs: 12, sm: 6, md: 3 }}>
            <Skeleton variant="rounded" height={106} sx={{ borderRadius: 3 }} />
          </Grid>
        ))}
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Skeleton variant="rounded" height={300} sx={{ borderRadius: 3 }} />
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <Skeleton variant="rounded" height={300} sx={{ borderRadius: 3 }} />
        </Grid>
      </Grid>
    </Stack>
  );
}

export default function EmgProjectDetails() {
  const { projectId } = useParams();
  return <ProjectDetails key={projectId} id={Number(projectId)} />;
}

function ProjectDetails({ id }: { id: number }) {
  const { user } = useAuth();
  const {
    state: { i18n, themeDirection }
  } = useConfig();
  const isArabic = i18n === 'ar';
  const canViewReports = hasPermission(user, 'reports.view');
  const canExport = hasPermission(user, 'reports.export');
  const canApproveImport = hasPermission(user, 'project-imports.approve');
  const canRejectImport = hasPermission(user, 'project-imports.reject');
  const allowedSections = workSections.filter(({ permission }) => hasPermission(user, permission));
  const invalidId = !Number.isInteger(id) || id <= 0;

  const [project, setProject] = useState<EmgProject | null>(null);
  const [projectError, setProjectError] = useState<'unavailable' | 'request' | null>(null);
  const [downloading, setDownloading] = useState(false);
  const [decisionBusy, setDecisionBusy] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectNotes, setRejectNotes] = useState('');
  const [decisionMessage, setDecisionMessage] = useState<string | null>(null);

  useEffect(() => {
    if (invalidId) return;
    let active = true;
    getEmgProject(id)
      .then((data) => {
        if (!active) return;
        if (data?.id === id) setProject(data);
        else setProjectError('request');
      })
      .catch((error) => {
        if (active) setProjectError(error?.status === 404 || error?.status === 403 ? 'unavailable' : 'request');
      });
    return () => {
      active = false;
    };
  }, [id, invalidId]);

  const downloadWorkbook = async () => {
    setDownloading(true);
    try {
      await downloadEmgProjectWorkbook(id);
    } finally {
      setDownloading(false);
    }
  };

  const approveImport = async () => {
    setDecisionBusy(true);
    setDecisionMessage(null);
    try {
      const updated = await approveEmgProjectImport(id, 'تمت مراجعة الترحيل من صفحة المشروع');
      setProject(updated);
      setDecisionMessage('تم تفعيل المشروع بنجاح. الحركات المسودة تستكمل دورة الاعتماد الخاصة بها.');
    } catch {
      setDecisionMessage('تعذر اعتماد الترحيل. راجع الصلاحيات وحالة المشروع.');
    } finally {
      setDecisionBusy(false);
    }
  };

  const rejectImport = async () => {
    if (!rejectNotes.trim()) return;
    setDecisionBusy(true);
    setDecisionMessage(null);
    try {
      const updated = await rejectEmgProjectImport(id, rejectNotes.trim());
      setProject(updated);
      setRejectOpen(false);
      setRejectNotes('');
      setDecisionMessage('تم رفض ترحيل المشروع وتسجيل السبب في سجل المراجعة.');
    } catch {
      setDecisionMessage('تعذر رفض الترحيل. راجع الصلاحيات وحالة المشروع.');
    } finally {
      setDecisionBusy(false);
    }
  };

  if (invalidId) return <Alert severity="error">{isArabic ? 'رقم المشروع غير صحيح.' : 'Invalid project ID.'}</Alert>;
  if (projectError === 'unavailable')
    return (
      <Alert severity="warning">
        {isArabic ? 'المشروع غير متاح لحسابك.' : 'This project is unavailable to your account.'}
      </Alert>
    );
  if (projectError)
    return <Alert severity="error">{isArabic ? 'تعذر تحميل بيانات المشروع.' : 'Could not load project details.'}</Alert>;
  if (!project) return <ProjectSkeleton />;

  return (
    <Box sx={{ pb: 4, minWidth: 0, maxWidth: '100%' }}>
      <Breadcrumbs sx={{ mb: 1.5 }}>
        <Typography component={Link} to="/emg/projects" variant="body2" color="text.secondary" sx={{ textDecoration: 'none', fontWeight: 700 }}>
          {isArabic ? 'المشاريع' : 'Projects'}
        </Typography>
        <Typography variant="body2" color="primary" sx={{ fontWeight: 700 }}>
          {project.name}
        </Typography>
      </Breadcrumbs>

      <MainCard sx={{ borderRadius: 3, minWidth: 0, maxWidth: '100%' }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ xs: 'flex-start', md: 'center' }}
          spacing={2}
          sx={{ minWidth: 0, maxWidth: '100%' }}
        >
          <Stack direction="row" spacing={2} alignItems="center" sx={{ minWidth: 0, maxWidth: '100%' }}>
            <Avatar
              variant="rounded"
              sx={{
                width: 54,
                height: 54,
                borderRadius: 2.5,
                background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                color: '#fff',
                flexShrink: 0,
                boxShadow: '0 8px 18px rgba(13, 71, 161, 0.24)'
              }}
            >
              <IconBuilding size={28} />
            </Avatar>
            <Box sx={{ minWidth: 0, overflowWrap: 'anywhere' }}>
              <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
                <Typography variant="h2" component="h1" sx={{ fontWeight: 850 }}>
                  {project.name}
                </Typography>
                <Chip size="small" label={project.code} variant="outlined" sx={{ fontWeight: 700 }} />
                <Chip
                  size="small"
                  label={project.status}
                  color={project.status === 'نشط' ? 'success' : 'warning'}
                  sx={{ fontWeight: 700 }}
                />
              </Stack>
              <Stack direction="row" spacing={2} flexWrap="wrap" useFlexGap sx={{ mt: 0.75, color: 'text.secondary' }}>
                <Stack direction="row" spacing={0.5} alignItems="center">
                  <IconMapPin size={15} />
                  <Typography variant="body2">{project.location || '—'}</Typography>
                </Stack>
                <Stack direction="row" spacing={0.5} alignItems="center">
                  <IconUser size={15} />
                  <Typography variant="body2">
                    {isArabic ? 'المهندس' : 'Engineer'}: {project.engineerName || '—'}
                  </Typography>
                </Stack>
              </Stack>
            </Box>
          </Stack>

          <Stack direction="row" useFlexGap flexWrap="wrap" spacing={1} sx={{ flexShrink: 0 }}>
            {canExport && (
              <Button
                size="small"
                variant="outlined"
                color="secondary"
                disabled={downloading}
                onClick={downloadWorkbook}
                startIcon={<IconDownload size={16} />}
                sx={{ borderRadius: 2, fontWeight: 700 }}
              >
                {downloading ? (isArabic ? 'جاري التصدير...' : 'Exporting…') : isArabic ? 'تصدير Excel' : 'Export Excel'}
              </Button>
            )}
            <Button
              component={Link}
              to="/emg/projects"
              size="small"
              endIcon={themeDirection === 'rtl' ? <IconArrowRight size={17} /> : <IconArrowLeft size={17} />}
              variant="outlined"
              color="inherit"
              sx={{ borderRadius: 2, fontWeight: 700 }}
            >
              {isArabic ? 'رجوع للمشاريع' : 'Back to projects'}
            </Button>
          </Stack>
        </Stack>

        {(allowedSections.length > 0 || canViewReports) && (
          <>
            <Divider sx={{ my: 2.25 }} />
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" alignItems="center">
              <Typography component="h2" variant="caption" color="text.secondary" sx={{ fontWeight: 800, ml: 0.5 }}>
                {isArabic ? 'أقسام المشروع:' : 'Sections:'}
              </Typography>
              {allowedSections.map((section) => (
                <Button
                  key={section.path}
                  component={Link}
                  to={`/emg/projects/${id}/${section.path}`}
                  size="small"
                  variant={section.path === 'movements' ? 'contained' : 'outlined'}
                  color={section.path === 'movements' ? 'secondary' : 'primary'}
                  sx={{ borderRadius: 2, fontWeight: 700 }}
                >
                  {isArabic ? section.ar : section.en}
                </Button>
              ))}
              {canViewReports && (
                <Button
                  component={Link}
                  to={`/emg/reports/${id}`}
                  size="small"
                  variant="outlined"
                  color="secondary"
                  startIcon={<IconChartBar size={15} />}
                  sx={{ borderRadius: 2, fontWeight: 700 }}
                >
                  {isArabic ? 'كشف الحساب النهائي' : 'Final Account Report'}
                </Button>
              )}
            </Stack>
          </>
        )}

        {project.status === 'تحت المراجعة' && (canApproveImport || canRejectImport) && (
          <>
            <Divider sx={{ my: 2 }} />
            <Alert severity="warning" sx={{ mb: 1.5, borderRadius: 2 }}>
              هذا المشروع ناتج عن ترحيل ملف Excel وينتظر قرار الاعتماد قبل تفعيله.
            </Alert>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              {canApproveImport && (
                <Button variant="contained" color="success" disabled={decisionBusy} onClick={approveImport} sx={{ borderRadius: 2, fontWeight: 700 }}>
                  تفعيل المشروع
                </Button>
              )}
              {canRejectImport && (
                <Button variant="outlined" color="error" disabled={decisionBusy} onClick={() => setRejectOpen(true)} sx={{ borderRadius: 2, fontWeight: 700 }}>
                  رفض الترحيل
                </Button>
              )}
            </Stack>
          </>
        )}

        {decisionMessage && (
          <Alert severity={decisionMessage.includes('بنجاح') || decisionMessage.includes('تم رفض') ? 'success' : 'error'} sx={{ mt: 1.5, borderRadius: 2 }}>
            {decisionMessage}
          </Alert>
        )}
      </MainCard>

      <Dialog open={rejectOpen} onClose={() => !decisionBusy && setRejectOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>رفض ترحيل المشروع</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={4}
            label="سبب الرفض"
            value={rejectNotes}
            onChange={(event) => setRejectNotes(event.target.value)}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectOpen(false)} disabled={decisionBusy}>
            إلغاء
          </Button>
          <Button onClick={rejectImport} disabled={decisionBusy || !rejectNotes.trim()} color="error" variant="contained">
            تأكيد الرفض
          </Button>
        </DialogActions>
      </Dialog>

      {canViewReports && <FinancialPanels projectId={id} isArabic={isArabic} />}
    </Box>
  );
}

function FinancialPanels({ projectId, isArabic }: { projectId: number; isArabic: boolean }) {
  const [report, setReport] = useState<EmgProjectReport | 'failed' | null>(null);
  const [audit, setAudit] = useState<EmgOperationalAudit | 'failed' | null>(null);
  const [movementSearch, setMovementSearch] = useState('');

  useEffect(() => {
    let active = true;
    getEmgProjectReport(projectId)
      .then((data) => {
        if (active) setReport(data);
      })
      .catch(() => {
        if (active) setReport('failed');
      });
    getEmgProjectAudit(projectId)
      .then((data) => {
        if (active) setAudit(data);
      })
      .catch(() => {
        if (active) setAudit('failed');
      });
    return () => {
      active = false;
    };
  }, [projectId]);

  const account = report && report !== 'failed' ? report.finalAccount : null;
  const filteredMovements = useMemo(() => {
    if (!report || report === 'failed') return [];
    const q = movementSearch.trim().toLowerCase();
    const list = q
      ? report.movements.filter((m) => `${m.type} ${m.description} ${m.supplierName || ''}`.toLowerCase().includes(q))
      : report.movements;
    return list.slice(0, 10);
  }, [report, movementSearch]);

  return (
    <>
      {report === null ? (
        <Grid container spacing={2} sx={{ mt: 1.5 }}>
          {[0, 1, 2, 3].map((i) => (
            <Grid key={i} size={{ xs: 12, sm: 6, md: 3 }}>
              <Skeleton variant="rounded" height={106} sx={{ borderRadius: 3 }} />
            </Grid>
          ))}
        </Grid>
      ) : (
        account && (
          <Grid container spacing={2} sx={{ mt: 1.5 }}>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <GradientKpiCard label="إجمالي الداخل" value={formatMoney(account.totalIncoming)} icon={<IconReceipt size={22} />} variant="blue" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <GradientKpiCard label="صافي المشروع" value={formatMoney(account.netAfterOfficeShare)} icon={<IconCircleCheck size={22} />} variant="purple" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <SolidMetricCard label="إجمالي الصادر" value={formatMoney(account.totalOutgoing ?? 0)} tone="error" icon={<IconTool size={22} />} />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <SolidMetricCard label="نصيب المكتب" value={formatMoney(account.officeShareAmount)} tone="warning" icon={<IconFileAnalytics size={22} />} />
            </Grid>
          </Grid>
        )
      )}

      <Grid container spacing={2} sx={{ mt: 1.5, minWidth: 0, maxWidth: '100%' }}>
        <Grid size={{ xs: 12, md: 7 }}>
          <MainCard
            sx={{ borderRadius: 3, height: '100%' }}
            title={isArabic ? 'آخر حركات الموقع' : 'Recent site movements'}
            secondary={
              report && report !== 'failed' && report.movements.length > 0 ? (
                <TextField
                  size="small"
                  placeholder="بحث سريع..."
                  value={movementSearch}
                  onChange={(e) => setMovementSearch(e.target.value)}
                  sx={{ width: { xs: 150, sm: 210 } }}
                  InputProps={{
                    startAdornment: (
                      <InputAdornment position="start">
                        <IconSearch size={15} />
                      </InputAdornment>
                    )
                  }}
                />
              ) : undefined
            }
          >
            {report === 'failed' ? (
              <Alert severity="warning">{isArabic ? 'تعذر تحميل التقرير المالي.' : 'Could not load the financial report.'}</Alert>
            ) : report === null ? (
              <Stack spacing={1.25}>
                {[0, 1, 2, 3, 4].map((i) => (
                  <Skeleton key={i} variant="rounded" height={36} />
                ))}
              </Stack>
            ) : filteredMovements.length === 0 ? (
              <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                {movementSearch ? 'لا توجد حركات مطابقة للبحث.' : 'لا توجد حركات مسجلة لهذا المشروع.'}
              </Typography>
            ) : (
              <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 800 }}>التاريخ</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>نوع الحركة</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>الوصف</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 800 }}>
                        المبلغ
                      </TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {filteredMovements.map((movement) => (
                      <TableRow key={movement.id} hover>
                        <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(movement.date)}</TableCell>
                        <TableCell>
                          <Chip size="small" variant="outlined" label={movement.type} sx={{ fontWeight: 600 }} />
                        </TableCell>
                        <TableCell>{movement.description || '—'}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                          {formatMoney(movement.amount)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </MainCard>
        </Grid>

        <Grid size={{ xs: 12, md: 5 }}>
          <MainCard sx={{ borderRadius: 3, height: '100%' }} title={isArabic ? 'تدقيق الأعمال' : 'Operational audit'}>
            {audit === 'failed' ? (
              <Alert severity="warning">{isArabic ? 'تعذر تحميل تدقيق الأعمال.' : 'Could not load the operational audit.'}</Alert>
            ) : audit === null ? (
              <Stack spacing={1.25}>
                {[0, 1, 2, 3].map((i) => (
                  <Skeleton key={i} variant="rounded" height={44} />
                ))}
              </Stack>
            ) : (
              <>
                <Grid container spacing={1.25}>
                  {[
                    ['الحفر', audit.excavationTotal],
                    ['الخرسانات', audit.concreteTotal],
                    ['الحديد', audit.steelTotal],
                    ['التشطيبات', audit.finishingTotal],
                    ['توريدات الموردين', audit.supplierSuppliesTotal],
                    ['مدفوعات الموردين', audit.supplierPaymentsTotal]
                  ].map(([label, value]) => (
                    <Grid key={String(label)} size={6}>
                      <Box
                        sx={{
                          p: 1.5,
                          borderRadius: 2,
                          border: '1px solid',
                          borderColor: 'divider',
                          bgcolor: 'background.default'
                        }}
                      >
                        <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700 }}>
                          {label}
                        </Typography>
                        <Typography variant="subtitle1" sx={{ fontWeight: 800, mt: 0.25, fontVariantNumeric: 'tabular-nums' }}>
                          {formatMoney(Number(value))}
                        </Typography>
                      </Box>
                    </Grid>
                  ))}
                </Grid>
                <Divider sx={{ my: 2 }} />
                <Stack spacing={1}>
                  {audit.checks.map((check) => (
                    <Stack key={check.name} direction="row" spacing={1} alignItems="center" justifyContent="space-between">
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {check.name}
                      </Typography>
                      <Chip
                        size="small"
                        label={check.isValid ? 'سليم' : 'مراجعة'}
                        color={check.isValid ? 'success' : 'error'}
                        sx={{ fontWeight: 700 }}
                      />
                    </Stack>
                  ))}
                </Stack>
              </>
            )}
          </MainCard>
        </Grid>
      </Grid>
    </>
  );
}
