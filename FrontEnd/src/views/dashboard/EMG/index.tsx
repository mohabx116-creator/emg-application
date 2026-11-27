import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Divider,
  Grid,
  Skeleton,
  Stack,
  Typography,
  useTheme
} from '@mui/material';
import { Link } from 'react-router-dom';
import {
  IconArrowUpLeft,
  IconBell,
  IconBuilding,
  IconCash,
  IconChecklist,
  IconClipboardCheck,
  IconClock,
  IconFileAnalytics,
  IconFileUpload,
  IconReceipt,
  IconTable,
  IconTruckDelivery,
  IconUser,
  IconUserShield,
  IconUsers
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { useEmgProjectsCache, useEmgSuppliersCache, useTodayAttendanceCache } from 'hooks/useEmgData';
import EngineerMobileDashboard from './EngineerMobileDashboard';
import { withAlpha } from 'utils/colorUtils';
import {
  hasAnyPermission,
  isSiteEngineer,
  resolvePrimaryRole,
  roleSubtitle,
  roleTitle
} from 'Modules/EMG/roleAccess';
import { EmgNotification, getEmgNotifications } from 'api/emg';
import FinancialSummary from 'Modules/EMG/FinancialSummary';

const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const fmt = (value: number) => `${money.format(value || 0)} ج.م`;

type AccentTone = 'primary' | 'secondary' | 'success' | 'warning' | 'error';

type QuickTile = {
  label: string;
  to: string;
  icon: React.ReactNode;
  tone: AccentTone;
  badge?: number;
};

function BerryGradientHeroCard({
  title,
  value,
  subtitle,
  icon,
  variant = 'blue'
}: {
  title: string;
  value: string;
  subtitle?: string;
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
        minHeight: { xs: 144, sm: 172 },
        position: 'relative',
        overflow: 'hidden',
        color: '#ffffff',
        background: gradient,
        borderRadius: 3.5,
        boxShadow: '0 10px 28px rgba(13, 43, 82, 0.18)',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'space-between',
        '&::before': {
          content: '""',
          position: 'absolute',
          width: 190,
          height: 190,
          borderRadius: '50%',
          background: 'rgba(255, 255, 255, 0.10)',
          top: -75,
          left: -55,
          pointerEvents: 'none'
        },
        '&::after': {
          content: '""',
          position: 'absolute',
          width: 130,
          height: 130,
          borderRadius: '50%',
          background: 'rgba(255, 255, 255, 0.08)',
          top: 20,
          left: 35,
          pointerEvents: 'none'
        }
      }}
    >
      <CardContent
        sx={{
          position: 'relative',
          zIndex: 1,
          height: '100%',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'space-between',
          p: { xs: 2.25, sm: 2.75 },
          '&:last-child': { pb: { xs: 2.25, sm: 2.75 } }
        }}
      >
        <Box
          sx={{
            width: 44,
            height: 44,
            borderRadius: 2.5,
            display: 'grid',
            placeItems: 'center',
            bgcolor: 'rgba(255, 255, 255, 0.92)',
            color: variant === 'purple' ? '#4527a0' : '#1565c0',
            boxShadow: '0 4px 12px rgba(0,0,0,0.12)'
          }}
        >
          {icon}
        </Box>
        <Box sx={{ mt: 2 }}>
          <Typography
            variant="h2"
            sx={{
              color: '#ffffff',
              fontWeight: 800,
              fontSize: { xs: '1.45rem', sm: '1.75rem' },
              fontVariantNumeric: 'tabular-nums',
              lineHeight: 1.2
            }}
          >
            {value}
          </Typography>
          <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.88)', fontWeight: 600, mt: 0.5 }}>
            {title}
          </Typography>
          {subtitle && (
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.76)', display: 'block', mt: 0.25 }}>
              {subtitle}
            </Typography>
          )}
        </Box>
      </CardContent>
    </Card>
  );
}

function StackedMetricCard({
  title,
  value,
  icon,
  tone = 'warning'
}: {
  title: string;
  value: string;
  icon: React.ReactNode;
  tone?: AccentTone;
}) {
  const theme = useTheme();
  const solidBg: Record<AccentTone, string> = {
    primary: theme.vars.palette.primary.main,
    secondary: theme.vars.palette.secondary.main,
    success: theme.vars.palette.success.dark,
    warning: theme.vars.palette.warning.main,
    error: theme.vars.palette.error.dark
  };

  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        minHeight: 78,
        borderRadius: 3,
        borderColor: 'divider',
        display: 'flex',
        alignItems: 'center',
        transition: 'box-shadow 150ms ease, transform 150ms ease',
        '&:hover': {
          transform: 'translateY(-1px)',
          boxShadow: theme.shadows[2]
        }
      }}
    >
      <CardContent sx={{ width: '100%', py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Box
            sx={{
              width: 42,
              height: 42,
              borderRadius: 2.5,
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
              bgcolor: solidBg[tone],
              color: '#ffffff'
            }}
          >
            {icon}
          </Box>
          <Box sx={{ minWidth: 0 }}>
            <Typography
              variant="h4"
              sx={{
                fontWeight: 800,
                fontSize: { xs: '1.05rem', sm: '1.2rem' },
                fontVariantNumeric: 'tabular-nums',
                lineHeight: 1.2
              }}
            >
              {value}
            </Typography>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mt: 0.25 }}>
              {title}
            </Typography>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

function ShortcutTile({ tile }: { tile: QuickTile }) {
  const theme = useTheme();
  const paletteMap: Record<AccentTone, { fg: string; bg: string }> = {
    primary: { fg: theme.vars.palette.primary.main, bg: withAlpha(theme.vars.palette.primary.main, 0.12) },
    secondary: { fg: theme.vars.palette.secondary.main, bg: withAlpha(theme.vars.palette.secondary.main, 0.12) },
    success: { fg: theme.vars.palette.success.dark, bg: withAlpha(theme.vars.palette.success.main, 0.14) },
    warning: { fg: theme.vars.palette.warning.dark, bg: withAlpha(theme.vars.palette.warning.main, 0.14) },
    error: { fg: theme.vars.palette.error.dark, bg: withAlpha(theme.vars.palette.error.main, 0.12) }
  };
  const colors = paletteMap[tile.tone];

  return (
    <Card
      variant="outlined"
      sx={{
        height: '100%',
        borderRadius: 3,
        borderColor: 'divider',
        position: 'relative',
        transition: 'border-color 150ms ease, transform 150ms ease, box-shadow 150ms ease',
        '&:hover': {
          borderColor: colors.fg,
          transform: 'translateY(-2px)',
          boxShadow: theme.shadows[2]
        }
      }}
    >
      <CardActionArea component={Link} to={tile.to} sx={{ height: '100%', p: { xs: 1.5, sm: 1.75 } }}>
        {tile.badge !== undefined && tile.badge > 0 && (
          <Chip
            size="small"
            color="error"
            label={tile.badge}
            sx={{
              position: 'absolute',
              top: 8,
              left: 8,
              height: 20,
              minWidth: 20,
              fontSize: '0.7rem',
              fontWeight: 700,
              '& .MuiChip-label': { px: 0.75 }
            }}
          />
        )}
        <Stack spacing={1} alignItems="center" textAlign="center">
          <Box
            sx={{
              width: 44,
              height: 44,
              borderRadius: 2.5,
              display: 'grid',
              placeItems: 'center',
              bgcolor: colors.bg,
              color: colors.fg
            }}
          >
            {tile.icon}
          </Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, color: 'text.primary', lineHeight: 1.3 }}>
            {tile.label}
          </Typography>
        </Stack>
      </CardActionArea>
    </Card>
  );
}

function DashboardSkeleton() {
  return (
    <Box sx={{ pb: 4 }} aria-label="جاري تحميل لوحة التحكم">
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={1.5} sx={{ mb: 2.5 }}>
        <Box>
          <Skeleton variant="text" width={220} height={36} />
          <Skeleton variant="text" width={300} height={22} />
        </Box>
        <Skeleton variant="rounded" width={140} height={40} />
      </Stack>
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid size={{ xs: 12, lg: 6 }}>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <Skeleton variant="rounded" height={172} sx={{ borderRadius: 3.5 }} />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <Skeleton variant="rounded" height={172} sx={{ borderRadius: 3.5 }} />
            </Grid>
          </Grid>
        </Grid>
        <Grid size={{ xs: 12, lg: 6 }}>
          <Grid container spacing={2}>
            {[0, 1, 2, 3].map((i) => (
              <Grid key={i} size={{ xs: 6 }}>
                <Skeleton variant="rounded" height={78} sx={{ borderRadius: 3 }} />
              </Grid>
            ))}
          </Grid>
        </Grid>
      </Grid>
      <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <Grid key={i} size={{ xs: 6, sm: 4, md: 2 }}>
            <Skeleton variant="rounded" height={96} sx={{ borderRadius: 3 }} />
          </Grid>
        ))}
      </Grid>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 7 }}>
          <Skeleton variant="rounded" height={260} sx={{ borderRadius: 3 }} />
        </Grid>
        <Grid size={{ xs: 12, lg: 5 }}>
          <Skeleton variant="rounded" height={260} sx={{ borderRadius: 3 }} />
        </Grid>
      </Grid>
    </Box>
  );
}

export default function EmgDashboard() {
  const theme = useTheme();
  const { user } = useAuth();
  const primaryRole = resolvePrimaryRole(user);
  const engineerView = isSiteEngineer(user);
  const accountantView = primaryRole === 'accountant';
  const pmView = primaryRole === 'project_manager';

  // Effective permission checks
  const canReadProjects = hasAnyPermission(user, ['projects.view']);
  const canReadSuppliers = hasAnyPermission(user, ['suppliers.manage', 'suppliers.view']);
  const canReadAdvances = hasAnyPermission(user, ['advances.manage', 'advances.view']);
  const canReadReports = hasAnyPermission(user, ['reports.view']);
  const canReadReviewCenter = hasAnyPermission(user, [
    'project-imports.approve',
    'project-imports.reject',
    'workflow.execute',
    'suppliers.supplies-accept-technical',
    'suppliers.supplies-approve-financial'
  ]);
  const canReadUsers = hasAnyPermission(user, ['users.view', 'users.manage']);
  const canReadRoles = hasAnyPermission(user, ['roles.view', 'roles.manage']);
  const canMigrateData = hasAnyPermission(user, ['data.migrate']);
  const canImportApprovedHistory = hasAnyPermission(user, ['project-imports.import-approved-history']);
  const canReadAttendance = hasAnyPermission(user, ['attendance.manage', 'attendance.view']);
  const canReadNotifications = hasAnyPermission(user, ['notifications.view']);

  const canSiteMovements = hasAnyPermission(user, ['site-movements.view', 'site-movements.create', 'site-movements.manage']);
  const canExcavation = hasAnyPermission(user, ['excavation.view', 'excavation.create']);
  const canConcrete = hasAnyPermission(user, ['concrete.view', 'concrete.create']);
  const canSteel = hasAnyPermission(user, ['steel.view', 'steel.create']);
  const canFinishing = hasAnyPermission(user, ['finishing.view', 'finishing.create']);

  const { data: projects = [], isLoading: projectsLoading, error: projectsError } = useEmgProjectsCache(canReadProjects);
  const { data: suppliers = [], isLoading: suppliersLoading, error: suppliersError } = useEmgSuppliersCache(canReadSuppliers && !engineerView);
  const { data: attendance = null } = useTodayAttendanceCache(canReadAttendance);

  const [notifications, setNotifications] = useState<EmgNotification[]>([]);

  useEffect(() => {
    if (!canReadNotifications || engineerView) return;
    let active = true;
    getEmgNotifications(true)
      .then((rows) => {
        if (active) setNotifications(rows);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [canReadNotifications, engineerView]);

  const loading = projectsLoading || (!engineerView && suppliersLoading);
  const error =
    projectsError || suppliersError
      ? 'تعذر تحميل بعض المؤشرات المالية. راجع صلاحيات الحساب أو افتح المشاريع مباشرة.'
      : null;

  const totals = useMemo(
    () => ({
      due: projects.reduce((sum,p) => sum + Math.max(0, -(p.cashBalance ?? 0)), 0),
      supplierRemaining: suppliers.reduce((x, s) => x + s.remaining, 0),
      openMovements: projects.reduce((x, p) => x + p.openMovements, 0),
        activeProjects: projects.filter((p) => p.status === 'نشط').length,
        totalWorks: projects.reduce((x, p) => x + (p.totalWorks || 0), 0),
        totalPayments: projects.reduce((x, p) => x + (p.totalPayments || 0), 0),
        cashBalance: projects.reduce((x, p) => x + (p.cashBalance || 0), 0)
    }),
    [suppliers, projects]
  );

  const quickTiles = useMemo<QuickTile[]>(() => {
    const tiles: QuickTile[] = [];
    const add = (cond: boolean, tile: QuickTile) => {
      if (cond) tiles.push(tile);
    };

    switch (primaryRole) {
      case 'admin':
        add(canReadUsers, { label: 'إدارة المستخدمين', to: '/emg/users', icon: <IconUsers size={22} />, tone: 'primary' });
        add(canReadRoles, { label: 'الأدوار والصلاحيات', to: '/emg/roles', icon: <IconUserShield size={22} />, tone: 'secondary' });
        add(false, {
          label: 'مركز المراجعة',
          to: '/emg/review-center',
          icon: <IconChecklist size={22} />,
          tone: 'warning',
          badge: totals.openMovements
        });
        add(canReadProjects, { label: 'المشاريع', to: '/emg/projects', icon: <IconBuilding size={22} />, tone: 'primary' });
        add(canMigrateData, { label: 'ترحيل البيانات', to: '/emg/imports', icon: <IconFileUpload size={22} />, tone: 'success' });
        add(canReadReports, { label: 'التقارير', to: '/emg/reports', icon: <IconFileAnalytics size={22} />, tone: 'secondary' });
        break;

      case 'director':
        add(canReadProjects, { label: 'محفظة المشروعات', to: '/emg/projects', icon: <IconBuilding size={22} />, tone: 'primary' });
        add(canReadReports, { label: 'كشف الحساب المجمع', to: '/emg/reports/consolidated', icon: <IconFileAnalytics size={22} />, tone: 'secondary' });
        add(canReadReports, { label: 'التقارير المالية', to: '/emg/reports', icon: <IconTable size={22} />, tone: 'primary' });
        add(canReadSuppliers, { label: 'كشوف المقاولين', to: '/emg/contractor-statements', icon: <IconClipboardCheck size={22} />, tone: 'success' });
        add(canReadSuppliers, { label: 'حسابات الموردين', to: '/emg/suppliers', icon: <IconTruckDelivery size={22} />, tone: 'warning' });
        break;

      case 'accountant':
        add(canReadProjects, { label: 'المشروعات والدفعات', to: '/emg/projects', icon: <IconBuilding size={22} />, tone: 'success' });
        add(canReadSuppliers, { label: 'حسابات الموردين', to: '/emg/suppliers', icon: <IconTruckDelivery size={22} />, tone: 'warning' });
        add(canReadSuppliers, { label: 'كشوف المقاولين', to: '/emg/contractor-statements', icon: <IconClipboardCheck size={22} />, tone: 'primary' });
        add(canReadReports, { label: 'مركز التقارير المالي', to: '/emg/reports/builder', icon: <IconTable size={22} />, tone: 'primary' });
        add(canReadReports, { label: 'كشف الحساب المجمع', to: '/emg/reports/consolidated', icon: <IconFileAnalytics size={22} />, tone: 'secondary' });
        add(canMigrateData || canImportApprovedHistory, { label: 'استيراد البيانات', to: '/emg/imports', icon: <IconFileUpload size={22} />, tone: 'primary' });
        break;

      case 'project_manager':
        add(false, {
          label: 'الاعتمادات الفنية',
          to: '/emg/review-center',
          icon: <IconChecklist size={22} />,
          tone: 'warning',
          badge: totals.openMovements
        });
        add(canReadProjects, { label: 'المشاريع المسندة', to: '/emg/projects', icon: <IconBuilding size={22} />, tone: 'primary' });
        add(canReadReports, { label: 'تقارير المشروعات', to: '/emg/reports', icon: <IconFileAnalytics size={22} />, tone: 'secondary' });
        add(canReadAttendance, { label: 'حضور الموقع', to: '/emg/attendance', icon: <IconClock size={22} />, tone: 'success' });
        add(canReadNotifications, {
          label: 'التنبيهات',
          to: '/emg/notifications',
          icon: <IconBell size={22} />,
          tone: 'error',
          badge: notifications.length
        });
        break;

      case 'engineer':
        add(canReadProjects, {
          label: 'حركة الموقع اليومية',
          to: projects.length === 1 ? `/emg/projects/${projects[0].id}/movements` : '/emg/projects',
          icon: <IconClipboardCheck size={22} />,
          tone: 'primary'
        });
        add(canReadAttendance, {
          label: attendance ? 'سجل الحضور' : 'تسجيل الحضور',
          to: '/emg/attendance',
          icon: <IconClock size={22} />,
          tone: 'success'
        });
        add(canReadSuppliers, { label: 'التوريدات والموردين', to: '/emg/suppliers', icon: <IconTruckDelivery size={22} />, tone: 'warning' });
        add(canReadNotifications, {
          label: 'التنبيهات',
          to: '/emg/notifications',
          icon: <IconBell size={22} />,
          tone: 'secondary',
          badge: notifications.length
        });
        break;

      case 'user':
      default:
        add(true, { label: 'البروفايل الشخصي', to: '/emg/profile', icon: <IconUser size={22} />, tone: 'primary' });
        add(canReadAttendance, { label: 'الحضور والانصراف', to: '/emg/attendance', icon: <IconClock size={22} />, tone: 'success' });
        add(canReadNotifications, {
          label: 'الإشعارات',
          to: '/emg/notifications',
          icon: <IconBell size={22} />,
          tone: 'secondary',
          badge: notifications.length
        });
        add(canReadProjects, { label: 'المشاريع', to: '/emg/projects', icon: <IconBuilding size={22} />, tone: 'primary' });
        break;
    }

    return tiles;
  }, [
    attendance,
    canImportApprovedHistory,
    canMigrateData,
    canReadAdvances,
    canReadAttendance,
    canReadNotifications,
    canReadProjects,
    canReadReports,
    canReadReviewCenter,
    canReadRoles,
    canReadSuppliers,
    canReadUsers,
    notifications.length,
    primaryRole,
    projects,
    totals.openMovements
  ]);

  if (engineerView) {
    return <EngineerMobileDashboard />;
  }

  if (loading) return <DashboardSkeleton />;

  const hasFinancialAccess = (canReadAdvances || canReadSuppliers) && !engineerView;
  const isGeneralUserWithoutProjects = primaryRole === 'user' && !canReadProjects;
  const tileMdSize = quickTiles.length <= 4 ? 3 : quickTiles.length === 5 ? 2.4 : 2;

  const timelineItems: Array<{ label: string; time?: string; color: string }> = [];
  if (canReadAttendance) {
    timelineItems.push({
      label: attendance ? 'تم تسجيل حضور اليوم بنجاح' : 'لم يتم تسجيل الحضور اليوم بعد',
      time: attendance
        ? new Date(attendance.checkInAtUtc).toLocaleTimeString('ar-EG-u-nu-latn', { hour: '2-digit', minute: '2-digit' })
        : 'اليوم',
      color: attendance ? theme.vars.palette.success.dark : theme.vars.palette.warning.main
    });
  }
  notifications.slice(0, 4).forEach((n, idx) => {
    timelineItems.push({
      label: n.title,
      color:
        idx % 3 === 0
          ? theme.vars.palette.primary.main
          : idx % 3 === 1
            ? theme.vars.palette.secondary.main
            : theme.vars.palette.warning.main
    });
  });

  return (
    <Box sx={{ pb: 4 }}>
      {/* Clean Header */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={1.5}
        sx={{ mb: 2.5 }}
      >
        <Box>
          <Typography variant="h2" sx={{ fontSize: { xs: '1.45rem', sm: '1.75rem' }, fontWeight: 800 }}>
            لوحة {roleTitle(user)}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.35 }}>
            {roleSubtitle(user)}
          </Typography>
        </Box>
        {canReadProjects && (
          <Button
            component={Link}
            to="/emg/projects"
            variant="contained"
            startIcon={<IconBuilding size={18} />}
            sx={{ borderRadius: 2, fontWeight: 700, minHeight: 40 }}
          >
            عرض المشاريع
          </Button>
        )}
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {canReadProjects && (
        <Box sx={{ mb: 2.5 }}>
          <FinancialSummary
            title="نظرة عامة على المشروعات"
            subtitle="نظرة مالية شاملة للشركة حسب صلاحيات المستخدم."
            totalWorks={totals.totalWorks}
            totalPayments={totals.totalPayments}
            cashBalance={projects.some(p => p.historicalFundingUnknown) ? null : totals.cashBalance}
            cashBalanceStatus={totals.cashBalance > 0 ? 'العميل له فلوس' : totals.cashBalance < 0 ? 'لنا مستحقات لدى العميل' : 'الحساب متوازن'}
          />
        </Box>
      )}

      {isGeneralUserWithoutProjects && (
        <Alert severity="info" sx={{ mb: 2 }}>
          مرحباً بك في منظومة EMG. حسابك نشط حالياً، وفي حال كنت بحاجة لصلاحيات إضافية للمشاريع أو العمليات التشغيلية والمالية، يرجى التواصل مع مسؤول المنظومة.
        </Alert>
      )}

      {/* Tier 1: Berry Signature 2 Gradient Hero Cards + 2x2 Stacked Metric Cards */}
      {(canReadProjects || hasFinancialAccess) && (
        <Grid container spacing={2} sx={{ mb: 2.5 }}>
          {/* Left/Right Half 1: Two Tall Gradient Berry Hero Cards */}
          <Grid size={{ xs: 12, lg: 6 }}>
            <Grid container spacing={2} sx={{ height: '100%' }}>
              {hasFinancialAccess && canReadAdvances ? (
                <Grid size={{ xs: 12, sm: 6 }}>
                  <BerryGradientHeroCard
                    title="دفعات تحت الحساب"
                    value={fmt(totals.totalPayments)}
                    subtitle="الدفعات المثبتة بالجنيه المصري"
                    icon={<IconCash size={22} />}
                    variant="purple"
                  />
                </Grid>
              ) : null}
              {canReadProjects && (
                <Grid size={{ xs: 12, sm: 6 }}>
                  <BerryGradientHeroCard
                    title={engineerView ? 'مشاريعك الميدانية' : 'المشاريع النشطة'}
                    value={String(projects.length)}
                    subtitle={`${totals.activeProjects} مشروع نشط`}
                    icon={<IconBuilding size={22} />}
                    variant="blue"
                  />
                </Grid>
              )}
            </Grid>
          </Grid>

          {/* Half 2: 2x2 Compact Stacked White Metric Cards */}
          <Grid size={{ xs: 12, lg: 6 }}>
            <Grid container spacing={2} sx={{ height: '100%' }}>
              {hasFinancialAccess && canReadSuppliers && (
                <Grid size={{ xs: 6 }}>
                  <StackedMetricCard
                    title="متبقي الموردين"
                    value={fmt(totals.supplierRemaining)}
                    icon={<IconTruckDelivery size={20} />}
                    tone="warning"
                  />
                </Grid>
              )}
              {hasFinancialAccess && canReadAdvances && (
                <Grid size={{ xs: 6 }}>
                  <StackedMetricCard
                    title="الأرصدة المدينة المثبتة"
                    value={fmt(totals.due)}
                    icon={<IconReceipt size={20} />}
                    tone="error"
                  />
                </Grid>
              )}
              {canReadNotifications && (
                <Grid size={{ xs: 6 }}>
                  <StackedMetricCard
                    title="تنبيهات غير مقروءة"
                    value={String(notifications.length)}
                    icon={<IconBell size={20} />}
                    tone="secondary"
                  />
                </Grid>
              )}
              {engineerView && canReadAttendance && (
                <Grid size={{ xs: 6 }}>
                  <StackedMetricCard
                    title="حضور اليوم"
                    value={attendance ? 'مسجل' : 'غير مسجل'}
                    icon={<IconClock size={20} />}
                    tone={attendance ? 'success' : 'warning'}
                  />
                </Grid>
              )}
            </Grid>
          </Grid>
        </Grid>
      )}

      {/* Tier 2: Compact Horizontal Icon-Tile Quick Shortcuts */}
      {quickTiles.length > 0 && (
        <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
          {quickTiles.map((tile) => (
            <Grid key={tile.label} size={{ xs: 6, sm: 4, md: tileMdSize }}>
              <ShortcutTile tile={tile} />
            </Grid>
          ))}
        </Grid>
      )}

      {/* Tier 3: Balanced 2-Column Section — Clean Project Cards + Daily Tracking Timeline */}
      <Grid container spacing={2}>
        {canReadProjects && (
          <Grid size={{ xs: 12, lg: 7 }}>
            <MainCard
              title={engineerView ? 'مشاريعك الميدانية — وصول سريع للتشغيل' : 'المشاريع الجارية'}
              secondary={
                <Button component={Link} to="/emg/projects" size="small" endIcon={<IconArrowUpLeft size={16} />}>
                  كل المشاريع
                </Button>
              }
              sx={{ borderRadius: 3 }}
              contentSX={{ p: { xs: 1.75, sm: 2.25 } }}
            >
              {projects.length === 0 ? (
                <Stack spacing={1} alignItems="center" sx={{ py: 4, textAlign: 'center' }}>
                  <Box
                    sx={{
                      width: 48,
                      height: 48,
                      borderRadius: '50%',
                      display: 'grid',
                      placeItems: 'center',
                      bgcolor: withAlpha(theme.vars.palette.primary.main, 0.1),
                      color: 'primary.main'
                    }}
                  >
                    <IconBuilding size={24} />
                  </Box>
                  <Typography variant="subtitle1" fontWeight={700}>
                    لا توجد مشاريع مسجلة حالياً
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    ستظهر المشاريع المسندة لحسابك هنا فور إضافتها أو إسنادها.
                  </Typography>
                </Stack>
              ) : (
                <Stack spacing={1.5}>
                  {projects.slice(0, 5).map((project) => (
                    <Card
                      key={project.id}
                      variant="outlined"
                      sx={{
                        borderRadius: 2.5,
                        transition: 'border-color 150ms ease, box-shadow 150ms ease',
                        '&:hover': { borderColor: 'primary.main', boxShadow: theme.shadows[1] }
                      }}
                    >
                      <CardContent sx={{ p: { xs: 1.5, sm: 2 }, '&:last-child': { pb: { xs: 1.5, sm: 2 } } }}>
                        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                          <Box sx={{ minWidth: 0 }}>
                            <Typography
                              component={Link}
                              to={`/emg/projects/${project.id}`}
                              variant="subtitle1"
                              fontWeight={700}
                              sx={{ color: 'text.primary', textDecoration: 'none', '&:hover': { color: 'primary.main', textDecoration: 'underline' } }}
                            >
                              {project.name}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.25 }}>
                              {project.code} · {project.engineerName || 'بدون مهندس'}
                            </Typography>
                          </Box>
                          <Stack direction="row" spacing={0.75} alignItems="center" flexShrink={0}>
                            <Chip size="small" label={project.status} color={project.status === 'نشط' ? 'success' : 'warning'} />

                          </Stack>
                        </Stack>

                        {hasFinancialAccess && (
                          <Box sx={{ mt: 1.25 }}>
                            <FinancialSummary
                              totalWorks={project.totalWorks}
                              totalPayments={project.historicalFundingUnknown && !project.totalPayments ? null : project.totalPayments}
                              cashBalance={project.historicalFundingUnknown ? null : project.cashBalance}
                              cashBalanceStatus={project.cashBalanceStatus}
                            />
                          </Box>
                        )}

                        <Divider sx={{ my: 1.25 }} />

                        {engineerView ? (
                          <Stack direction="row" flexWrap="wrap" useFlexGap spacing={0.75}>
                            {canSiteMovements && (
                              <Button component={Link} to={`/emg/projects/${project.id}/movements`} size="small" variant="contained" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                                حركة الموقع
                              </Button>
                            )}
                            {canExcavation && (
                              <Button component={Link} to={`/emg/projects/${project.id}/excavation`} size="small" variant="outlined" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                                الحفر
                              </Button>
                            )}
                            {canConcrete && (
                              <Button component={Link} to={`/emg/projects/${project.id}/concrete`} size="small" variant="outlined" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                                الخرسانة
                              </Button>
                            )}
                            {canSteel && (
                              <Button component={Link} to={`/emg/projects/${project.id}/steel`} size="small" variant="outlined" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                                الحديد
                              </Button>
                            )}
                            {canFinishing && (
                              <Button component={Link} to={`/emg/projects/${project.id}/finishing`} size="small" variant="outlined" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                                التشطيبات
                              </Button>
                            )}
                            <Button component={Link} to={`/emg/projects/${project.id}`} size="small" variant="text" sx={{ minHeight: 34, fontSize: '0.78rem' }}>
                              التفاصيل
                            </Button>
                          </Stack>
                        ) : (
                          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                            <Button component={Link} to={`/emg/projects/${project.id}`} size="small" variant="outlined">
                              فتح المشروع
                            </Button>
                            {accountantView && canReadReports && (
                              <Button component={Link} to={`/emg/reports/${project.id}`} size="small" variant="text">
                                التقرير المالي
                              </Button>
                            )}
                            {(pmView || canSiteMovements) && (
                              <Button component={Link} to={`/emg/projects/${project.id}/movements`} size="small" variant="text">
                                حركة الموقع
                              </Button>
                            )}
                          </Stack>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </Stack>
              )}
            </MainCard>
          </Grid>
        )}

        {/* Daily Tracking Vertical Timeline */}
        <Grid size={{ xs: 12, lg: canReadProjects ? 5 : 12 }}>
          <MainCard
            title="المتابعة اليومية"
            secondary={
              canReadNotifications ? (
                <Button component={Link} to="/emg/notifications" size="small">
                  كل التنبيهات
                </Button>
              ) : undefined
            }
            sx={{ borderRadius: 3, height: '100%' }}
            contentSX={{ p: { xs: 1.75, sm: 2.25 } }}
          >
            {timelineItems.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>
                لا توجد تحديثات يومية مسجلة حالياً.
              </Typography>
            ) : (
              <Stack spacing={0}>
                {timelineItems.map((entry, idx) => {
                  const isLast = idx === timelineItems.length - 1;
                  return (
                    <Stack key={`${entry.label}-${idx}`} direction="row" spacing={2} alignItems="flex-start">
                      <Stack alignItems="center" sx={{ pt: 0.6 }}>
                        <Box
                          sx={{
                            width: 12,
                            height: 12,
                            borderRadius: '50%',
                            bgcolor: entry.color,
                            boxShadow: `0 0 0 4px ${withAlpha(entry.color, 0.16)}`
                          }}
                        />
                        {!isLast && <Box sx={{ width: 2, minHeight: 38, bgcolor: 'divider', my: 0.5 }} />}
                      </Stack>
                      <Box sx={{ pb: isLast ? 0 : 2.25, minWidth: 0 }}>
                        {entry.time && (
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.25 }}>
                            {entry.time}
                          </Typography>
                        )}
                        <Typography variant="body2" fontWeight={600} sx={{ lineHeight: 1.5 }}>
                          {entry.label}
                        </Typography>
                      </Box>
                    </Stack>
                  );
                })}
              </Stack>
            )}
            {canReadAttendance && !attendance && (
              <Box sx={{ mt: 2 }}>
                <Button fullWidth component={Link} to="/emg/attendance" variant="contained" startIcon={<IconClock size={18} />}>
                  تسجيل حضور اليوم
                </Button>
              </Box>
            )}
          </MainCard>
        </Grid>
      </Grid>
    </Box>
  );
}
