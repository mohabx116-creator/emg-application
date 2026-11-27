import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

// material-ui
import { Theme, keyframes } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// icons
import { IconLanguage } from '@tabler/icons-react';

// project imports
import AuthCardWrapper from '../AuthCardWrapper';
import AuthLogin from '../jwt/AuthLogin';
import AuthFooter from 'ui-component/cards/AuthFooter';
import useConfig from 'hooks/useConfig';
import { ThemeDirection } from 'config';
import emgLogo3d from 'assets/images/emg-logo-3d.png';
import emgLoginBg1 from 'assets/images/auth/emg-login-bg-1.jpg';
import emgLoginBg2 from 'assets/images/auth/emg-login-bg-2.jpg';

const kenBurnsZoom = keyframes`
  0% {
    transform: scale(1.0) translate3d(0, 0, 0);
  }
  50% {
    transform: scale(1.08) translate3d(-1.5%, -1%, 0);
  }
  100% {
    transform: scale(1.0) translate3d(0, 0, 0);
  }
`;

// ================================|| EMG — PREMIUM DIRECT LOGIN ||================================ //

export default function Login() {
  const downMD = useMediaQuery((theme: Theme) => theme.breakpoints.down('md'));
  const {
    state: { i18n },
    setField
  } = useConfig();

  const isAr = i18n === 'ar';
  const [activeSlide, setActiveSlide] = useState(0);

  useEffect(() => {
    document.title = 'EMG – تسجيل الدخول';
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActiveSlide((prev) => (prev + 1) % 2);
    }, 6500);
    return () => window.clearInterval(timer);
  }, []);

  const toggleLanguage = () => {
    const nextLang = isAr ? 'en' : 'ar';
    setField('i18n', nextLang);
    setField('themeDirection', nextLang === 'ar' ? ThemeDirection.RTL : ThemeDirection.LTR);
  };

  return (
    <Box
      sx={{
        position: 'relative',
        minHeight: '100vh',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: '#0b2042',
        color: 'common.white'
      }}
    >
      {/* Full-Viewport Branded Construction Background */}
      {[emgLoginBg1, emgLoginBg2].map((bgSrc, idx) => {
        const isVisible = idx === activeSlide % 2;
        return (
          <Box
            key={`full-bg-${idx}`}
            aria-hidden="true"
            sx={{
              position: 'absolute',
              inset: 0,
              backgroundImage: `url(${bgSrc})`,
              backgroundSize: 'cover',
              backgroundPosition: 'center',
              opacity: isVisible ? 1 : 0,
              transition: 'opacity 1.2s ease-in-out',
              animation: `${kenBurnsZoom} 24s ease-in-out infinite`,
              '@media (prefers-reduced-motion: reduce)': {
                animation: 'none'
              },
              pointerEvents: 'none'
            }}
          />
        );
      })}

      {/* Twilight Architectural Scrim */}
      <Box
        aria-hidden="true"
        sx={{
          position: 'absolute',
          inset: 0,
          background: 'linear-gradient(155deg, rgba(10, 29, 60, 0.62) 0%, rgba(16, 48, 96, 0.48) 48%, rgba(20, 18, 64, 0.78) 100%)',
          pointerEvents: 'none'
        }}
      />

      {/* Top Bar */}
      <Container maxWidth="xl" sx={{ position: 'relative', zIndex: 1, pt: { xs: 2, sm: 3 }, maxWidth: { xl: 1440 } }}>
        <Stack direction="row" justifyContent="space-between" alignItems="center">
          <Button
            component={Link}
            to="/"
            size="small"
            variant="text"
            sx={{
              color: 'rgba(255,255,255,0.88)',
              fontWeight: 700,
              '&:hover': { color: '#ffffff', bgcolor: 'rgba(255,255,255,0.1)' }
            }}
          >
            {isAr ? '← الصفحة الرئيسية' : '← Home'}
          </Button>

          <Button
            size="small"
            variant="outlined"
            onClick={toggleLanguage}
            startIcon={<IconLanguage size={16} />}
            aria-label={isAr ? 'Switch to English' : 'التبديل إلى العربية'}
            sx={{
              borderRadius: 2.5,
              fontWeight: 600,
              color: '#ffffff',
              borderColor: 'rgba(255,255,255,0.36)',
              bgcolor: 'rgba(255,255,255,0.12)',
              backdropFilter: 'blur(12px)',
              WebkitBackdropFilter: 'blur(12px)',
              '&:hover': {
                bgcolor: 'rgba(255,255,255,0.22)',
                borderColor: 'rgba(255,255,255,0.62)'
              }
            }}
          >
            {isAr ? 'English' : 'العربية'}
          </Button>
        </Stack>
      </Container>

      {/* Main Content: Logo on LEFT (enlarged), Login Form on RIGHT */}
      <Container
        maxWidth="xl"
        sx={{
          position: 'relative',
          zIndex: 1,
          flexGrow: 1,
          display: 'flex',
          alignItems: 'center',
          py: { xs: 3, md: 5 },
          maxWidth: { xl: 1360 }
        }}
      >
        <Grid
          container
          spacing={{ xs: 3, md: 6 }}
          alignItems="center"
          justifyContent="space-between"
          sx={{
            width: '100%',
            flexDirection: { xs: 'row', md: isAr ? 'row-reverse' : 'row' }
          }}
        >
          {/* LEFT COLUMN: Enlarged Company Logo occupying the previous card area */}
          <Grid
            size={{ xs: 12, md: 6 }}
            sx={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center'
            }}
          >
            <Stack
              component={Link}
              to="/"
              spacing={{ xs: 1.75, md: 2.75 }}
              alignItems="center"
              aria-label="EMG Construction ERP"
              sx={{ textDecoration: 'none', textAlign: 'center', px: 2, py: { xs: 1, md: 3 } }}
            >
              <Box
                sx={{
                  position: 'relative',
                  display: 'grid',
                  placeItems: 'center',
                  '&::before': {
                    content: '""',
                    position: 'absolute',
                    inset: { xs: -16, md: -34 },
                    borderRadius: '50%',
                    background: 'radial-gradient(circle, rgba(144, 202, 249, 0.34) 0%, rgba(144, 202, 249, 0) 70%)',
                    pointerEvents: 'none'
                  }
                }}
              >
                <Box
                  component="img"
                  src={emgLogo3d}
                  alt="EMG Construction"
                  sx={{
                    position: 'relative',
                    width: { xs: 112, sm: 148, md: 220, lg: 256 },
                    height: { xs: 112, sm: 148, md: 220, lg: 256 },
                    objectFit: 'contain',
                    filter: 'drop-shadow(0 18px 36px rgba(4, 14, 34, 0.62))'
                  }}
                />
              </Box>
              <Box sx={{ direction: 'ltr', textAlign: 'center' }}>
                <Typography
                  sx={{
                    fontSize: { xs: '2.15rem', sm: '2.8rem', md: '3.75rem', lg: '4.25rem' },
                    lineHeight: 0.98,
                    fontWeight: 900,
                    color: '#ffffff',
                    letterSpacing: '-0.02em',
                    textShadow: '0 8px 24px rgba(4, 14, 34, 0.58)'
                  }}
                >
                  EMG
                </Typography>
                <Typography
                  sx={{
                    fontSize: { xs: '0.78rem', sm: '0.92rem', md: '1.1rem' },
                    fontWeight: 700,
                    letterSpacing: '0.26em',
                    textTransform: 'uppercase',
                    color: 'rgba(255,255,255,0.92)',
                    mt: 1,
                    textShadow: '0 2px 10px rgba(4, 14, 34, 0.45)'
                  }}
                >
                  Construction ERP
                </Typography>
              </Box>
              <Box
                sx={{
                  width: { xs: 56, md: 88 },
                  height: 3.5,
                  borderRadius: 999,
                  background: 'linear-gradient(90deg, rgba(144,202,249,0.2) 0%, rgba(255,255,255,0.9) 50%, rgba(144,202,249,0.2) 100%)'
                }}
              />
            </Stack>
          </Grid>

          {/* RIGHT COLUMN: Login Form */}
          <Grid
            size={{ xs: 12, md: 6 }}
            sx={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: { xs: 'center', md: 'flex-end' }
            }}
          >
            <AuthCardWrapper
              border
              sx={{
                width: '100%',
                maxWidth: 468,
                borderRadius: 4,
                boxShadow:
                  '0 28px 64px rgba(4, 14, 34, 0.54), inset 0 1px 1px rgba(255, 255, 255, 0.52), inset 0 -1px 1px rgba(255, 255, 255, 0.14)',
                background: 'linear-gradient(145deg, rgba(15, 32, 66, 0.68) 0%, rgba(12, 24, 52, 0.56) 100%)',
                bgcolor: 'transparent',
                backdropFilter: 'blur(18px) saturate(165%)',
                WebkitBackdropFilter: 'blur(18px) saturate(165%)',
                borderColor: 'rgba(255, 255, 255, 0.3)',
                '& .MuiOutlinedInput-root': {
                  bgcolor: 'rgba(8, 20, 44, 0.52)',
                  backdropFilter: 'blur(8px)',
                  color: '#ffffff',
                  borderRadius: 2.5,
                  overflow: 'hidden',
                  '& input': {
                    backgroundColor: 'transparent !important',
                    color: '#ffffff !important',
                    WebkitTextFillColor: '#ffffff !important',
                    caretColor: '#ffffff',
                    fontSize: '0.98rem'
                  },
                  '& input:-webkit-autofill, & input:-webkit-autofill:hover, & input:-webkit-autofill:focus, & input:-webkit-autofill:active': {
                    WebkitBoxShadow: '0 0 0 1000px rgba(14, 34, 68, 0.72) inset !important',
                    WebkitTextFillColor: '#ffffff !important',
                    caretColor: '#ffffff',
                    transition: 'background-color 5000s ease-in-out 0s'
                  },
                  '& .MuiOutlinedInput-notchedOutline': {
                    borderColor: 'rgba(255, 255, 255, 0.34)'
                  },
                  '&:hover .MuiOutlinedInput-notchedOutline': {
                    borderColor: 'rgba(255, 255, 255, 0.65)'
                  },
                  '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
                    borderColor: '#90caf9'
                  }
                },
                '& .MuiInputLabel-root': {
                  color: 'rgba(255, 255, 255, 0.88)',
                  '&.Mui-focused': {
                    color: '#ffffff'
                  }
                },
                '& .MuiInputAdornment-root .MuiIconButton-root': {
                  color: 'rgba(255, 255, 255, 0.9)'
                },
                '& .MuiFormControlLabel-label': {
                  color: 'rgba(255, 255, 255, 0.94)'
                },
                '& .MuiCheckbox-root': {
                  color: 'rgba(255, 255, 255, 0.85)',
                  '&.Mui-checked': {
                    color: '#90caf9'
                  }
                },
                '& a': {
                  color: '#bbdefb'
                },
                '& .MuiDivider-root': {
                  borderColor: 'rgba(255, 255, 255, 0.22)'
                },
                '& button[type="submit"]': {
                  minHeight: 48,
                  fontSize: '1rem',
                  fontWeight: 700,
                  borderRadius: 2.5,
                  boxShadow: '0 10px 24px rgba(13, 71, 161, 0.45), inset 0 1px 0 rgba(255, 255, 255, 0.38)'
                }
              }}
            >
              <Stack spacing={3}>
                <Stack spacing={1.25} sx={{ alignItems: 'center', textAlign: 'center' }}>
                  <Chip
                    label={isAr ? 'منظومة إدارة المشروعات الهندسية' : 'Construction Management ERP'}
                    size="small"
                    variant="outlined"
                    sx={{
                      fontWeight: 700,
                      color: '#ffffff',
                      bgcolor: 'rgba(255, 255, 255, 0.14)',
                      borderColor: 'rgba(255, 255, 255, 0.34)'
                    }}
                  />
                  <Typography
                    variant={downMD ? 'h3' : 'h2'}
                    sx={{ color: '#ffffff', fontWeight: 800, textShadow: '0 2px 10px rgba(0,0,0,0.28)' }}
                  >
                    {isAr ? 'تسجيل الدخول' : 'Sign In'}
                  </Typography>
                  <Typography variant="body2" sx={{ color: 'rgba(255,255,255,0.78)' }}>
                    {isAr ? 'أدخل بيانات حسابك للوصول المباشر إلى مشروعاتك' : 'Sign in to access your assigned projects'}
                  </Typography>
                </Stack>

                <Box sx={{ width: 1 }}>
                  <AuthLogin />
                </Box>
              </Stack>
            </AuthCardWrapper>
          </Grid>
        </Grid>
      </Container>

      {/* Footer */}
      <Container
        maxWidth="xl"
        sx={{
          position: 'relative',
          zIndex: 1,
          pb: 2.5,
          maxWidth: { xl: 1360 },
          '& .MuiTypography-root': {
            color: 'rgba(255,255,255,0.82)'
          }
        }}
      >
        <AuthFooter />
      </Container>
    </Box>
  );
}
