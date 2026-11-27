import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Alert,
  Avatar,
  Box,
  Breadcrumbs,
  Button,
  Chip,
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
  IconChevronLeft,
  IconClipboardList,
  IconDownload,
  IconHammer,
  IconMapPin,
  IconPaint,
  IconSearch,
  IconShovel,
  IconUser,
  IconWall
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import {
  downloadEmgProjectWorkbook,
  EmgOperationalAudit,
  EmgProject,
  EmgProjectReport,
  getEmgProject,
  getEmgProjectAudit,
  getEmgProjectReport,
} from 'api/emg';
import useAuth from 'hooks/useAuth';
import useConfig from 'hooks/useConfig';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import { formatEmgMoney } from 'utils/emgFormatting';
import { hasPermission } from '../roleAccess';
import FinancialSummary from '../FinancialSummary';
import { extractSectionFinancialSummary, WorkSectionKey } from '../sectionFinancials';

const formatMoney = (value: number | null | undefined) => formatEmgMoney(value);
const formatDate = (value: string | null) => value ? new Intl.DateTimeFormat('ar-EG-u-nu-latn', { dateStyle: 'medium' }).format(new Date(value)) : '—';

const workSections = [
  { path: 'movements', permission: 'site-movements.view', ar: 'حركة الموقع اليومية', en: 'Site Movements' },
  { path: 'advances', permission: 'advances.view', ar: 'الدفعات تحت الحساب', en: 'Advances' },
  { path: 'excavation', permission: 'excavation.view', ar: 'أعمال الحفر', en: 'Excavation' },
  { path: 'concrete', permission: 'concrete.view', ar: 'الخرسانات', en: 'Concrete' },
  { path: 'steel', permission: 'steel.view', ar: 'الحديد', en: 'Steel' },
  { path: 'finishing', permission: 'finishing.view', ar: 'التشطيبات', en: 'Finishing' }
];

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
  const { setActiveProjectId } = useActiveProject();
  const {
    state: { i18n, themeDirection }
  } = useConfig();
  const isArabic = i18n === 'ar';
  const canViewReports = hasPermission(user, 'reports.view');
  const canExport = hasPermission(user, 'reports.export');
  const allowedSections = workSections.filter(({ permission }) => hasPermission(user, permission));
  const invalidId = !Number.isInteger(id) || id <= 0;

  const [project, setProject] = useState<EmgProject | null>(null);
  const [projectError, setProjectError] = useState<'unavailable' | 'request' | null>(null);
  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    if (invalidId) return;
    setActiveProjectId(id);
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

      </MainCard>

      <FinancialPanels project={project} canViewReports={canViewReports} isArabic={isArabic} allowedSections={allowedSections} />
    </Box>
  );
}

const SECTION_CARDS_META: Record<WorkSectionKey, { icon: React.ReactNode; color: string }> = {
  movements: { icon: <IconClipboardList size={18} />, color: '#2563eb' },
  excavation: { icon: <IconShovel size={18} />, color: '#d97706' },
  concrete: { icon: <IconWall size={18} />, color: '#7c3aed' },
  steel: { icon: <IconHammer size={18} />, color: '#0d9488' },
  finishing: { icon: <IconPaint size={18} />, color: '#ea580c' }
};

function FinancialPanels({
  project,
  canViewReports,
  isArabic,
  allowedSections
}: {
  project: EmgProject;
  canViewReports: boolean;
  isArabic: boolean;
  allowedSections: typeof workSections;
}) {
  const projectId = project.id;
  const [report, setReport] = useState<EmgProjectReport | 'failed' | null>(null);
  const [audit, setAudit] = useState<EmgOperationalAudit | 'failed' | null>(null);
  const [movementSearch, setMovementSearch] = useState('');

  useEffect(() => {
    let active = true;
    if (!canViewReports) return;
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
  }, [projectId, canViewReports]);

  const account = report && report !== 'failed' ? report.finalAccount : null;
  const verifiedBalance = (amount?: number | null) => amount == null ? 'غير محدد' : formatMoney(amount);
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
          <Stack spacing={2} sx={{ mt: 2 }}>
            <FinancialSummary
              title={isArabic ? 'الملخص المالي للمشروع' : 'Project Financial Summary'}
              totalWorks={project.totalWorks}
              totalPayments={project.historicalFundingUnknown && !project.totalPayments ? null : project.totalPayments}
              cashBalance={project.historicalFundingUnknown ? null : project.cashBalance}
              cashBalanceStatus={project.cashBalanceStatus}

            />

            {project.financialWarnings?.map(warning => <Alert key={warning} severity="warning">{warning}</Alert>)}
            {/* Clickable Project Section Financial Cards (Works / Payments / Balance before entering) */}
            {allowedSections.some((s) => s.path !== 'advances') && (
              <Box>
                <Typography variant="h4" sx={{ fontWeight: 850, mb: 1.25 }}>
                  {isArabic ? 'أقسام الأعمال والملخص المالي لكل قسم' : 'Work Sections Financial Overview'}
                </Typography>
                <Grid container spacing={1.5}>
                  {allowedSections
                    .filter((s): s is (typeof workSections)[number] & { path: WorkSectionKey } => s.path !== 'advances')
                    .map((sec) => {
                      const summary = extractSectionFinancialSummary(project, sec.path);
                      const meta = SECTION_CARDS_META[sec.path];
                      return (
                        <Grid key={sec.path} size={{ xs: 12, sm: 6, lg: 4 }}>
                          <Box
                            component={Link}
                            to={`/emg/projects/${projectId}/${sec.path}`}
                            sx={{
                              display: 'block',
                              textDecoration: 'none',
                              color: 'inherit',
                              p: 2,
                              borderRadius: 2.5,
                              border: '1px solid',
                              borderColor: 'divider',
                              bgcolor: 'background.paper',
                              transition: 'border-color 150ms ease, transform 150ms ease, box-shadow 150ms ease',
                              '&:hover': {
                                borderColor: meta.color,
                                transform: 'translateY(-2px)',
                                boxShadow: 2
                              }
                            }}
                          >
                            <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                              <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0 }}>
                                <Box
                                  sx={{
                                    width: 34,
                                    height: 34,
                                    borderRadius: 1.75,
                                    display: 'grid',
                                    placeItems: 'center',
                                    bgcolor: `${meta.color}1a`,
                                    color: meta.color,
                                    flexShrink: 0
                                  }}
                                >
                                  {meta.icon}
                                </Box>
                                <Typography variant="subtitle1" sx={{ fontWeight: 800 }} noWrap>
                                  {isArabic ? sec.ar : sec.en}
                                </Typography>
                              </Stack>
                              <IconChevronLeft size={18} style={{ opacity: 0.6 }} />
                            </Stack>
                            <Divider sx={{ my: 1.25 }} />
                            <FinancialSummary
                              layout="compact"
                              totalWorks={summary.totalWorks}
                              totalPayments={summary.totalPayments}
                              cashBalance={summary.cashBalance}
                              cashBalanceStatus={summary.cashBalanceStatus}
                              unresolved={summary.unresolved}
                            />
                          </Box>
                        </Grid>
                      );
                    })}
                </Grid>
              </Box>
            )}

            {account && <MainCard sx={{ borderRadius: 3 }} content={false}>
              <Grid container sx={{ p: 1.5 }} spacing={1.5}>
                {[
                  { label: 'نسبة المكتب', value: `${account.officeSharePercentage}%` },
                  { label: 'أتعاب المكتب', value: verifiedBalance(account.officeShareAmount) },
                  { label: 'المدفوع للمكتب', value: verifiedBalance(account.officePaid) },
                  { label: 'المستحق للمكتب', value: verifiedBalance(account.officeNetDue) }
                ].map((metric) => (
                  <Grid key={metric.label} size={{ xs: 6, md: 3 }}>
                    <Box sx={{ px: 1.25, py: 0.75, borderRadius: 2, bgcolor: 'background.default' }}>
                      <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, display: 'block' }}>
                        {metric.label}
                      </Typography>
                      <Typography variant="subtitle1" sx={{ fontWeight: 850, fontVariantNumeric: 'tabular-nums', mt: 0.25 }}>
                        {metric.value}
                      </Typography>
                    </Box>
                  </Grid>
                ))}
              </Grid>
            </MainCard>}
          </Stack>

      {canViewReports && <Grid container spacing={2} sx={{ mt: 1.5, minWidth: 0, maxWidth: '100%' }}>
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
                        <TableCell>
                          <Typography
                            component={Link}
                            to={`/emg/site-movements/${movement.id}`}
                            variant="body2"
                            color="primary.main"
                            sx={{ fontWeight: 700, textDecoration: 'none', '&:hover': { textDecoration: 'underline' } }}
                          >
                            {movement.description || '—'}
                          </Typography>
                          {movement.reference && (
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                              فاتورة / مرجع: {movement.reference}
                            </Typography>
                          )}
                        </TableCell>
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
      </Grid>}
    </>
  );
}
