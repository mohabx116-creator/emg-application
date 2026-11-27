import { ReactNode, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Breadcrumbs,
  Button,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  FormControl,
  Grid,
  IconButton,
  InputAdornment,
  MenuItem,
  Select,
  Skeleton,
  Stack,
  TablePagination,
  TextField,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconArrowRight,
  IconPlus,
  IconRefresh,
  IconSearch,
  IconX
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { EmgProject } from 'api/emg';
import FinancialSummary from './FinancialSummary';
import type { HistoricalWork } from 'api/historicalWork';
import HistoricalWorkTable from './HistoricalWorkTable';
import { extractSectionFinancialSummary, SectionFinancialSummary } from './sectionFinancials';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import useAuth from 'hooks/useAuth';
import { hasPermission } from './roleAccess';

const moneyFmt = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
export const formatWorkMoney = (value: number) => `${moneyFmt.format(value || 0)} ج.م`;

export const WORK_SECTION_TABS = [
  { key: 'movements', label: 'حركة الموقع اليومية' },
  { key: 'excavation', label: 'أعمال الحفر' },
  { key: 'concrete', label: 'الخرسانات' },
  { key: 'steel', label: 'الحديد' },
  { key: 'finishing', label: 'التشطيبات' }
] as const;

export function WorkStatusChip({ status }: { status?: string | null }) {
  const s = status || 'مسودة';
  const color: 'success' | 'warning' | 'error' | 'default' =
    s === 'معتمد' || s === 'مرحّل' ? 'success' : s === 'مرفوض' ? 'error' : s === 'للمراجعة' || s === 'تحت المراجعة' ? 'warning' : 'default';
  return <Chip size="small" label={s} color={color} sx={{ fontWeight: 700, height: 22 }} />;
}

export function WorkSectionSkeleton() {
  return (
    <Stack spacing={2.5} sx={{ pb: 4 }}>
      <Skeleton variant="text" width={240} height={24} />
      <Skeleton variant="rounded" height={72} sx={{ borderRadius: 3 }} />
      <Grid container spacing={2}>
        {[0, 1, 2, 3].map((i) => (
          <Grid key={i} size={{ xs: 12, sm: 6, lg: 3 }}>
            <Skeleton variant="rounded" height={106} sx={{ borderRadius: 3 }} />
          </Grid>
        ))}
      </Grid>
      <Skeleton variant="rounded" height={380} sx={{ borderRadius: 3 }} />
    </Stack>
  );
}

type WorkSectionShellProps = {
  project: EmgProject;
  activeSection: 'movements' | 'excavation' | 'concrete' | 'steel' | 'finishing';
  title: string;
  subtitle?: string;
  addButtonLabel: string;
  canCreate?: boolean;
  onOpenCreate: () => void;
  onRefresh: () => void;
  loading?: boolean;
  detailWarnings: string[];
  message?: { severity: 'success' | 'warning' | 'error'; text: string } | null;
  onCloseMessage?: () => void;
  totalAmount: number;
  totalCount: number;
  historical: HistoricalWork[];
  approvedCount: number;
  pendingCount: number;
  search: string;
  onSearchChange: (value: string) => void;
  searchPlaceholder?: string;
  statusFilter: string;
  onStatusFilterChange: (value: string) => void;
  availableStatuses: string[];
  filteredCount: number;
  page: number;
  rowsPerPage: number;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  children: ReactNode;
  createModalOpen: boolean;
  createModalTitle: string;
  onCloseCreateModal: () => void;
  createModalContent: ReactNode;
  /** Optional typed section-specific financial summary (from backend section endpoint). Never falls back to project totals. */
  sectionSummary?: SectionFinancialSummary | null;
  /** Allows specialized modals (e.g. New Transaction Modal) to render their own custom header & spacing */
  customModalLayout?: boolean;
};

export default function WorkSectionShell({
  project,
  activeSection,
  title,
  subtitle,
  addButtonLabel,
  canCreate = true,
  onOpenCreate,
  onRefresh,
  loading = false,
  detailWarnings,
  message,
  onCloseMessage,
  totalAmount,
  totalCount,
  historical,
  approvedCount,
  pendingCount,
  search,
  onSearchChange,
  searchPlaceholder = 'ابحث بالوصف أو المورد أو البند...',
  statusFilter,
  onStatusFilterChange,
  availableStatuses,
  filteredCount,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  children,
  createModalOpen,
  createModalTitle,
  onCloseCreateModal,
  createModalContent,
  sectionSummary = null,
  customModalLayout = false
}: WorkSectionShellProps) {
  void totalAmount;
  void approvedCount;
  void pendingCount;
  const theme = useTheme();
  const downSM = useMediaQuery(theme.breakpoints.down('sm'));
  const navigate = useNavigate();
  const { user } = useAuth();
  const { projects, setActiveProjectId } = useActiveProject();
  const canAdvances = hasPermission(user, 'advances.view') || hasPermission(user, 'advances.manage');
  useEffect(() => {
    if (project?.id) setActiveProjectId(project.id);
  }, [project?.id, setActiveProjectId]);

  const searchText = search.trim().toLowerCase();
  const sourceMatches = statusFilter === 'all' ? historical.filter(row =>
    [row.description, row.category, row.party, row.workDate, row.unit, row.currency, row.amount, row.sourceFileName, row.sheet, row.cell]
      .join(' ').toLowerCase().includes(searchText)
  ) : [];
  const count = filteredCount + sourceMatches.length;
  const lastPage = Math.max(0, Math.ceil(count / rowsPerPage) - 1);
  useEffect(() => {
    if (page > lastPage) onPageChange(lastPage);
  }, [lastPage, page, onPageChange]);
  const offset = page * rowsPerPage;
  const sourcePage = sourceMatches.slice(Math.max(0, offset - filteredCount), Math.max(0, offset + rowsPerPage - filteredCount));

  const effectiveSectionSummary = sectionSummary ?? extractSectionFinancialSummary(project, activeSection);

  return (
    <Box sx={{ pb: 5 }}>
      {/* Breadcrumbs */}
      <Breadcrumbs sx={{ mb: 1.5 }}>
        <Typography component={Link} to="/emg/projects" variant="body2" color="text.secondary" sx={{ textDecoration: 'none', fontWeight: 700 }}>
          المشروعات
        </Typography>
        <Typography component={Link} to={`/emg/projects/${project.id}`} variant="body2" color="text.secondary" sx={{ textDecoration: 'none', fontWeight: 700 }}>
          {project.name}
        </Typography>
        <Typography variant="body2" color="primary" sx={{ fontWeight: 700 }}>
          {title}
        </Typography>
      </Breadcrumbs>

      {/* Executive Header + Fast Project Switcher */}
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'stretch', md: 'center' }}
        spacing={2}
        sx={{ mb: 2 }}
      >
        <Box>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            <Typography variant="h2" sx={{ fontWeight: 850 }}>
              {title}
            </Typography>
            <Chip size="small" label={`كود: ${project.code}`} variant="outlined" sx={{ fontWeight: 700 }} />
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {subtitle || `${project.name} — متابعة السجلات وإضافة الحركات الجديدة`}
          </Typography>
        </Box>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', sm: 'center' }}>
          {projects.length > 1 && (
            <FormControl size="small" sx={{ minWidth: { xs: '100%', sm: 210 } }}>
              <Select
                value={String(project.id)}
                aria-label="تبديل المشروع"
                onChange={(e) => {
                  const nextId = Number(e.target.value);
                  if (nextId && nextId !== project.id) {
                    setActiveProjectId(nextId);
                    navigate(`/emg/projects/${nextId}/${activeSection}`);
                  }
                }}
                sx={{ borderRadius: 2, bgcolor: 'background.paper', fontWeight: 700 }}
              >
                {projects.map((item) => (
                  <MenuItem key={item.id} value={String(item.id)}>
                    {item.name} ({item.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          {canCreate && (
            <Button
              variant="contained"
              color="secondary"
              startIcon={<IconPlus size={18} />}
              onClick={onOpenCreate}
              sx={{ borderRadius: 2, fontWeight: 700, px: 2.5, minHeight: 42, boxShadow: '0 8px 20px rgba(94, 53, 177, 0.28)' }}
            >
              {addButtonLabel}
            </Button>
          )}
          <Button
            component={Link}
            to={`/emg/projects/${project.id}`}
            variant="outlined"
            color="inherit"
            endIcon={<IconArrowRight size={17} />}
            sx={{ borderRadius: 2, fontWeight: 700, minHeight: 42 }}
          >
            تفاصيل المشروع
          </Button>
        </Stack>
      </Stack>

      {/* Quick Section Switcher Bar */}
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: 2.5 }}>
        {WORK_SECTION_TABS.map((sec) => {
          const isActive = sec.key === activeSection;
          return (
            <Button
              key={sec.key}
              component={Link}
              to={`/emg/projects/${project.id}/${sec.key}`}
              size="small"
              variant={isActive ? 'contained' : 'outlined'}
              color={isActive ? 'primary' : 'inherit'}
              sx={{
                borderRadius: 2,
                fontWeight: 700,
                borderColor: isActive ? undefined : 'divider',
                bgcolor: isActive ? undefined : 'background.paper'
              }}
            >
              {sec.label}
            </Button>
          );
        })}
        {canAdvances && (
          <Button
            component={Link}
            to={`/emg/projects/${project.id}/advances`}
            size="small"
            variant="outlined"
            color="inherit"
            sx={{ borderRadius: 2, fontWeight: 700, borderColor: 'divider', bgcolor: 'background.paper' }}
          >
            الدفعات تحت الحساب
          </Button>
        )}
      </Stack>

      {message && (
        <Alert severity={message.severity} onClose={onCloseMessage} sx={{ mb: 2.5, borderRadius: 2 }}>
          {message.text}
        </Alert>
      )}

      <Box sx={{ mb: 2.5 }}>
        <FinancialSummary
          totalWorks={effectiveSectionSummary.totalWorks}
          totalPayments={effectiveSectionSummary.totalPayments}
          cashBalance={effectiveSectionSummary.cashBalance}
          cashBalanceStatus={effectiveSectionSummary.cashBalanceStatus}
          unresolved={effectiveSectionSummary.unresolved}
        />
        {project.currencies?.filter(row => row.currency !== 'EGP').map(row => {
          const section = extractSectionFinancialSummary(project, activeSection, row.currency);
          return section.source === 'unavailable' ? null : <FinancialSummary key={row.currency}
            layout="compact" currency={row.currency} totalWorks={section.totalWorks}
            totalPayments={section.totalPayments} cashBalance={section.cashBalance} />;
        })}
      </Box>

      {/* Tier 2: Full-Width Records Table Card */}
      <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden' }}>
        <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'action.hover' }}>
          <Stack
            direction={{ xs: 'column', lg: 'row' }}
            spacing={1.5}
            alignItems={{ xs: 'stretch', lg: 'center' }}
            justifyContent="space-between"
          >
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', sm: 'center' }} flexWrap="wrap" useFlexGap>
              <TextField
                size="small"
                placeholder={searchPlaceholder}
                value={search}
                onChange={(e) => onSearchChange(e.target.value)}
                sx={{ minWidth: { xs: '100%', sm: 260 }, bgcolor: 'background.paper', borderRadius: 1 }}
                InputProps={{
                  startAdornment: (
                    <InputAdornment position="start">
                      <IconSearch size={17} />
                    </InputAdornment>
                  ),
                  endAdornment: search ? (
                    <InputAdornment position="end">
                      <IconButton size="small" onClick={() => onSearchChange('')} aria-label="مسح البحث">
                        <IconX size={14} />
                      </IconButton>
                    </InputAdornment>
                  ) : undefined
                }}
              />

              <Stack direction="row" spacing={0.75} flexWrap="wrap" useFlexGap>
                <Chip
                  clickable
                  size="small"
                  label={`الكل (${totalCount + historical.length})`}
                  color={statusFilter === 'all' ? 'primary' : 'default'}
                  variant={statusFilter === 'all' ? 'filled' : 'outlined'}
                  onClick={() => onStatusFilterChange('all')}
                  sx={{ fontWeight: 700 }}
                />
                {availableStatuses.filter(status => !['للمراجعة', 'تحت المراجعة', 'معتمد', 'بانتظار الاعتماد'].includes(status)).map((status) => (
                  <Chip
                    key={status}
                    clickable
                    size="small"
                    label={status}
                    color={statusFilter === status ? 'secondary' : 'default'}
                    variant={statusFilter === status ? 'filled' : 'outlined'}
                    onClick={() => onStatusFilterChange(status)}
                    sx={{ fontWeight: 700 }}
                  />
                ))}
              </Stack>
            </Stack>

            <Button
              size="small"
              variant="outlined"
              color="inherit"
              startIcon={<IconRefresh size={16} />}
              onClick={onRefresh}
              disabled={loading}
              sx={{ borderRadius: 2, fontWeight: 700, bgcolor: 'background.paper', alignSelf: { xs: 'flex-start', lg: 'center' } }}
            >
              تحديث
            </Button>
          </Stack>
        </Box>

        {detailWarnings.map(warning => <Alert key={warning} severity="warning">{warning}</Alert>)}
        {loading && count === 0 && <Skeleton variant="rounded" height={48} />}
        {(offset < filteredCount || (count === 0 && !loading && detailWarnings.length === 0)) && children}
        <HistoricalWorkTable rows={sourcePage} />

        {count > 0 && (
          <TablePagination
            component="div"
            count={count}
            page={Math.min(page, lastPage)}
            onPageChange={(_, p) => onPageChange(p)}
            rowsPerPage={rowsPerPage}
            onRowsPerPageChange={(e) => {
              onRowsPerPageChange(parseInt(e.target.value, 10));
              onPageChange(0);
            }}
            rowsPerPageOptions={[10, 25, 50, 100]}
            labelRowsPerPage="عدد الصفوف:"
            labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`}
            showFirstButton
            showLastButton
            sx={{ borderTop: 1, borderColor: 'divider' }}
          />
        )}
      </MainCard>

      {/* Creation Modal Dialog */}
      <Dialog
        open={createModalOpen}
        onClose={onCloseCreateModal}
        fullWidth
        fullScreen={downSM}
        maxWidth={customModalLayout ? 'sm' : 'md'}
        sx={{ zIndex: (t) => t.zIndex.modal + 2 }}
        PaperProps={{
          sx: {
            borderRadius: downSM ? 0 : 3.5,
            maxWidth: !downSM && customModalLayout ? 720 : undefined,
            overflow: 'hidden'
          }
        }}
      >
        {customModalLayout ? (
          <DialogContent sx={{ p: 0 }}>{createModalContent}</DialogContent>
        ) : (
          <>
            <DialogTitle sx={{ px: 3, py: 2, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Box>
                <Typography variant="h3" sx={{ fontWeight: 800 }}>
                  {createModalTitle}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {project.name} · كود {project.code}
                </Typography>
              </Box>
              <IconButton onClick={onCloseCreateModal} aria-label="إغلاق" size="small">
                <IconX size={20} />
              </IconButton>
            </DialogTitle>
            <DialogContent dividers sx={{ p: 3 }}>
              {createModalContent}
            </DialogContent>
          </>
        )}
      </Dialog>
    </Box>
  );
}
