import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  FormControl,
  Grid,
  MenuItem,
  Select,
  Skeleton,
  Stack,
  Typography
} from '@mui/material';
import {
  IconBuilding,
  IconChevronLeft,
  IconClipboardList,
  IconClock,
  IconHammer,
  IconMapPin,
  IconPaint,
  IconPlus,
  IconReceipt,
  IconShovel,
  IconTruckDelivery,
  IconWall
} from '@tabler/icons-react';
import { EmgSiteMovement, getEmgSiteMovements } from 'api/emg';
import useAuth from 'hooks/useAuth';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import { useTodayAttendanceCache } from 'hooks/useEmgData';
import { hasAnyPermission } from 'Modules/EMG/roleAccess';
import { formatEmgMoney } from 'utils/emgFormatting';
import { WorkStatusChip } from 'Modules/EMG/WorkSectionShell';
import FinancialSummary from 'Modules/EMG/FinancialSummary';

export default function EngineerMobileDashboard() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { projects, activeProject, activeProjectId, setActiveProjectId, loading: projectsLoading } = useActiveProject();
  const { data: attendance } = useTodayAttendanceCache(true);

  const [recentMovements, setRecentMovements] = useState<EmgSiteMovement[]>([]);
  const [movementsLoading, setMovementsLoading] = useState(false);

  const canCreateMovement = hasAnyPermission(user, ['site-movements.create']);
  const canMovements = hasAnyPermission(user, ['site-movements.view', 'site-movements.create', 'projects.view']);
  const canExcavation = hasAnyPermission(user, ['excavation.view', 'excavation.create']);
  const canConcrete = hasAnyPermission(user, ['concrete.view', 'concrete.create']);
  const canSteel = hasAnyPermission(user, ['steel.view', 'steel.create']);
  const canFinishing = hasAnyPermission(user, ['finishing.view', 'finishing.create']);
  const canAttendance = hasAnyPermission(user, ['attendance.view', 'attendance.manage']);
  const canSuppliers = hasAnyPermission(user, ['suppliers.view', 'suppliers.manage']);

  useEffect(() => {
    if (!activeProjectId || !canMovements) {
      setRecentMovements([]);
      return;
    }
    let active = true;
    setMovementsLoading(true);
    getEmgSiteMovements(activeProjectId)
      .then((rows) => {
        if (active) setRecentMovements(rows.slice(0, 8));
      })
      .catch(() => {
        if (active) setRecentMovements([]);
      })
      .finally(() => {
        if (active) setMovementsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [activeProjectId, canMovements]);

  const workModules = [
    canMovements && activeProjectId
      ? { label: 'حركات الموقع', to: `/emg/projects/${activeProjectId}/movements`, icon: <IconClipboardList size={22} />, color: '#1565c0', bg: 'rgba(21,101,192,0.12)' }
      : null,
    canExcavation && activeProjectId
      ? { label: 'أعمال الحفر', to: `/emg/projects/${activeProjectId}/excavation`, icon: <IconShovel size={22} />, color: '#b45309', bg: 'rgba(180,83,9,0.12)' }
      : null,
    canConcrete && activeProjectId
      ? { label: 'الخرسانات', to: `/emg/projects/${activeProjectId}/concrete`, icon: <IconWall size={22} />, color: '#4527a0', bg: 'rgba(69,39,160,0.12)' }
      : null,
    canSteel && activeProjectId
      ? { label: 'الحديد', to: `/emg/projects/${activeProjectId}/steel`, icon: <IconHammer size={22} />, color: '#0f766e', bg: 'rgba(15,118,110,0.12)' }
      : null,
    canFinishing && activeProjectId
      ? { label: 'التشطيبات', to: `/emg/projects/${activeProjectId}/finishing`, icon: <IconPaint size={22} />, color: '#c2410c', bg: 'rgba(194,65,12,0.12)' }
      : null,
    canSuppliers
      ? { label: 'الموردون', to: '/emg/suppliers', icon: <IconTruckDelivery size={22} />, color: '#1d4ed8', bg: 'rgba(29,78,216,0.12)' }
      : null
  ].filter(Boolean) as Array<{ label: string; to: string; icon: React.ReactNode; color: string; bg: string }>;

  if (projectsLoading) {
    return (
      <Stack spacing={2} sx={{ pb: 4 }}>
        <Skeleton variant="rounded" height={150} sx={{ borderRadius: 3 }} />
        <Skeleton variant="rounded" height={54} sx={{ borderRadius: 2.5 }} />
        <Grid container spacing={1.5}>
          {[0, 1, 2, 3].map((i) => (
            <Grid key={i} size={6}>
              <Skeleton variant="rounded" height={88} sx={{ borderRadius: 3 }} />
            </Grid>
          ))}
        </Grid>
      </Stack>
    );
  }

  const portfolioFinancials = useMemo(() => {
    const totalWorks = projects.reduce((sum, p) => sum + (p.totalWorks ?? 0), 0);
    const totalPayments = projects.reduce((sum, p) => sum + (p.totalPayments ?? 0), 0);
    const cashBalance = projects.some(p => p.historicalFundingUnknown) ? null : totalPayments - totalWorks;
    return { totalWorks, totalPayments, cashBalance };
  }, [projects]);

  return (
    <Box sx={{ pb: 4, maxWidth: 960, mx: 'auto', width: '100%' }}>
      {/* Approved Financial Summary across accessible projects */}
      {projects.length > 0 && (
        <Box sx={{ mb: 2 }}>
          <FinancialSummary
            title="نظرة عامة على المشروعات"
            subtitle="إجماليات المشروعات المسندة لحسابك."
            totalWorks={portfolioFinancials.totalWorks}
            totalPayments={portfolioFinancials.totalPayments}
            cashBalance={portfolioFinancials.cashBalance}
          />
        </Box>
      )}

      {/* Active Project Hero Card + Fast Switcher */}
      <Card
        sx={{
          borderRadius: 3.5,
          color: '#fff',
          background: 'linear-gradient(135deg, #0d47a1 0%, #1565c0 55%, #1e88e5 100%)',
          boxShadow: '0 14px 30px rgba(13, 71, 161, 0.24)',
          mb: 2
        }}
      >
        <CardContent sx={{ p: { xs: 2, sm: 2.75 } }}>
          <Stack spacing={1.75}>
            <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
              <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.85)', fontWeight: 700 }}>
                المشروع النشط · {user?.name || 'مهندس الموقع'}
              </Typography>
              {activeProject && (
                <Chip
                  size="small"
                  label={activeProject.code}
                  sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', fontWeight: 800, height: 24 }}
                />
              )}
            </Stack>

            {projects.length === 0 ? (
              <Alert severity="info" sx={{ borderRadius: 2 }}>
                لا توجد مشروعات مسندة لحسابك حالياً.
              </Alert>
            ) : (
              <>
                <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={1.5}>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="h2" sx={{ color: '#fff', fontWeight: 900, fontSize: { xs: '1.35rem', sm: '1.65rem' } }}>
                      {activeProject?.name}
                    </Typography>
                    <Stack direction="row" spacing={0.75} alignItems="center" sx={{ mt: 0.4, color: 'rgba(255,255,255,0.86)' }}>
                      <IconMapPin size={15} />
                      <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.88)' }}>
                        {activeProject?.location || 'موقع المشروع'}
                      </Typography>
                    </Stack>
                  </Box>

                  {projects.length > 1 && (
                    <FormControl size="small" sx={{ minWidth: { xs: '100%', sm: 220 } }}>
                      <Select
                        value={String(activeProjectId ?? '')}
                        aria-label="تبديل المشروع النشط"
                        onChange={(e) => setActiveProjectId(Number(e.target.value))}
                        sx={{
                          bgcolor: 'rgba(255,255,255,0.16)',
                          color: '#fff',
                          borderRadius: 2.5,
                          fontWeight: 700,
                          '& .MuiOutlinedInput-notchedOutline': { borderColor: 'rgba(255,255,255,0.35)' },
                          '& .MuiSvgIcon-root': { color: '#fff' }
                        }}
                      >
                        {projects.map((p) => (
                          <MenuItem key={p.id} value={String(p.id)}>
                            {p.name} ({p.code})
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  )}
                </Stack>

                {/* Primary One-Handed CTA */}
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} sx={{ pt: 0.5 }}>
                  {canCreateMovement && activeProjectId && (
                    <Button
                      fullWidth
                      variant="contained"
                      size="large"
                      startIcon={<IconPlus size={20} />}
                      onClick={() => navigate(`/emg/projects/${activeProjectId}/movements?quickAdd=1`)}
                      sx={{
                        minHeight: 48,
                        borderRadius: 2.5,
                        bgcolor: '#ffffff',
                        color: '#0d47a1',
                        fontWeight: 900,
                        fontSize: '0.98rem',
                        '&:hover': { bgcolor: '#eef5ff' }
                      }}
                    >
                      إضافة حركة موقع جديدة
                    </Button>
                  )}
                  {canAttendance && (
                    <Button
                      component={Link}
                      to="/emg/attendance"
                      variant="outlined"
                      size="large"
                      startIcon={<IconClock size={19} />}
                      sx={{
                        minHeight: 48,
                        minWidth: { sm: 190 },
                        borderRadius: 2.5,
                        color: '#fff',
                        borderColor: 'rgba(255,255,255,0.45)',
                        fontWeight: 800,
                        '&:hover': { borderColor: '#fff', bgcolor: 'rgba(255,255,255,0.12)' }
                      }}
                    >
                      {attendance ? 'تم تسجيل الحضور' : 'تسجيل الحضور'}
                    </Button>
                  )}
                </Stack>
              </>
            )}
          </Stack>
        </CardContent>
      </Card>

      {/* Assigned Work Modules Grid */}
      {workModules.length > 0 && (
        <Box sx={{ mb: 2.5 }}>
          <Typography variant="h4" sx={{ fontWeight: 800, mb: 1.25 }}>
            أقسام التنفيذ السريعة
          </Typography>
          <Grid container spacing={1.25}>
            {workModules.map((mod) => (
              <Grid key={mod.label} size={{ xs: 6, sm: 4 }}>
                <Card variant="outlined" sx={{ borderRadius: 3, height: '100%' }}>
                  <CardActionArea component={Link} to={mod.to} sx={{ height: '100%', p: 1.75 }}>
                    <Stack spacing={1} alignItems="center" textAlign="center">
                      <Avatar sx={{ width: 46, height: 46, borderRadius: 2.5, bgcolor: mod.bg, color: mod.color }}>
                        {mod.icon}
                      </Avatar>
                      <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                        {mod.label}
                      </Typography>
                    </Stack>
                  </CardActionArea>
                </Card>
              </Grid>
            ))}
          </Grid>
        </Box>
      )}

      {/* Recent Site Transactions (Mobile-Friendly Cards) */}
      {canMovements && activeProjectId && (
        <Box>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.25 }}>
            <Typography variant="h4" sx={{ fontWeight: 800 }}>
              أحدث حركات الموقع
            </Typography>
            <Button
              component={Link}
              to={`/emg/projects/${activeProjectId}/movements`}
              size="small"
              endIcon={<IconChevronLeft size={16} />}
              sx={{ fontWeight: 700 }}
            >
              عرض الكل
            </Button>
          </Stack>

          {movementsLoading ? (
            <Stack spacing={1.25}>
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} variant="rounded" height={86} sx={{ borderRadius: 3 }} />
              ))}
            </Stack>
          ) : recentMovements.length === 0 ? (
            <Card variant="outlined" sx={{ borderRadius: 3, p: 3, textAlign: 'center' }}>
              <IconReceipt size={32} style={{ opacity: 0.45 }} />
              <Typography color="text.secondary" sx={{ mt: 1, fontWeight: 600 }}>
                لا توجد حركات مسجلة لهذا المشروع بعد.
              </Typography>
            </Card>
          ) : (
            <Stack spacing={1.25}>
              {recentMovements.map((movement) => (
                <Card key={movement.id} variant="outlined" sx={{ borderRadius: 3 }}>
                  <CardActionArea component={Link} to={`/emg/site-movements/${movement.id}`} sx={{ p: 1.75 }}>
                    <Stack spacing={0.85}>
                      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                        <Box sx={{ minWidth: 0 }}>
                          <Typography variant="subtitle1" sx={{ fontWeight: 800 }} noWrap>
                            {movement.description}
                          </Typography>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                            {movement.expenseCategory || movement.movementType}
                            {movement.supplierName ? ` · ${movement.supplierName}` : ''}
                            {movement.reference ? ` · #${movement.reference}` : ''}
                          </Typography>
                        </Box>
                        <Typography variant="subtitle1" color="primary.main" sx={{ fontWeight: 900, fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' }}>
                          {formatEmgMoney(movement.totalAmount)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between" alignItems="center">
                        <Typography variant="caption" color="text.secondary">
                          {new Date(movement.movementDate).toLocaleDateString('ar-EG-u-nu-latn')} · {movement.isPaid ? movement.paymentMethod || 'نقدي' : 'آجل'}
                        </Typography>
                        <WorkStatusChip status={movement.status} />
                      </Stack>
                    </Stack>
                  </CardActionArea>
                </Card>
              ))}
            </Stack>
          )}
        </Box>
      )}

      {/* Quick link to All Projects if engineer has multiple */}
      {projects.length > 1 && (
        <Button
          component={Link}
          to="/emg/projects"
          fullWidth
          variant="outlined"
          startIcon={<IconBuilding size={18} />}
          sx={{ mt: 2.5, minHeight: 44, borderRadius: 2.5, fontWeight: 700 }}
        >
          عرض جميع المشروعات المسندة ({projects.length})
        </Button>
      )}
    </Box>
  );
}
