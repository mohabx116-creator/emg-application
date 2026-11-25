import { useMemo } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { alpha, useTheme } from '@mui/material/styles';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Button from '@mui/material/Button';
import useMediaQuery from '@mui/material/useMediaQuery';
import { IconArrowRight } from '@tabler/icons-react';

const fallbackRoutes: Array<[string, string]> = [
  ['/emg/projects/', '/emg/projects'],
  ['/emg/site-movements/', '/emg/projects'],
  ['/emg/suppliers/', '/emg/suppliers'],
  ['/emg/reports/', '/emg/reports'],
  ['/emg/advances/', '/emg/advances'],
  ['/emg/imports/', '/emg/imports']
];

function getFallbackRoute(pathname: string) {
  return fallbackRoutes.find(([prefix]) => pathname.startsWith(prefix))?.[1] ?? '/emg/dashboard';
}

export default function GlobalBackButton() {
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const compact = useMediaQuery(theme.breakpoints.down('sm'));

  const fallback = useMemo(() => getFallbackRoute(location.pathname), [location.pathname]);
  const isDashboard = location.pathname === '/emg' || location.pathname === '/emg/dashboard';

  if (isDashboard) return null;

  const goBack = () => {
    const historyIndex = window.history.state?.idx;
    if (typeof historyIndex === 'number' && historyIndex > 0) {
      navigate(-1);
      return;
    }
    navigate(fallback, { replace: true });
  };

  const button = compact ? (
    <Tooltip title="رجوع">
      <IconButton
        aria-label="رجوع"
        onClick={goBack}
        size="small"
        sx={{
          width: 38,
          height: 38,
          color: 'primary.main',
          bgcolor: alpha(theme.palette.primary.main, 0.08),
          border: `1px solid ${alpha(theme.palette.primary.main, 0.16)}`,
          '&:hover': { bgcolor: alpha(theme.palette.primary.main, 0.16), transform: 'translateX(2px)' },
          transition: 'all .2s ease-in-out'
        }}
      >
        <IconArrowRight size={20} stroke={1.8} />
      </IconButton>
    </Tooltip>
  ) : (
    <Button
      aria-label="رجوع"
      onClick={goBack}
      variant="outlined"
      size="small"
      startIcon={<IconArrowRight size={18} stroke={1.8} />}
      sx={{
        minWidth: 92,
        borderRadius: 2,
        px: 1.25,
        gap: 0.5,
        fontWeight: 600,
        whiteSpace: 'nowrap'
      }}
    >
      رجوع
    </Button>
  );

  return <>{button}</>;
}
