import { useEffect } from 'react';

// material-ui
import { useTheme } from '@mui/material/styles';
import Container from '@mui/material/Container';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// project imports
import HeaderSection from './HeaderSection';
import Customization from 'layout/Customization';
import AppBar from 'ui-component/extended/AppBar';
import useConfig from 'hooks/useConfig';

const prefetchRoutes = () => {
  import('views/pages/authentication/authentication2/Login2');
};

// =============================|| EMG LANDING MAIN ||============================= //

export default function Landing() {
  const theme = useTheme();
  const {
    state: { i18n }
  } = useConfig();
  const isAr = i18n === 'ar';

  useEffect(() => {
    prefetchRoutes();
  }, []);

  return (
    <Box
      id="home"
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        overflowX: 'hidden',
        overflowY: 'clip',
        background: `linear-gradient(180deg, ${theme.vars.palette.background.paper} 0%, ${theme.vars.palette.grey[100]} 100%)`,
        ...theme.applyStyles('dark', {
          background: theme.vars.palette.background.default
        })
      }}
    >
      <AppBar />

      <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}>
        <HeaderSection />
      </Box>

      {/* Compact EMG Footer */}
      <Box
        component="footer"
        sx={{
          py: 2.5,
          borderTop: '1px solid',
          borderColor: 'divider'
        }}
      >
        <Container maxWidth="lg">
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            justifyContent="space-between"
            alignItems="center"
            spacing={1}
          >
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600 }}>
              EMG Construction ERP
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {isAr ? '© جميع الحقوق محفوظة — منظومة EMG لإدارة المشروعات' : '© All rights reserved — EMG Project Management System'}
            </Typography>
          </Stack>
        </Container>
      </Box>

      <Customization />
    </Box>
  );
}
