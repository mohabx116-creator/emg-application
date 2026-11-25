import { cloneElement, useState, ReactElement, CSSProperties } from 'react';
import { Link as RouterLink } from 'react-router-dom';

// material-ui
import { useColorScheme, useTheme } from '@mui/material/styles';
import useScrollTrigger from '@mui/material/useScrollTrigger';
import MuiAppBar from '@mui/material/AppBar';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Toolbar from '@mui/material/Toolbar';
import Box from '@mui/material/Box';

// project imports
import { ThemeDirection, ThemeMode } from 'config';
import useConfig from 'hooks/useConfig';
import emgLogo3d from 'assets/images/emg-logo-3d.png';

// assets
import { IconLanguage, IconLogin } from '@tabler/icons-react';
import MenuIcon from '@mui/icons-material/Menu';

// elevation scroll
interface ElevationScrollProps {
  children: ReactElement<{ elevation?: number; style?: CSSProperties }>;
  window?: Window | Node;
}

function ElevationScroll({ children, window }: ElevationScrollProps) {
  const theme = useTheme();
  const { colorScheme } = useColorScheme();

  const trigger = useScrollTrigger({
    disableHysteresis: true,
    threshold: 0,
    target: window!
  });

  return cloneElement(children, {
    elevation: trigger ? 1 : 0,
    style: {
      backgroundColor:
        colorScheme === ThemeMode.DARK && trigger
          ? theme.vars.palette.dark[800]
          : trigger
            ? theme.vars.palette.background.paper
            : 'transparent',
      color: theme.vars.palette.text.dark
    }
  });
}

// ==============================|| EMG LANDING APP BAR ||============================== //

export default function AppBar({ ...others }) {
  const [drawerToggle, setDrawerToggle] = useState<boolean>(false);
  const {
    state: { i18n },
    setField
  } = useConfig();

  const isAr = i18n === 'ar';

  const toggleLanguage = () => {
    const nextLang = isAr ? 'en' : 'ar';
    setField('i18n', nextLang);
    setField('themeDirection', nextLang === 'ar' ? ThemeDirection.RTL : ThemeDirection.LTR);
  };

  const drawerToggler = (open: boolean) => (event: any) => {
    if (event.type === 'keydown' && (event.key === 'Tab' || event.key === 'Shift')) {
      return;
    }
    setDrawerToggle(open);
  };

  return (
    <ElevationScroll {...others}>
      <MuiAppBar>
        <Container maxWidth="lg">
          <Toolbar sx={{ py: 2, px: '0 !important', justifyContent: { xs: 'space-between', md: 'flex-end' } }}>
            <Stack
              component={RouterLink}
              to="/"
              direction="row"
              spacing={1.25}
              alignItems="center"
              sx={{
                flexGrow: { xs: 1, md: 0 },
                display: { xs: 'flex', md: 'none' },
                textDecoration: 'none'
              }}
            >
              <Box
                component="img"
                src={emgLogo3d}
                alt="EMG"
                sx={{ width: 38, height: 38, objectFit: 'contain' }}
              />
              <Box sx={{ direction: 'ltr', textAlign: 'left' }}>
                <Typography
                  sx={{
                    fontSize: '1.15rem',
                    lineHeight: 1,
                    fontWeight: 900,
                    color: 'text.primary',
                    letterSpacing: '-0.02em'
                  }}
                >
                  EMG
                </Typography>
                <Typography
                  sx={{
                    fontSize: '0.58rem',
                    fontWeight: 700,
                    letterSpacing: '0.14em',
                    textTransform: 'uppercase',
                    color: 'text.secondary',
                    mt: 0.2
                  }}
                >
                  Construction ERP
                </Typography>
              </Box>
            </Stack>

            <Stack direction="row" alignItems="center" sx={{ gap: { xs: 1.5, md: 2 }, display: { xs: 'none', sm: 'flex' } }}>
              <Button
                color="inherit"
                size="small"
                variant="outlined"
                onClick={toggleLanguage}
                startIcon={<IconLanguage size={16} />}
                sx={{ borderRadius: 2, fontWeight: 600, borderColor: 'divider' }}
              >
                {isAr ? 'English' : 'العربية'}
              </Button>
              <Button
                component={RouterLink}
                to="/login"
                disableElevation
                variant="contained"
                color="secondary"
                startIcon={<IconLogin size={18} />}
                sx={{ borderRadius: 2, fontWeight: 700, px: 2.5 }}
              >
                {isAr ? 'تسجيل الدخول' : 'Log In'}
              </Button>
            </Stack>

            <Box sx={{ display: { xs: 'block', sm: 'none' } }}>
              <IconButton color="inherit" onClick={drawerToggler(true)} size="large" aria-label="menu">
                <MenuIcon />
              </IconButton>
              <Drawer anchor="top" open={drawerToggle} onClose={drawerToggler(false)}>
                {drawerToggle && (
                  <Box sx={{ width: 'auto', p: 1 }} role="presentation" onClick={drawerToggler(false)} onKeyDown={drawerToggler(false)}>
                    <List>
                      <ListItemButton component={RouterLink} to="/login">
                        <ListItemIcon>
                          <IconLogin size={20} />
                        </ListItemIcon>
                        <ListItemText primary={isAr ? 'تسجيل الدخول' : 'Log In'} />
                      </ListItemButton>
                      <ListItemButton onClick={toggleLanguage}>
                        <ListItemIcon>
                          <IconLanguage size={20} />
                        </ListItemIcon>
                        <ListItemText primary={isAr ? 'English' : 'العربية'} />
                      </ListItemButton>
                    </List>
                  </Box>
                )}
              </Drawer>
            </Box>
          </Toolbar>
        </Container>
      </MuiAppBar>
    </ElevationScroll>
  );
}
