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
import { IconBuildingSkyscraper, IconChecklist, IconLanguage, IconShieldCheck } from '@tabler/icons-react';

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

// ================================|| EMG V1 - FULL-BLEED LIQUID GLASS LOGIN ||================================ //

export default function Login() {
  const downMD = useMediaQuery((theme: Theme) => theme.breakpoints.down('md'));
  const {
    state: { i18n },
    setField
  } = useConfig();

  const isAr = i18n === 'ar';
  const [activeSlide, setActiveSlide] = useState(0);

  const toggleLanguage = () => {
    const nextLang = isAr ? 'en' : 'ar';
    setField('i18n', nextLang);
    setField('themeDirection', nextLang === 'ar' ? ThemeDirection.RTL : ThemeDirection.LTR);
  };

  const slides = isAr
    ? [
      { headline: 'إدارة المشروعات بوضوح ودقة' },
      { headline: 'متابعة مالية وميدانية متكاملة' },
      { headline: 'بيئة عمل موحدة لفرق التنفيذ' }
    ]
    : [
      { headline: 'Clear, Unified Project Control' },
      { headline: 'Integrated Site & Financial Tracking' },
      { headline: 'One Workspace for Every Team' }
    ];

  const features = isAr
    ? [
      {
        icon: <IconBuildingSkyscraper size={20} stroke={1.75} />,
        title: 'المشاريع وحركة الموقع'
      },
      {
        icon: <IconChecklist size={20} stroke={1.75} />,
        title: 'الموردين والاعتمادات المالية'
      },
      {
        icon: <IconShieldCheck size={20} stroke={1.75} />,
        title: 'التقارير الختامية والصلاحيات'
      }
    ]
    : [
      {
        icon: <IconBuildingSkyscraper size={20} stroke={1.75} />,
        title: 'Projects & Site Operations'
      },
      {
        icon: <IconChecklist size={20} stroke={1.75} />,
        title: 'Suppliers & Approvals'
      },
      {
        icon: <IconShieldCheck size={20} stroke={1.75} />,
        title: 'Reports & Role Access'
      }
    ];

  useEffect(() => {
    const timer = window.setInterval(() => {
      setActiveSlide((prev) => (prev + 1) % 3);
    }, 5500);
    return () => window.clearInterval(timer);
  }, []);

  const currentSlide = slides[activeSlide];

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
      {/* Full-Viewport Dynamic Crossfading Construction Background */}
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
              animation: `${kenBurnsZoom} 22s ease-in-out infinite`,
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
          background: 'linear-gradient(155deg, rgba(10, 29, 60, 0.56) 0%, rgba(16, 48, 96, 0.44) 48%, rgba(20, 18, 64, 0.74) 100%)',
          pointerEvents: 'none'
        }}
      />

      {/* Top Navigation Bar */}
      <Container maxWidth="xl" sx={{ position: 'relative', zIndex: 1, pt: { xs: 2, sm: 3 }, maxWidth: { xl: 1480 } }}>
        <Stack
          direction="row"
          justifyContent={{ xs: 'space-between', lg: 'flex-end' }}
          alignItems="center"
        >
          <Stack
            component={Link}
            to="/login"
            direction="row"
            spacing={1.5}
            alignItems="center"
            aria-label="EMG Construction"
            sx={{ textDecoration: 'none', display: { xs: 'flex', lg: 'none' } }}
          >
            <Box
              component="img"
              src={emgLogo3d}
              alt="EMG Construction"
              sx={{
                width: { xs: 42, sm: 52 },
                height: { xs: 42, sm: 52 },
                objectFit: 'contain',
                filter: 'drop-shadow(0 6px 12px rgba(0,0,0,0.4))'
              }}
            />
            <Box sx={{ direction: 'ltr', textAlign: 'left' }}>
              <Typography
                sx={{
                  fontSize: { xs: '1.35rem', sm: '1.65rem' },
                  lineHeight: 1,
                  fontWeight: 900,
                  color: '#ffffff',
                  letterSpacing: '-0.03em'
                }}
              >
                EMG
              </Typography>
              <Typography
                sx={{
                  fontSize: { xs: '0.62rem', sm: '0.72rem' },
                  fontWeight: 700,
                  letterSpacing: '0.15em',
                  textTransform: 'uppercase',
                  color: 'rgba(255,255,255,0.86)',
                  mt: 0.25
                }}
              >
                Construction ERP
              </Typography>
            </Box>
          </Stack>

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

      {/* Main Content */}
      <Container
        maxWidth="xl"
        sx={{
          position: 'relative',
          zIndex: 1,
          flexGrow: 1,
          display: 'flex',
          alignItems: 'center',
          py: { xs: 3, md: 4 },
          maxWidth: { xl: 1480 }
        }}
      >
        <Grid
          container
          columns={{ xs: 12, md: 12, lg: 24 }}
          spacing={{ xs: 3, md: 5, lg: 3.5, xl: 4.5 }}
          alignItems="center"
          justifyContent="center"
          sx={{ width: '100%' }}
        >
          {/* Desktop Side Brand Lockup Column */}
          <Grid
            size={{ lg: 6 }}
            sx={{
              display: { xs: 'none', lg: 'flex' },
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center'
            }}
          >
            <Stack
              component={Link}
              to="/login"
              spacing={2}
              alignItems="center"
              aria-label="EMG Construction"
              sx={{
                textDecoration: 'none',
                textAlign: 'center',
                px: 1.5,
                py: 2
              }}
            >
              <Box
                sx={{
                  position: 'relative',
                  display: 'grid',
                  placeItems: 'center',
                  '&::before': {
                    content: '""',
                    position: 'absolute',
                    inset: -18,
                    borderRadius: '50%',
                    background: 'radial-gradient(circle, rgba(144, 202, 249, 0.28) 0%, rgba(144, 202, 249, 0) 70%)',
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
                    width: { lg: 112, xl: 128 },
                    height: { lg: 112, xl: 128 },
                    objectFit: 'contain',
                    filter: 'drop-shadow(0 12px 24px rgba(4, 14, 34, 0.55))'
                  }}
                />
              </Box>
              <Box sx={{ direction: 'ltr', textAlign: 'center' }}>
                <Typography
                  sx={{
                    fontSize: { lg: '2.75rem', xl: '3.2rem' },
                    lineHeight: 1,
                    fontWeight: 900,
                    color: '#ffffff',
                    letterSpacing: '-0.02em',
                    textShadow: '0 6px 20px rgba(4, 14, 34, 0.55)'
                  }}
                >
                  EMG
                </Typography>
                <Typography
                  sx={{
                    fontSize: { lg: '0.82rem', xl: '0.92rem' },
                    fontWeight: 700,
                    letterSpacing: '0.24em',
                    textTransform: 'uppercase',
                    color: 'rgba(255,255,255,0.9)',
                    mt: 0.9,
                    textShadow: '0 2px 10px rgba(4, 14, 34, 0.45)'
                  }}
                >
                  Construction ERP
                </Typography>
              </Box>
              <Box
                sx={{
                  width: 56,
                  height: 3,
                  borderRadius: 999,
                  background: 'linear-gradient(90deg, rgba(144,202,249,0.2) 0%, rgba(255,255,255,0.85) 50%, rgba(144,202,249,0.2) 100%)'
                }}
              />
            </Stack>
          </Grid>

          {/* Liquid Glass Login Card Column */}
          <Grid size={{ xs: 12, md: 6, lg: 10 }} sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <AuthCardWrapper
              border
              sx={{
                width: '100%',
                maxWidth: 460,
                borderRadius: 4,
                boxShadow:
                  '0 24px 56px rgba(4, 14, 34, 0.48), inset 0 1px 1px rgba(255, 255, 255, 0.52), inset 0 -1px 1px rgba(255, 255, 255, 0.14)',
                background: 'linear-gradient(145deg, rgba(255, 255, 255, 0.12) 0%, rgba(255, 255, 255, 0.04) 100%)',
                bgcolor: 'transparent',
                backdropFilter: 'blur(16px) saturate(165%)',
                WebkitBackdropFilter: 'blur(16px) saturate(165%)',
                borderColor: 'rgba(255, 255, 255, 0.34)',
                '& .MuiOutlinedInput-root': {
                  bgcolor: 'rgba(10, 26, 54, 0.26)',
                  backdropFilter: 'blur(8px)',
                  color: '#ffffff',
                  borderRadius: 2.5,
                  overflow: 'hidden',
                  '& input': {
                    backgroundColor: 'transparent !important',
                    color: '#ffffff !important',
                    WebkitTextFillColor: '#ffffff !important',
                    caretColor: '#ffffff'
                  },
                  '& input:-webkit-autofill, & input:-webkit-autofill:hover, & input:-webkit-autofill:focus, & input:-webkit-autofill:active': {
                    WebkitBoxShadow: '0 0 0 1000px rgba(14, 34, 68, 0.58) inset !important',
                    WebkitTextFillColor: '#ffffff !important',
                    caretColor: '#ffffff',
                    transition: 'background-color 5000s ease-in-out 0s'
                  },
                  '& .MuiOutlinedInput-notchedOutline': {
                    borderColor: 'rgba(255, 255, 255, 0.34)'
                  },
                  '&:hover .MuiOutlinedInput-notchedOutline': {
                    borderColor: 'rgba(255, 255, 255, 0.62)'
                  },
                  '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
                    borderColor: 'rgba(255, 255, 255, 0.90)'
                  }
                },
                '& .MuiInputLabel-root': {
                  color: 'rgba(255, 255, 255, 0.84)',
                  '&.Mui-focused': {
                    color: '#ffffff'
                  }
                },
                '& .MuiInputAdornment-root .MuiIconButton-root': {
                  color: 'rgba(255, 255, 255, 0.88)'
                },
                '& .MuiFormControlLabel-label': {
                  color: 'rgba(255, 255, 255, 0.92)'
                },
                '& .MuiCheckbox-root': {
                  color: 'rgba(255, 255, 255, 0.82)',
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
                  boxShadow: '0 10px 24px rgba(13, 71, 161, 0.45), inset 0 1px 0 rgba(255, 255, 255, 0.38)'
                }
              }}
            >
              <Stack spacing={2.5}>
                <Stack spacing={1} sx={{ alignItems: 'center', textAlign: 'center' }}>
                  <Chip
                    label={isAr ? 'منظومة إدارة المشروعات' : 'Project Management System'}
                    size="small"
                    variant="outlined"
                    sx={{
                      fontWeight: 600,
                      color: '#ffffff',
                      bgcolor: 'rgba(255, 255, 255, 0.15)',
                      borderColor: 'rgba(255, 255, 255, 0.36)'
                    }}
                  />
                  <Typography
                    variant={downMD ? 'h3' : 'h2'}
                    sx={{ color: '#ffffff', fontWeight: 700, textShadow: '0 2px 10px rgba(0,0,0,0.28)' }}
                  >
                    {isAr ? 'تسجيل الدخول' : 'Sign In'}
                  </Typography>
                </Stack>

                <Box sx={{ width: 1 }}>
                  <AuthLogin />
                </Box>
              </Stack>
            </AuthCardWrapper>

            {/* Mobile Carousel Indicator Dots */}
            <Stack direction="row" spacing={1} alignItems="center" justifyContent="center" sx={{ display: { xs: 'flex', md: 'none' }, mt: 2.5 }}>
              {slides.map((_, index) => (
                <Box
                  key={`mobile-dot-${index}`}
                  component="button"
                  type="button"
                  aria-label={isAr ? `الشريحة ${index + 1}` : `Slide ${index + 1}`}
                  onClick={() => setActiveSlide(index)}
                  sx={{
                    width: activeSlide === index ? 24 : 8,
                    height: 8,
                    borderRadius: 999,
                    border: 'none',
                    p: 0,
                    cursor: 'pointer',
                    bgcolor: activeSlide === index ? 'common.white' : 'rgba(255,255,255,0.42)',
                    transition: 'all 250ms ease'
                  }}
                />
              ))}
            </Stack>
          </Grid>

          {/* Desktop Brand Showcase Column (Minimal Frosted Glass Highlights) */}
          <Grid size={{ xs: 12, md: 6, lg: 8 }} sx={{ display: { xs: 'none', md: 'block' } }}>
            <Stack spacing={2.5} sx={{ maxWidth: 380, mx: 'auto' }}>
              <Typography
                variant="h1"
                sx={{
                  color: '#ffffff',
                  fontWeight: 800,
                  fontSize: { md: '1.85rem', lg: '2.1rem' },
                  lineHeight: 1.35,
                  textShadow: '0 4px 16px rgba(0,0,0,0.35)'
                }}
              >
                {currentSlide.headline}
              </Typography>

              <Stack spacing={1.25}>
                {features.map((item, index) => {
                  const isActive = index === activeSlide;
                  return (
                    <Stack
                      key={item.title}
                      direction="row"
                      spacing={1.5}
                      alignItems="center"
                      onClick={() => setActiveSlide(index)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          setActiveSlide(index);
                        }
                      }}
                      sx={{
                        px: 2,
                        py: 1.5,
                        borderRadius: 3,
                        cursor: 'pointer',
                        backdropFilter: 'blur(14px) saturate(150%)',
                        WebkitBackdropFilter: 'blur(14px) saturate(150%)',
                        bgcolor: isActive ? 'rgba(255, 255, 255, 0.16)' : 'rgba(10, 25, 50, 0.34)',
                        border: '1px solid',
                        borderColor: isActive ? 'rgba(255, 255, 255, 0.44)' : 'rgba(255, 255, 255, 0.18)',
                        boxShadow: isActive ? '0 10px 24px rgba(4, 14, 34, 0.28), inset 0 1px 0 rgba(255,255,255,0.35)' : 'none',
                        transform: isActive ? 'translateY(-1px)' : 'none',
                        transition: 'all 250ms ease',
                        '&:hover': {
                          bgcolor: 'rgba(255, 255, 255, 0.18)',
                          borderColor: 'rgba(255, 255, 255, 0.4)'
                        }
                      }}
                    >
                      <Box
                        sx={{
                          width: 36,
                          height: 36,
                          borderRadius: 2,
                          display: 'grid',
                          placeItems: 'center',
                          flexShrink: 0,
                          bgcolor: isActive ? 'primary.main' : 'rgba(255, 255, 255, 0.16)',
                          color: 'common.white',
                          transition: 'background-color 250ms ease'
                        }}
                      >
                        {item.icon}
                      </Box>
                      <Typography variant="subtitle1" sx={{ color: '#ffffff', fontWeight: 700 }}>
                        {item.title}
                      </Typography>
                    </Stack>
                  );
                })}
              </Stack>

              {/* Desktop Carousel Indicator Dots */}
              <Stack direction="row" spacing={1} alignItems="center" justifyContent="flex-start" sx={{ pt: 0.25 }}>
                {slides.map((_, index) => (
                  <Box
                    key={`desktop-dot-${index}`}
                    component="button"
                    type="button"
                    aria-label={isAr ? `الشريحة ${index + 1}` : `Slide ${index + 1}`}
                    onClick={() => setActiveSlide(index)}
                    sx={{
                      width: activeSlide === index ? 28 : 9,
                      height: 9,
                      borderRadius: 999,
                      border: 'none',
                      p: 0,
                      cursor: 'pointer',
                      bgcolor: activeSlide === index ? 'common.white' : 'rgba(255,255,255,0.38)',
                      transition: 'all 250ms ease'
                    }}
                  />
                ))}
              </Stack>
            </Stack>
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
          maxWidth: { xl: 1480 },
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
