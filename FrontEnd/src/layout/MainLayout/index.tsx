import { useEffect, useMemo } from 'react';
import { Outlet } from 'react-router-dom';

// material-ui
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import AppBar from '@mui/material/AppBar';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Box from '@mui/material/Box';

// project imports
import Footer from './Footer';
import Header from './Header';
import Sidebar from './Sidebar';
import HorizontalBar from './HorizontalBar';
import MainContentStyled from './MainContentStyled';
import Loader from 'ui-component/Loader';
import Breadcrumbs from 'ui-component/extended/Breadcrumbs';
import AttendancePrompt from 'ui-component/extended/AttendancePrompt';
import PwaStatusBar from 'ui-component/emg/PwaStatusBar';
import MobileBottomNav from 'ui-component/emg/MobileBottomNav';

import { MenuOrientation } from 'config';
import useConfig from 'hooks/useConfig';
import { useDocumentTitle } from 'hooks/useDocumentTitle';
import { ActiveProjectProvider } from 'contexts/ActiveProjectContext';
import { handlerDrawerOpen, useGetMenuMaster } from 'api/menu';

// ==============================|| MAIN LAYOUT ||============================== //

export default function MainLayout() {
  useDocumentTitle();
  const theme = useTheme();
  const downMD = useMediaQuery(theme.breakpoints.down('md'));

  const {
    state: { borderRadius, container, miniDrawer, menuOrientation }
  } = useConfig();
  const { menuMaster, menuMasterLoading } = useGetMenuMaster();
  const drawerOpen = menuMaster?.isDashboardDrawerOpened;

  useEffect(() => {
    handlerDrawerOpen(!miniDrawer);
  }, [miniDrawer]);

  useEffect(() => {
    downMD && handlerDrawerOpen(false);
  }, [downMD]);

  const isHorizontal = menuOrientation === MenuOrientation.HORIZONTAL && !downMD;

  // horizontal menu-list bar : drawer
  const menu = useMemo(() => (isHorizontal ? <HorizontalBar /> : <Sidebar />), [isHorizontal]);

  if (menuMasterLoading) return <Loader />;

  return (
    <ActiveProjectProvider>
      <Box sx={{ display: 'flex', maxWidth: '100vw', overflowX: 'clip' }}>
        {/* header */}
        <AppBar enableColorOnDark position="fixed" color="inherit" elevation={0} sx={{ bgcolor: 'background.default' }}>
          <Toolbar sx={{ p: isHorizontal ? 1.25 : { xs: 1, sm: 1.5, md: 2 }, minHeight: { xs: 60, md: 72 } }}>
            <Header />
          </Toolbar>
        </AppBar>

        {/* menu / drawer */}
        {menu}

        {/* main content */}
        <MainContentStyled {...{ borderRadius, menuOrientation, open: drawerOpen }}>
          <Container
            maxWidth={container ? 'lg' : false}
            sx={{
              ...(!container && { px: { xs: 0 } }),
              minHeight: 'calc(100vh - 128px)',
              display: 'flex',
              flexDirection: 'column',
              pb: { xs: 9, md: 0 }
            }}
          >
            <PwaStatusBar />
            <Breadcrumbs />
            <AttendancePrompt />
            <Outlet />
            <Footer />
          </Container>
        </MainContentStyled>

        {/* Native-style mobile bottom navigation */}
        <MobileBottomNav />
      </Box>
    </ActiveProjectProvider>
  );
}
