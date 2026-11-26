import { useLocation, useNavigate } from 'react-router-dom';
import { Box, Paper, Stack, Typography } from '@mui/material';
import { IconBuilding, IconClipboardList, IconClock, IconDashboard, IconPlus, IconTruckDelivery } from '@tabler/icons-react';
import useAuth from 'hooks/useAuth';
import { useActiveProject } from 'contexts/ActiveProjectContext';
import { hasPermission } from 'Modules/EMG/roleAccess';

export default function MobileBottomNav() {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { activeProjectId } = useActiveProject();

  const canMovements = hasPermission(user, 'site-movements.view') || hasPermission(user, 'projects.view');
  const canCreateMovement = hasPermission(user, 'site-movements.create');
  const canAttendance = hasPermission(user, 'attendance.view') || hasPermission(user, 'attendance.manage');
  const canSuppliers = hasPermission(user, 'suppliers.view') || hasPermission(user, 'suppliers.manage');

  const movementsUrl = activeProjectId ? `/emg/projects/${activeProjectId}/movements` : '/emg/site-movements';
  const addMovementUrl = activeProjectId ? `/emg/projects/${activeProjectId}/movements?quickAdd=1` : '/emg/site-movements';

  const isDashboard = pathname === '/emg' || pathname === '/emg/dashboard';
  const isMovements = pathname.includes('/movements') || pathname.startsWith('/emg/site-movements');
  const isProjects = pathname === '/emg/projects' || (pathname.startsWith('/emg/projects/') && !isMovements);
  const isAttendance = pathname.startsWith('/emg/attendance');
  const isSuppliers = pathname.startsWith('/emg/suppliers');

  const NavBtn = ({
    label,
    icon,
    active,
    onClick
  }: {
    label: string;
    icon: React.ReactNode;
    active: boolean;
    onClick: () => void;
  }) => (
    <Box
      component="button"
      type="button"
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      sx={{
        flex: 1,
        minHeight: 54,
        border: 'none',
        background: 'transparent',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 0.35,
        cursor: 'pointer',
        color: active ? 'primary.main' : 'text.secondary',
        p: 0.5,
        WebkitTapHighlightColor: 'transparent',
        '&:active': { transform: 'scale(0.95)' }
      }}
    >
      {icon}
      <Typography
        variant="caption"
        sx={{
          fontSize: '0.68rem',
          fontWeight: active ? 800 : 600,
          lineHeight: 1.1,
          whiteSpace: 'nowrap'
        }}
      >
        {label}
      </Typography>
    </Box>
  );

  return (
    <Paper
      elevation={8}
      component="nav"
      aria-label="التنقل السريع للهاتف"
      sx={{
        display: { xs: 'block', md: 'none' },
        position: 'fixed',
        bottom: 0,
        insetInlineStart: 0,
        insetInlineEnd: 0,
        zIndex: (theme) => theme.zIndex.appBar + 2,
        borderTop: '1px solid',
        borderColor: 'divider',
        bgcolor: 'background.paper',
        pb: 'env(safe-area-inset-bottom, 0px)',
        px: 0.5
      }}
    >
      <Stack direction="row" alignItems="center" justifyContent="space-around">
        <NavBtn
          label="الرئيسية"
          icon={<IconDashboard size={21} stroke={isDashboard ? 2.2 : 1.75} />}
          active={isDashboard}
          onClick={() => navigate('/emg/dashboard')}
        />

        {canMovements && (
          <NavBtn
            label="حركات الموقع"
            icon={<IconClipboardList size={21} stroke={isMovements ? 2.2 : 1.75} />}
            active={isMovements}
            onClick={() => navigate(movementsUrl)}
          />
        )}

        {canCreateMovement && (
          <Box sx={{ display: 'flex', justifyContent: 'center', px: 0.75, mt: -2.25 }}>
            <Box
              component="button"
              type="button"
              aria-label="إضافة حركة موقع"
              onClick={() => navigate(addMovementUrl)}
              sx={{
                width: 54,
                height: 54,
                borderRadius: '50%',
                border: '3px solid',
                borderColor: 'background.paper',
                background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                color: '#fff',
                display: 'grid',
                placeItems: 'center',
                cursor: 'pointer',
                boxShadow: '0 8px 20px rgba(13, 71, 161, 0.38)',
                '&:active': { transform: 'scale(0.94)' }
              }}
            >
              <IconPlus size={26} stroke={2.4} />
            </Box>
          </Box>
        )}

        <NavBtn
          label="المشروعات"
          icon={<IconBuilding size={21} stroke={isProjects ? 2.2 : 1.75} />}
          active={isProjects}
          onClick={() => navigate('/emg/projects')}
        />

        {canAttendance ? (
          <NavBtn
            label="الحضور"
            icon={<IconClock size={21} stroke={isAttendance ? 2.2 : 1.75} />}
            active={isAttendance}
            onClick={() => navigate('/emg/attendance')}
          />
        ) : canSuppliers ? (
          <NavBtn
            label="الموردون"
            icon={<IconTruckDelivery size={21} stroke={isSuppliers ? 2.2 : 1.75} />}
            active={isSuppliers}
            onClick={() => navigate('/emg/suppliers')}
          />
        ) : null}
      </Stack>
    </Paper>
  );
}
