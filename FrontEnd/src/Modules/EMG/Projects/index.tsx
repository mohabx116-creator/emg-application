import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';

// material-ui
import { useTheme } from '@mui/material/styles';
import { withAlpha } from 'utils/colorUtils';
import Grid from '@mui/material/Grid';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import InputAdornment from '@mui/material/InputAdornment';
import IconButton from '@mui/material/IconButton';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Pagination from '@mui/material/Pagination';
import Box from '@mui/material/Box';
import Avatar from '@mui/material/Avatar';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Skeleton from '@mui/material/Skeleton';
import Tooltip from '@mui/material/Tooltip';
import MuiBreadcrumbs from '@mui/material/Breadcrumbs';

// icons
import {
  IconBuilding,
  IconPlus,
  IconSearch,
  IconX,
  IconReload,
  IconCircleCheck,
  IconAlertTriangle,
  IconAlertCircle,
  IconClock,
  IconFolders,
  IconFolderOff,
  IconChevronLeft,
  IconChevronRight,
  IconMapPin,
  IconUser,
} from '@tabler/icons-react';
import HomeTwoToneIcon from '@mui/icons-material/HomeTwoTone';

// project imports
import MainCard from 'ui-component/cards/MainCard';
import SubCard from 'ui-component/cards/SubCard';
import { assignEmgUserProject, createEmgProject, EmgProject, EmgUser, getEmgUsers } from 'api/emg';
import { gridSpacing } from 'store/constant';
import useAuth from 'hooks/useAuth';
import useConfig from 'hooks/useConfig';
import axios from 'utils/axios';
import { emgCardSx, emgSurfaceSx } from '../emgCardStyles';
import { hasPermission } from '../roleAccess';
import FinancialSummary from '../FinancialSummary';

// currency formatter
const numberFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const formatMoney = (amount: number) => `${numberFormat.format(amount)} ج.م`;

type StatusFilter = 'all' | 'active';
type SortOption = 'default' | 'issued_desc' | 'received_desc' | 'name_asc';
type CreateResult = { kind: 'unassigned' } | { kind: 'assigned' | 'assignment-failed'; name: string };

const defaultProjectForm = {
  name: '',
  code: '',
  clientName: '',
  ownerName: '',
  payerName: '',
  location: '',
  status: 'نشط',
  officeSharePercentage: '10'
};

export default function EmgProjects() {
  const theme = useTheme();
  const isRtl = theme.direction === 'rtl';
  const {
    state: { borderRadius, i18n }
  } = useConfig();
  const label = (ar: string, en: string) => (i18n === 'ar' ? ar : en);
  const { user } = useAuth();
  const canCreate = hasPermission(user, 'projects.create');
  const canLookupUsers = hasPermission(user, 'users.view');
  const canAssignProject = hasPermission(user, 'users.assign-project');
  const canSelectAssignee = canLookupUsers && canAssignProject;

  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<'denied' | 'request' | null>(null);
  const [reload, setReload] = useState(0);

  // filter & search state
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [sortBy, setSortBy] = useState<SortOption>('default');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(6);
  const [createOpen, setCreateOpen] = useState(false);
  const [createSaving, setCreateSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [createResult, setCreateResult] = useState<CreateResult | null>(null);
  const [assignees, setAssignees] = useState<EmgUser[] | 'loading' | 'failed'>('loading');
  const [assigneeId, setAssigneeId] = useState('');
  const [projectForm, setProjectForm] = useState(defaultProjectForm);

  useEffect(() => {
    if (!createOpen || !canCreate || !canSelectAssignee) return;
    let active = true;
    getEmgUsers()
      .then((users) => {
        if (active)
          setAssignees(users.filter((item) => item.isActive && item.roles.some((role) => ['engineer', 'project_manager'].includes(role))));
      })
      .catch(() => {
        if (active) setAssignees('failed');
      });
    return () => {
      active = false;
    };
  }, [createOpen, canCreate, canSelectAssignee]);

  const openCreateDialog = () => {
    if (!canCreate) return;
    setCreateError(null);
    setCreateResult(null);
    setAssignees('loading');
    setAssigneeId('');
    setCreateOpen(true);
  };

  const closeCreateDialog = () => {
    if (!createSaving) setCreateOpen(false);
  };

  const saveProject = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canCreate || createSaving) return;
    setCreateSaving(true);
    setCreateError(null);
    const selected = canSelectAssignee && Array.isArray(assignees) ? assignees.find((item) => String(item.id) === assigneeId) : undefined;
    try {
      const created = await createEmgProject({
        ...projectForm,
        engineerName: selected?.displayName ?? '',
        officeSharePercentage: Number(projectForm.officeSharePercentage)
      });
      if (canAssignProject && selected) {
        try {
          await assignEmgUserProject(selected.id, created.id);
          setCreateResult({ kind: 'assigned', name: selected.displayName });
        } catch {
          setCreateResult({ kind: 'assignment-failed', name: selected.displayName });
        }
      } else {
        setCreateResult({ kind: 'unassigned' });
      }
      setCreateOpen(false);
      setProjectForm(defaultProjectForm);
      loadProjects();
    } catch (error) {
      const message = error && typeof error === 'object' && 'message' in error && typeof error.message === 'string' ? error.message : null;
      setCreateError(message ?? label('تعذر إنشاء المشروع. راجع البيانات والصلاحيات.', 'Could not create the project. Check the details and your permissions.'));
    } finally {
      setCreateSaving(false);
    }
  };

  const loadProjects = () => setReload((previous) => previous + 1);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);
    axios
      .get<EmgProject[]>('/api/projects', {
        signal: controller.signal,
        // Keep bodyless permission failures distinguishable from network errors.
        transformResponse: [
          ...[axios.defaults.transformResponse ?? []].flat(),
          (body: unknown, _headers: unknown, status?: number) => (!body && status === 403 ? { status } : body)
        ]
      })
      .then(({ data }) => {
        if (controller.signal.aborted) return;
        if (!Array.isArray(data)) throw new Error('Invalid project list');
        setProjects(data);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        setProjects([]);
        setLoadError(error?.status === 403 ? 'denied' : 'request');
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [reload]);

  // Derived KPI metrics
  const kpis = useMemo(() => {
    const total = projects.length;
    const active = projects.filter((p) => p.status === 'نشط').length;
    const totalIssued = projects.reduce((sum, p) => sum + (p.totalIssued || 0), 0);
    const totalReceived = projects.reduce((sum, p) => sum + (p.totalReceived || 0), 0);
    const totalWorks = projects.reduce((sum, p) => sum + (p.totalWorks || 0), 0);
    const totalPayments = projects.reduce((sum, p) => sum + (p.totalPayments || 0), 0);

    return {
      total,
      active,
      totalIssued,
      totalReceived,
      totalWorks,
      totalPayments,
    };
  }, [projects]);

  // Filtered & sorted projects
  const filteredProjects = useMemo(() => {
    let result = [...projects];

    // Search filter
    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (p) =>
          p.name?.toLowerCase().includes(q) ||
          p.code?.toLowerCase().includes(q) ||
          p.location?.toLowerCase().includes(q) ||
          p.engineerName?.toLowerCase().includes(q) ||
          p.clientName?.toLowerCase().includes(q)
      );
    }

    // Status filter
    if (statusFilter === 'active') {
      result = result.filter((p) => p.status === 'نشط');
    }

    // Sorting
    if (sortBy === 'issued_desc') {
      result.sort((a, b) => (b.totalIssued || 0) - (a.totalIssued || 0));
    } else if (sortBy === 'received_desc') {
      result.sort((a, b) => (b.totalReceived || 0) - (a.totalReceived || 0));
    } else if (sortBy === 'name_asc') {
      result.sort((a, b) => (a.name || '').localeCompare(b.name || '', 'ar'));
    }

    return result;
  }, [projects, searchQuery, statusFilter, sortBy]);

  useEffect(() => {
    setPage(1);
  }, [searchQuery, statusFilter, sortBy, pageSize]);

  const pageCount = Math.max(1, Math.ceil(filteredProjects.length / pageSize));
  const paginatedProjects = filteredProjects.slice((page - 1) * pageSize, page * pageSize);

  const hasActiveFilters = searchQuery.trim() !== '' || statusFilter !== 'all' || sortBy !== 'default';
  const hasNoMatchingProjects = projects.length > 0 && filteredProjects.length === 0;

  const resetFilters = () => {
    setSearchQuery('');
    setStatusFilter('all');
    setSortBy('default');
    setPage(1);
  };

  return (
    <Box sx={{ width: '100%', pb: 4 }}>
      {/* 1. Page Breadcrumbs & Title Hierarchy */}
      <Box sx={{ mb: 3 }}>
        <MuiBreadcrumbs
          aria-label={label('مسار التنقل', 'Breadcrumbs')}
          separator={isRtl ? <IconChevronLeft size={14} /> : <IconChevronRight size={14} />}
          sx={{ mb: 1.5, '& .MuiBreadcrumbs-separator': { mx: 1 } }}
        >
          <Typography
            component={Link}
            to="/emg/dashboard"
            variant="subtitle2"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 0.75,
              textDecoration: 'none',
              color: 'text.secondary',
              '&:hover': { color: 'primary.main' }
            }}
          >
            <HomeTwoToneIcon sx={{ fontSize: '1.1rem' }} />
            {label('لوحة التحكم', 'Dashboard')}
          </Typography>
          <Typography variant="subtitle2" sx={{ color: 'text.secondary' }}>
            EMG
          </Typography>
          <Typography variant="subtitle2" sx={{ color: 'primary.main', fontWeight: 600 }}>
            {label('المشاريع', 'Projects')}
          </Typography>
        </MuiBreadcrumbs>

        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          justifyContent="space-between"
          alignItems={{ xs: 'flex-start', sm: 'center' }}
          spacing={2}
        >
          <Box>
            <Typography variant="h2" component="h1" sx={{ fontWeight: 700, mb: 0.5, color: 'text.primary' }}>
              {label('المشاريع', 'Projects')}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {label('إدارة المشاريع ومتابعة الأداء المالي والتشغيلي', 'Manage projects and track financial and site activity')}
            </Typography>
          </Box>
          {canCreate && (
            <Button
              variant="contained"
              color="primary"
              startIcon={isRtl ? <IconBuilding size={19} /> : <IconPlus size={19} />}
              onClick={openCreateDialog}
              sx={{
                px: 2.75,
                py: 1.1,
                borderRadius: `${borderRadius}px`,
                fontWeight: 600,
                fontSize: '0.925rem',
                boxShadow: theme.shadows[2],
                whiteSpace: 'nowrap',
                '&:hover': {
                  boxShadow: theme.shadows[4]
                }
              }}
            >
              {label('إضافة مشروع', 'Create project')}
            </Button>
          )}
        </Stack>
      </Box>

      {createResult && (
        <Alert severity={createResult.kind === 'assignment-failed' ? 'warning' : 'success'} sx={{ mb: 3 }}>
          {createResult.kind === 'assignment-failed'
            ? label(
                `تم إنشاء المشروع، لكن تعذر إسناد صلاحية الوصول إلى ${createResult.name}.`,
                `Project created, but access could not be assigned to ${createResult.name}.`
              )
            : createResult.kind === 'assigned'
              ? label(`تم إنشاء المشروع وإسناده إلى ${createResult.name}.`, `Project created and assigned to ${createResult.name}.`)
              : label('تم إنشاء المشروع دون إسناده إلى أي مستخدم.', 'Project created without a user assignment.')}
        </Alert>
      )}

      {/* 2. Compact Project Summary / KPI Strip */}
      <Grid container spacing={gridSpacing} sx={{ mb: 3, display: loadError ? 'none' : undefined }}>
        {/* Total Projects */}
        <Grid size={{ xs: 12, sm: 6, lg: 6 }}>
          <SubCard
            sx={{
              height: '100%',
              bgcolor: 'background.paper',
              ...emgSurfaceSx(theme),
              transition: 'transform 0.2s ease, box-shadow 0.2s ease',
              '&:hover': { transform: 'translateY(-2px)' }
            }}
            contentSX={{ p: 2.25 }}
          >
            {loading ? (
              <Stack spacing={1}>
                <Skeleton variant="circular" width={40} height={40} />
                <Skeleton variant="text" width="60%" height={32} />
                <Skeleton variant="text" width="40%" height={20} />
              </Stack>
            ) : (
              <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2}>
                <Box>
                  <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600, textTransform: 'uppercase' }}>
                    إجمالي المشاريع
                  </Typography>
                  <Typography variant="h2" sx={{ fontWeight: 700, mt: 0.5, mb: 0.25, color: 'text.primary' }}>
                    {kpis.total}
                  </Typography>
                  <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                    جميع المشاريع المسجلة بالنظام
                  </Typography>
                </Box>
                <Avatar
                  variant="rounded"
                  sx={{
                    width: 48,
                    height: 48,
                    bgcolor: withAlpha(theme.vars.palette.primary.main, 0.1),
                    color: theme.vars.palette.primary.main
                  }}
                >
                  <IconFolders size={26} stroke={1.75} />
                </Avatar>
              </Stack>
            )}
          </SubCard>
        </Grid>

        {/* Active Projects */}
        <Grid size={{ xs: 12, sm: 6, lg: 6 }}>
          <SubCard
            sx={{
              height: '100%',
              bgcolor: 'background.paper',
              transition: 'transform 0.2s ease, box-shadow 0.2s ease',
              '&:hover': { transform: 'translateY(-2px)' }
            }}
            contentSX={{ p: 2.25 }}
          >
            {loading ? (
              <Stack spacing={1}>
                <Skeleton variant="circular" width={40} height={40} />
                <Skeleton variant="text" width="60%" height={32} />
                <Skeleton variant="text" width="40%" height={20} />
              </Stack>
            ) : (
              <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2}>
                <Box>
                  <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600, textTransform: 'uppercase' }}>
                    مشاريع نشطة
                  </Typography>
                  <Typography variant="h2" sx={{ fontWeight: 700, mt: 0.5, mb: 0.25, color: 'success.main' }}>
                    {kpis.active}
                  </Typography>
                  <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                    {kpis.total > 0 ? `${Math.round((kpis.active / kpis.total) * 100)}% قيد التنفيذ المباشر` : 'لا توجد مشاريع'}
                  </Typography>
                </Box>
                <Avatar
                  variant="rounded"
                  sx={{
                    width: 48,
                    height: 48,
                    bgcolor: withAlpha(theme.vars.palette.success.main, 0.1),
                    color: theme.vars.palette.success.main
                  }}
                >
                  <IconCircleCheck size={26} stroke={1.75} />
                </Avatar>
              </Stack>
            )}
          </SubCard>
        </Grid>

      </Grid>

      {/* 3. Search and Filter Affordance */}
      <MainCard
        border={true}
        sx={{
          display: loadError ? 'none' : undefined,
          mb: 3,
          boxShadow: 'none',
          borderColor: 'divider',
          bgcolor: 'background.paper'
        }}
        contentSX={{ p: 2 }}
      >
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'stretch', md: 'center' }} justifyContent="space-between">
          {/* Search Box */}
          <TextField
            fullWidth
            size="small"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="بحث باسم المشروع، الكود، الموقع، أو المهندس المسؤول..."
            aria-label="البحث في قائمة المشاريع"
            sx={{ maxWidth: { md: 380 } }}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <IconSearch size={18} color={theme.vars.palette.text.secondary} />
                  </InputAdornment>
                ),
                endAdornment: searchQuery ? (
                  <InputAdornment position="end">
                    <IconButton size="small" onClick={() => setSearchQuery('')} aria-label="مسح البحث">
                      <IconX size={16} />
                    </IconButton>
                  </InputAdornment>
                ) : null
              }
            }}
          />

          {/* Status Filter Chips & Sort Controls */}
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1.5}
            alignItems={{ xs: 'stretch', sm: 'center' }}
            flexWrap="wrap"
            useFlexGap
          >
            {/* Status Filter Tabs */}
            <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
              <Chip
                label={`الكل (${kpis.total})`}
                color={statusFilter === 'all' ? 'primary' : 'default'}
                variant={statusFilter === 'all' ? 'filled' : 'outlined'}
                onClick={() => setStatusFilter('all')}
                size="medium"
                clickable
                sx={{ fontWeight: 600 }}
              />
              <Chip
                label={`نشط (${kpis.active})`}
                color={statusFilter === 'active' ? 'success' : 'default'}
                variant={statusFilter === 'active' ? 'filled' : 'outlined'}
                onClick={() => setStatusFilter('active')}
                size="medium"
                clickable
                sx={{ fontWeight: 600 }}
              />
            </Stack>

            <Divider orientation="vertical" flexItem sx={{ display: { xs: 'none', sm: 'block' } }} />

            {/* Sort Selector */}
            <FormControl size="small" sx={{ minWidth: 170 }}>
              <InputLabel id="sort-projects-label">الترتيب حسب</InputLabel>
              <Select
                labelId="sort-projects-label"
                id="sort-projects"
                value={sortBy}
                label="الترتيب حسب"
                onChange={(e) => setSortBy(e.target.value as SortOption)}
              >
                <MenuItem value="default">الترتيب الافتراضي</MenuItem>
                <MenuItem value="issued_desc">الأعلى صادر</MenuItem>
                <MenuItem value="received_desc">الأعلى وارد</MenuItem>
                <MenuItem value="name_asc">اسم المشروع (أ-ي)</MenuItem>
              </Select>
            </FormControl>

            {/* Reset Filters Button */}
            {hasActiveFilters && (
              <Button
                variant="outlined"
                color="secondary"
                size="small"
                onClick={resetFilters}
                startIcon={<IconReload size={16} />}
                aria-label="إعادة تعيين خيارات التصفية والبحث"
                sx={{ whiteSpace: 'nowrap' }}
              >
                إعادة تعيين
              </Button>
            )}
          </Stack>
        </Stack>

        {/* Results Counter Bar */}
        {!loading && (
          <Box sx={{ mt: 1.5, pt: 1.5, borderTop: '1px dashed', borderColor: 'divider', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 500 }}>
              عرض {filteredProjects.length} من أصل {projects.length} مشروع
              {hasActiveFilters && ' (نتائج مصفاة)'}
            </Typography>
            <Stack direction="row" spacing={2} sx={{ display: { xs: 'none', md: 'flex' } }}>
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  إجمالي الأعمال المتاح:{' '}
                <Box component="span" sx={{ fontWeight: 600, color: 'text.primary' }}>
                    {formatMoney(kpis.totalWorks)}
                </Box>
              </Typography>
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  إجمالي الدفعات المتاح:{' '}
                <Box component="span" sx={{ fontWeight: 600, color: 'text.primary' }}>
                    {formatMoney(kpis.totalPayments)}
                </Box>
              </Typography>
            </Stack>
          </Box>
        )}
      </MainCard>

      {/* 4. Error State */}
      {loadError && (
        <Alert
          severity={loadError === 'denied' ? 'warning' : 'error'}
          sx={{ mb: 3 }}
          action={
            loadError === 'request' && (
              <Button color="inherit" size="small" onClick={loadProjects} startIcon={<IconReload size={16} />}>
                {label('إعادة المحاولة', 'Try again')}
              </Button>
            )
          }
        >
          <AlertTitle sx={{ fontWeight: 700 }}>
            {loadError === 'denied'
              ? label('لا تملك صلاحية عرض المشاريع.', 'You do not have permission to view projects.')
              : label('تعذر تحميل بيانات المشاريع.', 'Could not load projects.')}
          </AlertTitle>
        </Alert>
      )}

      {/* 5. Loading Skeletons State */}
      {loading ? (
        <Grid container spacing={gridSpacing} role="status" aria-label={label('جاري تحميل المشاريع', 'Loading projects')}>
          {[1, 2, 3, 4, 5, 6].map((key) => (
            <Grid key={key} size={{ xs: 12, sm: 6, lg: 4 }}>
              <Card sx={{ ...emgSurfaceSx(theme), borderRadius: `${borderRadius}px`, p: 2.5, height: '100%' }}>
                <Stack spacing={2}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Skeleton variant="rounded" width={80} height={24} />
                    <Skeleton variant="rounded" width={60} height={24} />
                  </Stack>
                  <Skeleton variant="text" width="85%" height={32} />
                  <Skeleton variant="text" width="50%" height={20} />
                  <Skeleton variant="rounded" width="100%" height={80} />
                  <Stack direction="row" justifyContent="space-between" alignItems="center" pt={1}>
                    <Skeleton variant="text" width="40%" height={24} />
                    <Skeleton variant="rounded" width={100} height={36} />
                  </Stack>
                </Stack>
              </Card>
            </Grid>
          ))}
        </Grid>
      ) : loadError ? null : filteredProjects.length === 0 ? (
        /* 6. Empty State */
        <MainCard border={true} sx={{ py: 6, textAlign: 'center', bgcolor: 'background.paper' }}>
          <Stack spacing={2} alignItems="center" justifyContent="center">
            <Avatar
              variant="rounded"
              sx={{
                width: 72,
                height: 72,
                bgcolor: withAlpha(theme.vars.palette.primary.main, 0.08),
                color: theme.vars.palette.primary.main,
                mb: 1
              }}
            >
              {hasNoMatchingProjects ? <IconFolderOff size={36} /> : <IconBuilding size={36} />}
            </Avatar>
            <Typography variant="h3" sx={{ fontWeight: 700 }}>
              {hasNoMatchingProjects
                ? label('لم يتم العثور على أي مشاريع مطابقة', 'No projects match your filters')
                : label('لا توجد مشاريع متاحة لحسابك حالياً', 'No projects are available to your account yet')}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary', maxWidth: 440 }}>
              {hasNoMatchingProjects
                ? label('جرب تغيير كلمات البحث أو إعادة تعيين الفلاتر.', 'Change your search or reset the filters.')
                : label('اطلب إسناد مشروع إلى حسابك من مسؤول النظام.', 'Ask an administrator to assign a project to your account.')}
            </Typography>
            {hasNoMatchingProjects ? (
              <Button
                variant="outlined"
                color="primary"
                onClick={resetFilters}
                startIcon={<IconReload size={18} />}
                sx={{ mt: 1, borderRadius: `${borderRadius}px` }}
              >
                {label('إعادة تعيين خيارات البحث', 'Reset filters')}
              </Button>
            ) : canCreate ? (
              <Button
                variant="contained"
                color="primary"
                startIcon={<IconPlus size={18} />}
                sx={{ mt: 1, borderRadius: `${borderRadius}px` }}
                onClick={openCreateDialog}
              >
                {label('إضافة مشروع', 'Create project')}
              </Button>
            ) : null}
          </Stack>
        </MainCard>
      ) : (
        <>
        {/* 7. Compact Approved Project Cards Grid (matches owner-approved reference) */}
        <Grid container spacing={2}>
          {paginatedProjects.map((project) => {
            const isProjectActive = project.status === 'نشط';

            return (
              <Grid key={project.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                <Card
                  component={Link}
                  to={`/emg/projects/${project.id}`}
                  variant="outlined"
                  role="link"
                  tabIndex={0}
                  sx={{
                    ...emgCardSx(theme),
                    height: '100%',
                    display: 'flex',
                    flexDirection: 'column',
                    borderRadius: 3,
                    borderColor: 'divider',
                    color: 'inherit',
                    textDecoration: 'none',
                    bgcolor: 'background.paper',
                    transition: 'border-color 150ms ease, transform 150ms ease, box-shadow 150ms ease',
                    '&:hover': {
                      borderColor: withAlpha(theme.vars.palette.primary.main, 0.55),
                      transform: 'translateY(-2px)'
                    },
                    '&:focus-within': {
                      outline: `2px solid ${theme.vars.palette.primary.main}`,
                      outlineOffset: '2px'
                    }
                  }}
                >
                  <CardContent
                    sx={{
                      p: { xs: 2, sm: 2.25 },
                      flexGrow: 1,
                      display: 'flex',
                      flexDirection: 'column',
                      '&:last-child': { pb: { xs: 2, sm: 2.25 } }
                    }}
                  >
                    {/* Header Row: Building Icon + Project Name + Status/Code + Chevron */}
                    <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.25}>
                      <Stack direction="row" spacing={1.25} alignItems="center" sx={{ minWidth: 0, flex: 1 }}>
                        <Box
                          sx={{
                            width: 38,
                            height: 38,
                            borderRadius: 2,
                            display: 'grid',
                            placeItems: 'center',
                            bgcolor: withAlpha(theme.vars.palette.primary.main, 0.12),
                            color: theme.vars.palette.primary.main,
                            flexShrink: 0
                          }}
                        >
                          <IconBuilding size={20} stroke={1.9} />
                        </Box>
                        <Box sx={{ minWidth: 0, flex: 1 }}>
                          <Tooltip title={project.name} enterDelay={500} placement="top">
                            <Typography
                              variant="h4"
                              component="h2"
                              sx={{
                                fontWeight: 800,
                                fontSize: '1.02rem',
                                lineHeight: 1.35,
                                color: 'text.primary'
                              }}
                              noWrap
                            >
                              {project.name}
                            </Typography>
                          </Tooltip>
                          <Stack direction="row" spacing={0.75} alignItems="center" sx={{ mt: 0.2 }}>
                            <Typography
                              variant="caption"
                              sx={{
                                fontWeight: 700,
                                color: isProjectActive ? 'success.main' : 'text.secondary'
                              }}
                            >
                              {isProjectActive ? 'مشروع نشط' : project.status}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace', fontWeight: 600 }} noWrap>
                              · {project.code}
                            </Typography>
                          </Stack>
                        </Box>
                      </Stack>
                      <Box sx={{ color: 'text.secondary', display: 'flex', flexShrink: 0 }}>
                        {isRtl ? <IconChevronLeft size={18} /> : <IconChevronRight size={18} />}
                      </Box>
                    </Stack>

                    <Divider sx={{ my: 1.5 }} />

                    {/* Compact 3-Column Financial Strip (Works / Payments / Cash Balance) */}
                    <FinancialSummary
                      layout="compact"
                      totalWorks={project.totalWorks}
                      totalPayments={project.historicalFundingUnknown && !project.totalPayments ? null : project.totalPayments}
                      cashBalance={project.historicalFundingUnknown ? null : project.cashBalance}
                      cashBalanceStatus={project.cashBalanceStatus}
                    />
                  </CardContent>
                </Card>
              </Grid>
            );
          })}
        </Grid>
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems="center" spacing={2} sx={{ mt: 3 }}>
          <Typography variant="body2" color="text.secondary">
            عرض {Math.min((page - 1) * pageSize + 1, filteredProjects.length)} - {Math.min(page * pageSize, filteredProjects.length)} من {filteredProjects.length} مشروع
          </Typography>
          <Stack direction="row" spacing={2} alignItems="center">
            <FormControl size="small" sx={{ minWidth: 125 }}>
              <InputLabel>عدد الكروت</InputLabel>
              <Select value={pageSize} label="عدد الكروت" onChange={(event) => setPageSize(Number(event.target.value))}>
                <MenuItem value={6}>6 كروت</MenuItem>
                <MenuItem value={12}>12 كارت</MenuItem>
                <MenuItem value={24}>24 كارت</MenuItem>
              </Select>
            </FormControl>
            <Pagination count={pageCount} page={Math.min(page, pageCount)} onChange={(_, value) => setPage(value)} color="primary" shape="rounded" showFirstButton showLastButton />
          </Stack>
        </Stack>
        </>
      )}

      <Dialog open={canCreate && createOpen} onClose={closeCreateDialog} fullWidth maxWidth="md" scroll="paper" aria-labelledby="create-project-title">
        <Box component="form" onSubmit={saveProject} dir={isRtl ? 'rtl' : 'ltr'}>
          <DialogTitle id="create-project-title" sx={{ fontWeight: 700 }}>{label('إضافة مشروع جديد', 'Create a project')}</DialogTitle>
          <DialogContent dividers>
            {createError && <Alert severity="error" sx={{ mb: 2 }}>{createError}</Alert>}
            <Grid container spacing={2} sx={{ pt: 1 }}>
              <Grid size={{ xs: 12, md: 8 }}><TextField required fullWidth label={label('اسم المشروع', 'Project name')} value={projectForm.name} onChange={(event) => setProjectForm({ ...projectForm, name: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 4 }}><TextField required fullWidth label={label('كود المشروع', 'Project code')} value={projectForm.code} onChange={(event) => setProjectForm({ ...projectForm, code: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth label="اسم العميل" value={projectForm.clientName} onChange={(event) => setProjectForm({ ...projectForm, clientName: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth label="اسم المالك" value={projectForm.ownerName} onChange={(event) => setProjectForm({ ...projectForm, ownerName: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth label="اسم الدافع" value={projectForm.payerName} onChange={(event) => setProjectForm({ ...projectForm, payerName: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth label="الموقع" value={projectForm.location} onChange={(event) => setProjectForm({ ...projectForm, location: event.target.value })} /></Grid>
              <Grid size={{ xs: 12, md: 4 }}><FormControl fullWidth><InputLabel>حالة المشروع</InputLabel><Select label="حالة المشروع" value={projectForm.status} onChange={(event) => setProjectForm({ ...projectForm, status: event.target.value })}><MenuItem value="نشط">نشط</MenuItem><MenuItem value="متوقف">متوقف</MenuItem><MenuItem value="مكتمل">مكتمل</MenuItem></Select></FormControl></Grid>
              {canSelectAssignee && (
                <Grid size={{ xs: 12, md: 4 }}>
                  {assignees === 'loading' ? (
                    <Typography role="status">{label('جاري تحميل المستخدمين…', 'Loading users…')}</Typography>
                  ) : assignees === 'failed' ? (
                    <Alert severity="warning">{label('تعذر تحميل المستخدمين للإسناد.', 'Could not load users for assignment.')}</Alert>
                  ) : assignees.length === 0 ? (
                    <Typography>{label('لا يوجد مستخدمون متاحون للإسناد.', 'No users are available for assignment.')}</Typography>
                  ) : (
                    <FormControl fullWidth disabled={createSaving}>
                      <InputLabel id="project-assignee-label">{label('إسناد المشروع', 'Assign project')}</InputLabel>
                      <Select
                        labelId="project-assignee-label"
                        label={label('إسناد المشروع', 'Assign project')}
                        value={assigneeId}
                        onChange={(event) => setAssigneeId(event.target.value)}
                      >
                        <MenuItem value="">{label('بدون إسناد الآن', 'No assignment')}</MenuItem>
                        {assignees.map((item) => (
                          <MenuItem key={item.id} value={String(item.id)}>{item.displayName} ({item.userName})</MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  )}
                </Grid>
              )}
              <Grid size={{ xs: 12, md: 4 }}><TextField fullWidth type="number" label="نصيب المكتب %" value={projectForm.officeSharePercentage} onChange={(event) => setProjectForm({ ...projectForm, officeSharePercentage: event.target.value })} slotProps={{ htmlInput: { min: 0, max: 100, step: 0.01 } }} /></Grid>
            </Grid>
          </DialogContent>
          <DialogActions sx={{ px: 3, py: 2 }}>
            <Button onClick={closeCreateDialog} disabled={createSaving}>{label('إلغاء', 'Cancel')}</Button>
            <Button type="submit" variant="contained" disabled={createSaving}>
              {createSaving ? label('جاري إنشاء المشروع...', 'Creating project…') : label('إنشاء المشروع', 'Create project')}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
    </Box>
  );
}
