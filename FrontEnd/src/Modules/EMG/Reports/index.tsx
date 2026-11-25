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
  FormControl,
  Grid,
  InputAdornment,
  InputLabel,
  MenuItem,
  Pagination,
  Select,
  Skeleton,
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
  useTheme
} from '@mui/material';
import {
  IconArrowUpLeft,
  IconBuilding,
  IconCash,
  IconChartBar,
  IconFileAnalytics,
  IconLayoutGrid,
  IconList,
  IconMapPin,
  IconReceipt,
  IconRefresh,
  IconSearch,
  IconTable,
  IconUser
} from '@tabler/icons-react';
import { Link, useNavigate } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { hasAnyPermission } from 'Modules/EMG/roleAccess';
import { withAlpha } from 'utils/colorUtils';
import { EmgProject, getEmgProjects } from 'api/emg';

const money = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;

type StatusFilter = 'all' | 'active' | 'review_status' | 'has_open_movements';

export default function EmgReports() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const theme = useTheme();

  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [viewMode, setViewMode] = useState<'table' | 'cards'>('table');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const canViewReports = hasAnyPermission(user, ['reports.view']);

  const load = () => {
    setLoading(true);
    setError(null);
    getEmgProjects()
      .then(setProjects)
      .catch(() => setError('تعذر تحميل بيانات المشاريع للتقارير.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (canViewReports) load();
    else setLoading(false);
  }, [canViewReports]);

  const totals = useMemo(() => {
    const received = projects.reduce((sum, p) => sum + (p.totalReceived || 0), 0);
    const issued = projects.reduce((sum, p) => sum + (p.totalIssued || 0), 0);
    const openMovements = projects.reduce((sum, p) => sum + (p.openMovements || 0), 0);
    const withReviews = projects.filter((p) => (p.openMovements || 0) > 0).length;
    const active = projects.filter((p) => p.status === 'نشط').length;
    return { received, issued, net: received - issued, openMovements, withReviews, active };
  }, [projects]);

  const filteredProjects = useMemo(() => {
    const q = search.trim().toLowerCase();
    return projects.filter((p) => {
      if (statusFilter === 'active' && p.status !== 'نشط') return false;
      if (statusFilter === 'review_status' && p.status !== 'تحت المراجعة') return false;
      if (statusFilter === 'has_open_movements' && !(p.openMovements > 0)) return false;
      if (!q) return true;
      return `${p.name} ${p.code} ${p.engineerName || ''} ${p.location || ''}`.toLowerCase().includes(q);
    });
  }, [projects, search, statusFilter]);

  const pageCount = Math.max(1, Math.ceil(filteredProjects.length / pageSize));
  const safePage = Math.min(page, pageCount);
  const visibleProjects = filteredProjects.slice((safePage - 1) * pageSize, safePage * pageSize);

  if (!canViewReports) {
    return <Alert severity="error">ليس لديك صلاحية عرض التقارير.</Alert>;
  }

  return (
    <Box sx={{ pb: 4 }}>
      {/* Executive Header */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={1.5}
        sx={{ mb: 2.5 }}
      >
        <Box>
          <Typography variant="h2" sx={{ fontSize: { xs: '1.45rem', sm: '1.75rem' }, fontWeight: 800 }}>
            مركز التقارير والتصدير
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.35 }}>
            التقارير المالية والتشغيلية للمشاريع وكشوف الحسابات المجمعة
          </Typography>
        </Box>
        <Button
          onClick={load}
          variant="outlined"
          disabled={loading}
          startIcon={<IconRefresh size={17} />}
          sx={{ borderRadius: 2, fontWeight: 600 }}
        >
          تحديث البيانات
        </Button>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Tier 1: Financial Totals (Berry Signature Gradient Cards) + Report Hub Shortcuts */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        {/* Left/Right Half 1: Financial Summary Gradient Cards */}
        <Grid size={{ xs: 12, lg: 6 }}>
          <Grid container spacing={2} sx={{ height: '100%' }}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <Card
                sx={{
                  height: '100%',
                  minHeight: 152,
                  position: 'relative',
                  overflow: 'hidden',
                  borderRadius: 3.5,
                  color: '#ffffff',
                  background: 'linear-gradient(135deg, #0d47a1 0%, #1565c0 55%, #1e88e5 100%)',
                  boxShadow: '0 10px 26px rgba(13, 43, 82, 0.18)',
                  '&::before': {
                    content: '""',
                    position: 'absolute',
                    width: 170,
                    height: 170,
                    borderRadius: '50%',
                    background: 'rgba(255,255,255,0.10)',
                    top: -70,
                    left: -50,
                    pointerEvents: 'none'
                  }
                }}
              >
                <CardContent sx={{ position: 'relative', zIndex: 1, p: 2.5, '&:last-child': { pb: 2.5 } }}>
                  <Stack spacing={1.5}>
                    <Box
                      sx={{
                        width: 42,
                        height: 42,
                        borderRadius: 2.5,
                        display: 'grid',
                        placeItems: 'center',
                        bgcolor: 'rgba(255,255,255,0.92)',
                        color: '#1565c0'
                      }}
                    >
                      <IconCash size={22} />
                    </Box>
                    <Box>
                      <Typography variant="h2" sx={{ color: '#ffffff', fontWeight: 800, fontSize: '1.5rem', fontVariantNumeric: 'tabular-nums' }}>
                        {loading ? '...' : formatMoney(totals.received)}
                      </Typography>
                      <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.88)', fontWeight: 600, mt: 0.25 }}>
                        إجمالي الوارد ({projects.length} مشروع)
                      </Typography>
                    </Box>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>

            <Grid size={{ xs: 12, sm: 6 }}>
              <Card
                sx={{
                  height: '100%',
                  minHeight: 152,
                  position: 'relative',
                  overflow: 'hidden',
                  borderRadius: 3.5,
                  color: '#ffffff',
                  background: 'linear-gradient(135deg, #311b92 0%, #5e35b1 55%, #7e57c2 100%)',
                  boxShadow: '0 10px 26px rgba(49, 27, 146, 0.18)',
                  '&::before': {
                    content: '""',
                    position: 'absolute',
                    width: 170,
                    height: 170,
                    borderRadius: '50%',
                    background: 'rgba(255,255,255,0.10)',
                    top: -70,
                    left: -50,
                    pointerEvents: 'none'
                  }
                }}
              >
                <CardContent sx={{ position: 'relative', zIndex: 1, p: 2.5, '&:last-child': { pb: 2.5 } }}>
                  <Stack spacing={1.5}>
                    <Box
                      sx={{
                        width: 42,
                        height: 42,
                        borderRadius: 2.5,
                        display: 'grid',
                        placeItems: 'center',
                        bgcolor: 'rgba(255,255,255,0.92)',
                        color: '#4527a0'
                      }}
                    >
                      <IconReceipt size={22} />
                    </Box>
                    <Box>
                      <Typography variant="h2" sx={{ color: '#ffffff', fontWeight: 800, fontSize: '1.5rem', fontVariantNumeric: 'tabular-nums' }}>
                        {loading ? '...' : formatMoney(totals.issued)}
                      </Typography>
                      <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.88)', fontWeight: 600, mt: 0.25 }}>
                        إجمالي الصادر · صافي: {loading ? '...' : formatMoney(totals.net)}
                      </Typography>
                    </Box>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </Grid>

        {/* Half 2: Direct One-Click Report Centers */}
        <Grid size={{ xs: 12, lg: 6 }}>
          <Stack spacing={1.5} sx={{ height: '100%' }}>
            <Card
              variant="outlined"
              sx={{
                flex: 1,
                borderRadius: 3,
                borderColor: 'divider',
                transition: 'border-color 150ms ease, box-shadow 150ms ease',
                '&:hover': { borderColor: 'primary.main', boxShadow: theme.shadows[2] }
              }}
            >
              <CardActionArea component={Link} to="/emg/reports/consolidated" sx={{ height: '100%', p: 2 }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.5}>
                  <Stack direction="row" spacing={1.75} alignItems="center" sx={{ minWidth: 0 }}>
                    <Box
                      sx={{
                        width: 44,
                        height: 44,
                        borderRadius: 2.5,
                        display: 'grid',
                        placeItems: 'center',
                        flexShrink: 0,
                        bgcolor: withAlpha(theme.vars.palette.primary.main, 0.12),
                        color: 'primary.main'
                      }}
                    >
                      <IconFileAnalytics size={22} />
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="subtitle1" fontWeight={700}>
                        كشف الحساب المجمع
                      </Typography>
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        مقارنة مجمعة لكل المشاريع والتمويل والموردين والأرصدة
                      </Typography>
                    </Box>
                  </Stack>
                  <IconArrowUpLeft size={18} color={theme.vars.palette.primary.main} />
                </Stack>
              </CardActionArea>
            </Card>

            <Card
              variant="outlined"
              sx={{
                flex: 1,
                borderRadius: 3,
                borderColor: 'divider',
                transition: 'border-color 150ms ease, box-shadow 150ms ease',
                '&:hover': { borderColor: 'secondary.main', boxShadow: theme.shadows[2] }
              }}
            >
              <CardActionArea component={Link} to="/emg/reports/builder" sx={{ height: '100%', p: 2 }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.5}>
                  <Stack direction="row" spacing={1.75} alignItems="center" sx={{ minWidth: 0 }}>
                    <Box
                      sx={{
                        width: 44,
                        height: 44,
                        borderRadius: 2.5,
                        display: 'grid',
                        placeItems: 'center',
                        flexShrink: 0,
                        bgcolor: withAlpha(theme.vars.palette.secondary.main, 0.12),
                        color: 'secondary.main'
                      }}
                    >
                      <IconTable size={22} />
                    </Box>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="subtitle1" fontWeight={700}>
                        مركز التقارير المالي (Report Builder)
                      </Typography>
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                        تخصيص وتصدير التقارير المالية التفصيلية والختامية
                      </Typography>
                    </Box>
                  </Stack>
                  <IconArrowUpLeft size={18} color={theme.vars.palette.secondary.main} />
                </Stack>
              </CardActionArea>
            </Card>
          </Stack>
        </Grid>
      </Grid>

      {/* Tier 2: Practical Projects Report Directory (Search + Filter + Table/Cards Toggle) */}
      <MainCard
        title={
          <Stack direction="row" spacing={1} alignItems="center">
            <IconChartBar size={20} />
            <Typography variant="h4" fontWeight={700}>
              تقارير المشاريع التفصيلية
            </Typography>
            <Chip size="small" label={`${filteredProjects.length} مشروع`} color="primary" variant="outlined" />
          </Stack>
        }
        secondary={
          <ToggleButtonGroup
            size="small"
            exclusive
            value={viewMode}
            onChange={(_, next) => next && setViewMode(next)}
            aria-label="طريقة عرض تقارير المشاريع"
            sx={{ display: { xs: 'none', md: 'inline-flex' } }}
          >
            <ToggleButton value="table" aria-label="عرض جدولي">
              <IconList size={18} />
            </ToggleButton>
            <ToggleButton value="cards" aria-label="عرض بطاقات">
              <IconLayoutGrid size={18} />
            </ToggleButton>
          </ToggleButtonGroup>
        }
        sx={{ borderRadius: 3 }}
        contentSX={{ p: { xs: 1.75, sm: 2.5 } }}
      >
        {/* Search & Filter Toolbar */}
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          justifyContent="space-between"
          alignItems={{ xs: 'stretch', md: 'center' }}
          spacing={1.5}
          sx={{ mb: 2.5 }}
        >
          <TextField
            size="small"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="ابحث باسم المشروع أو الكود أو المهندس أو الموقع..."
            sx={{ minWidth: { md: 320 } }}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <IconSearch size={18} />
                  </InputAdornment>
                )
              }
            }}
          />

          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip
              clickable
              label={`الكل (${projects.length})`}
              color={statusFilter === 'all' ? 'primary' : 'default'}
              variant={statusFilter === 'all' ? 'filled' : 'outlined'}
              onClick={() => {
                setStatusFilter('all');
                setPage(1);
              }}
            />
            <Chip
              clickable
              label={`نشط (${totals.active})`}
              color={statusFilter === 'active' ? 'success' : 'default'}
              variant={statusFilter === 'active' ? 'filled' : 'outlined'}
              onClick={() => {
                setStatusFilter('active');
                setPage(1);
              }}
            />
            <Chip
              clickable
              label={`حركات للمراجعة (${totals.withReviews})`}
              color={statusFilter === 'has_open_movements' ? 'warning' : 'default'}
              variant={statusFilter === 'has_open_movements' ? 'filled' : 'outlined'}
              onClick={() => {
                setStatusFilter('has_open_movements');
                setPage(1);
              }}
            />
          </Stack>
        </Stack>

        {/* Loading State */}
        {loading ? (
          <Stack spacing={1.5}>
            {[0, 1, 2, 3].map((row) => (
              <Skeleton key={row} variant="rounded" height={58} sx={{ borderRadius: 2 }} />
            ))}
          </Stack>
        ) : filteredProjects.length === 0 ? (
          <Stack spacing={1.25} alignItems="center" sx={{ py: 5, textAlign: 'center' }}>
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
              <IconChartBar size={24} />
            </Box>
            <Typography variant="subtitle1" fontWeight={700}>
              لا توجد مشاريع مطابقة لخيارات البحث
            </Typography>
            {(search || statusFilter !== 'all') && (
              <Button
                size="small"
                variant="outlined"
                onClick={() => {
                  setSearch('');
                  setStatusFilter('all');
                  setPage(1);
                }}
              >
                إعادة ضبط الفلاتر
              </Button>
            )}
          </Stack>
        ) : (
          <>
            {/* Desktop Table View */}
            {viewMode === 'table' && (
              <TableContainer sx={{ display: { xs: 'none', md: 'block' } }}>
                <Table size="small" aria-label="جدول تقارير المشاريع">
                  <TableHead>
                    <TableRow sx={{ '& th': { fontWeight: 700, color: 'text.secondary', borderBottomWidth: 2, whiteSpace: 'nowrap' } }}>
                      <TableCell>المشروع</TableCell>
                      <TableCell>المهندس المسؤول</TableCell>
                      <TableCell>الحالة</TableCell>
                      <TableCell align="right">وارد</TableCell>
                      <TableCell align="right">صادر</TableCell>
                      <TableCell align="right">الصافي</TableCell>
                      <TableCell align="center">مراجعة</TableCell>
                      <TableCell align="center">التقرير</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {visibleProjects.map((project) => {
                      const net = (project.totalReceived || 0) - (project.totalIssued || 0);
                      return (
                        <TableRow key={project.id} hover sx={{ '& td': { py: 1.5 } }}>
                          <TableCell>
                            <Typography variant="subtitle2" fontWeight={700}>
                              {project.name}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                              {project.code} {project.location ? `· ${project.location}` : ''}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2" color="text.secondary">
                              {project.engineerName || 'بدون مهندس'}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Chip size="small" label={project.status} color={project.status === 'نشط' ? 'success' : 'warning'} />
                          </TableCell>
                          <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', color: 'success.dark', fontWeight: 700, whiteSpace: 'nowrap' }}>
                            {formatMoney(project.totalReceived)}
                          </TableCell>
                          <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', fontWeight: 600, whiteSpace: 'nowrap' }}>
                            {formatMoney(project.totalIssued)}
                          </TableCell>
                          <TableCell
                            align="right"
                            sx={{
                              fontVariantNumeric: 'tabular-nums',
                              fontWeight: 700,
                              whiteSpace: 'nowrap',
                              color: net >= 0 ? 'primary.dark' : 'error.dark'
                            }}
                          >
                            {formatMoney(net)}
                          </TableCell>
                          <TableCell align="center">
                            {project.openMovements > 0 ? (
                              <Chip size="small" label={project.openMovements} color="warning" sx={{ fontWeight: 700 }} />
                            ) : (
                              <Typography variant="caption" color="text.secondary">
                                0
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell align="center">
                            <Stack direction="row" spacing={0.75} justifyContent="center">
                              <Button
                                component={Link}
                                to={`/emg/reports/${project.id}`}
                                size="small"
                                variant="contained"
                                startIcon={<IconChartBar size={15} />}
                                sx={{ whiteSpace: 'nowrap', borderRadius: 1.75 }}
                              >
                                فتح التقرير
                              </Button>
                              <Button
                                component={Link}
                                to={`/emg/projects/${project.id}`}
                                size="small"
                                variant="outlined"
                                sx={{ whiteSpace: 'nowrap', borderRadius: 1.75 }}
                              >
                                المشروع
                              </Button>
                            </Stack>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            )}

            {/* Cards View (Desktop when toggled + Always on Mobile < md) */}
            <Grid container spacing={2} sx={{ display: viewMode === 'cards' ? 'flex' : { xs: 'flex', md: 'none' } }}>
              {visibleProjects.map((project) => {
                const net = (project.totalReceived || 0) - (project.totalIssued || 0);
                return (
                  <Grid key={project.id} size={{ xs: 12, sm: 6, lg: 4 }}>
                    <Card
                      variant="outlined"
                      sx={{
                        height: '100%',
                        borderRadius: 3,
                        transition: 'border-color 150ms ease, box-shadow 150ms ease',
                        '&:hover': { borderColor: 'primary.main', boxShadow: theme.shadows[2] }
                      }}
                    >
                      <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
                        <Stack spacing={1.5}>
                          <Stack direction="row" justifyContent="space-between" alignItems="center">
                            <Box
                              sx={{
                                width: 40,
                                height: 40,
                                display: 'grid',
                                placeItems: 'center',
                                borderRadius: 2,
                                bgcolor: withAlpha(theme.vars.palette.primary.main, 0.12),
                                color: 'primary.main'
                              }}
                            >
                              <IconBuilding size={20} />
                            </Box>
                            <Stack direction="row" spacing={0.75}>
                              <Chip size="small" label={project.status} color={project.status === 'نشط' ? 'success' : 'warning'} />
                              {project.openMovements > 0 && <Chip size="small" label={`${project.openMovements} مراجعة`} color="error" />}
                            </Stack>
                          </Stack>

                          <Box>
                            <Typography variant="h4" fontWeight={700} sx={{ lineHeight: 1.4 }}>
                              {project.name}
                            </Typography>
                            <Typography variant="caption" color="text.secondary">
                              {project.code}
                            </Typography>
                          </Box>

                          <Stack spacing={0.5}>
                            <Stack direction="row" spacing={0.75} alignItems="center">
                              <IconUser size={15} />
                              <Typography variant="caption" color="text.secondary" noWrap>
                                {project.engineerName || 'بدون مهندس'}
                              </Typography>
                            </Stack>
                            {project.location && (
                              <Stack direction="row" spacing={0.75} alignItems="center">
                                <IconMapPin size={15} />
                                <Typography variant="caption" color="text.secondary" noWrap>
                                  {project.location}
                                </Typography>
                              </Stack>
                            )}
                          </Stack>

                          <Divider />

                          <Grid container spacing={1} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                            <Grid size={{ xs: 4 }}>
                              <Typography variant="caption" color="text.secondary" display="block">
                                وارد
                              </Typography>
                              <Typography variant="subtitle2" fontWeight={700} color="success.dark">
                                {formatMoney(project.totalReceived)}
                              </Typography>
                            </Grid>
                            <Grid size={{ xs: 4 }}>
                              <Typography variant="caption" color="text.secondary" display="block">
                                صادر
                              </Typography>
                              <Typography variant="subtitle2" fontWeight={700}>
                                {formatMoney(project.totalIssued)}
                              </Typography>
                            </Grid>
                            <Grid size={{ xs: 4 }}>
                              <Typography variant="caption" color="text.secondary" display="block">
                                الصافي
                              </Typography>
                              <Typography variant="subtitle2" fontWeight={700} color={net >= 0 ? 'primary.dark' : 'error.dark'}>
                                {formatMoney(net)}
                              </Typography>
                            </Grid>
                          </Grid>

                          <Stack direction="row" spacing={1}>
                            <Button
                              fullWidth
                              variant="contained"
                              size="small"
                              onClick={() => navigate(`/emg/reports/${project.id}`)}
                              startIcon={<IconChartBar size={16} />}
                            >
                              فتح التقرير المالي
                            </Button>
                            <Button
                              variant="outlined"
                              size="small"
                              onClick={() => navigate(`/emg/projects/${project.id}`)}
                            >
                              المشروع
                            </Button>
                          </Stack>
                        </Stack>
                      </CardContent>
                    </Card>
                  </Grid>
                );
              })}
            </Grid>

            {/* Pagination Footer */}
            {filteredProjects.length > pageSize && (
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                justifyContent="space-between"
                alignItems="center"
                spacing={2}
                sx={{ mt: 2.5, pt: 2, borderTop: '1px solid', borderColor: 'divider' }}
              >
                <Typography variant="body2" color="text.secondary">
                  عرض {(safePage - 1) * pageSize + 1} – {Math.min(safePage * pageSize, filteredProjects.length)} من {filteredProjects.length} مشروع
                </Typography>
                <Stack direction="row" spacing={2} alignItems="center">
                  <FormControl size="small" sx={{ minWidth: 100 }}>
                    <InputLabel>العدد</InputLabel>
                    <Select
                      value={pageSize}
                      label="العدد"
                      onChange={(e) => {
                        setPageSize(Number(e.target.value));
                        setPage(1);
                      }}
                    >
                      <MenuItem value={10}>10</MenuItem>
                      <MenuItem value={20}>20</MenuItem>
                      <MenuItem value={50}>50</MenuItem>
                    </Select>
                  </FormControl>
                  <Pagination
                    count={pageCount}
                    page={safePage}
                    onChange={(_, value) => setPage(value)}
                    color="primary"
                    shape="rounded"
                    showFirstButton
                    showLastButton
                  />
                </Stack>
              </Stack>
            )}
          </>
        )}
      </MainCard>
    </Box>
  );
}
