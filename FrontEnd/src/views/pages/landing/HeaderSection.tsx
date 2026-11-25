import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';

// material-ui
import { keyframes, useColorScheme, useTheme } from '@mui/material/styles';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// third party
import { motion } from 'framer-motion';

// project imports
import AnimateButton from 'ui-component/extended/AnimateButton';
import { ThemeDirection, ThemeMode } from 'config';
import useConfig from 'hooks/useConfig';
import useAuth from 'hooks/useAuth';
import axios from 'utils/axios';

// assets
import {
  IconActivityHeartbeat,
  IconBuildingSkyscraper,
  IconChartBar,
  IconChecklist,
  IconLogin,
  IconShieldCheck,
  IconTruckDelivery
} from '@tabler/icons-react';
import emgLogo3d from 'assets/images/emg-logo-3d.png';

const floatSlow = keyframes`
  0%, 100% {
    transform: translateY(0px);
  }
  50% {
    transform: translateY(-14px);
  }
`;

const floatMedium = keyframes`
  0%, 100% {
    transform: translateY(0px);
  }
  50% {
    transform: translateY(-11px);
  }
`;

const floatReverse = keyframes`
  0%, 100% {
    transform: translateY(0px);
  }
  50% {
    transform: translateY(11px);
  }
`;

const pulseGlow = keyframes`
  0%, 100% {
    opacity: 0.48;
    transform: scale(0.95);
  }
  50% {
    opacity: 0.85;
    transform: scale(1.06);
  }
`;

const spinOrbit = keyframes`
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
`;

const shimmerText = keyframes`
  0% {
    background-position: 0% 50%;
  }
  50% {
    background-position: 100% 50%;
  }
  100% {
    background-position: 0% 50%;
  }
`;

// ==============================|| 3D ISOMETRIC ENGINEERING SHOWCASE ||============================== //

function EmgHeroIsometric3D({
  isAr,
  isDark,
  liveProjectCount
}: {
  isAr: boolean;
  isDark: boolean;
  liveProjectCount: number | null;
}) {
  const theme = useTheme();
  const isRtl = theme.direction === ThemeDirection.RTL;

  const boardBg = isDark ? 'rgba(21, 30, 50, 0.94)' : 'rgba(255, 255, 255, 0.96)';
  const boardBorder = isDark ? 'rgba(144, 202, 249, 0.22)' : 'rgba(94, 53, 177, 0.14)';
  const cardSurface = isDark ? 'rgba(30, 42, 68, 0.85)' : '#f8fafc';
  const gridLineColor = isDark ? 'rgba(144, 202, 249, 0.08)' : 'rgba(94, 53, 177, 0.08)';

  const barHeights = [44, 72, 58, 92, 66, 84, 52];

  return (
    <Box
      sx={{
        position: 'relative',
        width: '100%',
        height: { md: 470, lg: 540 },
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        perspective: '1400px',
        userSelect: 'none'
      }}
    >
      {/* Subtle Isometric Architectural Grid Backdrop */}
      <Box
        aria-hidden="true"
        sx={{
          position: 'absolute',
          width: { md: 520, lg: 640 },
          height: { md: 420, lg: 500 },
          borderRadius: 6,
          backgroundImage: `linear-gradient(${gridLineColor} 1px, transparent 1px), linear-gradient(90deg, ${gridLineColor} 1px, transparent 1px)`,
          backgroundSize: '36px 36px',
          transform: isRtl ? 'rotateX(56deg) rotateZ(34deg)' : 'rotateX(56deg) rotateZ(-34deg)',
          maskImage: 'radial-gradient(ellipse 68% 68% at 50% 50%, black 35%, transparent 100%)',
          WebkitMaskImage: 'radial-gradient(ellipse 68% 68% at 50% 50%, black 35%, transparent 100%)',
          pointerEvents: 'none'
        }}
      />

      {/* Ambient Blue / Purple Radial Glow */}
      <Box
        aria-hidden="true"
        sx={{
          position: 'absolute',
          width: { md: 360, lg: 440 },
          height: { md: 360, lg: 440 },
          borderRadius: '50%',
          background:
            'radial-gradient(circle, rgba(94, 53, 177, 0.22) 0%, rgba(30, 136, 229, 0.16) 45%, rgba(30, 136, 229, 0) 72%)',
          filter: 'blur(26px)',
          pointerEvents: 'none'
        }}
      />

      {/* Tilted 3D Isometric Engineering Dashboard Board */}
      <motion.div
        initial={{ opacity: 0, scale: 0.9, y: 40 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 120, damping: 24, delay: 0.15 }}
        style={{ position: 'relative', zIndex: 2 }}
      >
        <Box
          sx={{
            width: { md: 430, lg: 520 },
            borderRadius: 4,
            p: 2.5,
            bgcolor: boardBg,
            border: '1px solid',
            borderColor: boardBorder,
            transform: isRtl
              ? 'rotateX(46deg) rotateY(-12deg) rotateZ(28deg)'
              : 'rotateX(46deg) rotateY(12deg) rotateZ(-28deg)',
            transformStyle: 'preserve-3d',
            boxShadow: isDark
              ? '-24px 36px 64px rgba(0, 0, 0, 0.58), 0 12px 0 rgba(30, 58, 108, 0.7), 0 13px 0 rgba(144, 202, 249, 0.18)'
              : '-24px 36px 64px rgba(33, 55, 110, 0.18), 0 12px 0 rgba(215, 226, 245, 0.95), 0 13px 0 rgba(94, 53, 177, 0.12)'
          }}
        >
          {/* Board Top Bar */}
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Stack direction="row" spacing={1} alignItems="center">
              <Box sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: '#ef4444' }} />
              <Box sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: '#f59e0b' }} />
              <Box sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: '#10b981' }} />
              <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.secondary', px: 1 }}>
                {isAr ? 'لوحة المشاريع الهندسية — EMG' : 'EMG Engineering Control'}
              </Typography>
            </Stack>
            <Chip
              size="small"
              label={isAr ? 'مباشر' : 'LIVE'}
              sx={{
                height: 20,
                fontSize: '0.65rem',
                fontWeight: 800,
                bgcolor: 'rgba(16, 185, 129, 0.15)',
                color: '#10b981'
              }}
            />
          </Stack>

          {/* Top Metric Tiles Row */}
          <Grid container spacing={1.5} sx={{ mb: 1.75 }}>
            <Grid size={6}>
              <Box
                sx={{
                  p: 1.75,
                  borderRadius: 2.5,
                  background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                  color: '#fff',
                  boxShadow: '0 8px 18px rgba(30, 136, 229, 0.28)'
                }}
              >
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.82)', fontWeight: 600 }}>
                  {isAr ? 'الموقف المالي للمشروعات' : 'Financial Overview'}
                </Typography>
                <Typography variant="h5" sx={{ color: '#fff', fontWeight: 800, mt: 0.5 }}>
                  {isAr ? 'مُحدّث لحظياً' : 'Live Synced'}
                </Typography>
                <Box sx={{ mt: 1, height: 5, borderRadius: 99, bgcolor: 'rgba(255,255,255,0.24)', overflow: 'hidden' }}>
                  <Box sx={{ width: '78%', height: '100%', bgcolor: '#90caf9', borderRadius: 99 }} />
                </Box>
              </Box>
            </Grid>
            <Grid size={6}>
              <Box
                sx={{
                  p: 1.75,
                  borderRadius: 2.5,
                  background: 'linear-gradient(135deg, #311b92 0%, #5e35b1 100%)',
                  color: '#fff',
                  boxShadow: '0 8px 18px rgba(94, 53, 177, 0.28)'
                }}
              >
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.82)', fontWeight: 600 }}>
                  {isAr ? 'متابعة التنفيذ الميداني' : 'Field Execution'}
                </Typography>
                <Typography variant="h5" sx={{ color: '#fff', fontWeight: 800, mt: 0.5 }}>
                  {isAr ? 'رقابة متكاملة' : 'Full Control'}
                </Typography>
                <Box sx={{ mt: 1, height: 5, borderRadius: 99, bgcolor: 'rgba(255,255,255,0.24)', overflow: 'hidden' }}>
                  <Box sx={{ width: '86%', height: '100%', bgcolor: '#ce93d8', borderRadius: 99 }} />
                </Box>
              </Box>
            </Grid>
          </Grid>

          {/* Bottom Split: Site Execution Bar Chart + Engineering Phases */}
          <Grid container spacing={1.5}>
            <Grid size={7}>
              <Box
                sx={{
                  p: 1.75,
                  borderRadius: 2.5,
                  bgcolor: cardSurface,
                  border: '1px solid',
                  borderColor: boardBorder,
                  height: 148,
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between'
                }}
              >
                <Stack direction="row" justifyContent="space-between" alignItems="center">
                  <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.primary' }}>
                    {isAr ? 'حركة المواقع والتوريدات' : 'Site & Supply Volume'}
                  </Typography>
                  <IconChartBar size={15} color="#5e35b1" />
                </Stack>
                <Stack direction="row" spacing={1} alignItems="flex-end" justifyContent="space-between" sx={{ height: 96, px: 0.5 }}>
                  {barHeights.map((h, i) => (
                    <Stack key={i} spacing={0.5} alignItems="center" sx={{ flex: 1, height: '100%', justifyContent: 'flex-end' }}>
                      <Box
                        sx={{
                          width: '100%',
                          maxWidth: 16,
                          height: `${h}%`,
                          borderRadius: '6px 6px 2px 2px',
                          background:
                            i % 2 === 0
                              ? 'linear-gradient(180deg, #7e57c2 0%, #5e35b1 100%)'
                              : 'linear-gradient(180deg, #42a5f5 0%, #1e88e5 100%)'
                        }}
                      />
                    </Stack>
                  ))}
                </Stack>
              </Box>
            </Grid>
            <Grid size={5}>
              <Box
                sx={{
                  p: 1.75,
                  borderRadius: 2.5,
                  bgcolor: cardSurface,
                  border: '1px solid',
                  borderColor: boardBorder,
                  height: 148,
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between'
                }}
              >
                <Typography variant="caption" sx={{ fontWeight: 700, color: 'text.primary' }}>
                  {isAr ? 'بنود التنفيذ' : 'Work Phases'}
                </Typography>
                <Stack spacing={1.1}>
                  {[
                    { label: isAr ? 'الخرسانة' : 'Concrete', pct: 92, color: '#1e88e5' },
                    { label: isAr ? 'الحديد' : 'Steel', pct: 84, color: '#5e35b1' },
                    { label: isAr ? 'التشطيبات' : 'Finishing', pct: 68, color: '#d97706' }
                  ].map((row) => (
                    <Box key={row.label}>
                      <Stack direction="row" justifyContent="space-between" sx={{ mb: 0.35 }}>
                        <Typography sx={{ fontSize: '0.68rem', fontWeight: 600, color: 'text.secondary' }}>{row.label}</Typography>
                        <Typography sx={{ fontSize: '0.68rem', fontWeight: 800, color: 'text.primary' }}>{row.pct}%</Typography>
                      </Stack>
                      <Box sx={{ height: 5, borderRadius: 99, bgcolor: isDark ? 'rgba(255,255,255,0.08)' : 'rgba(15,23,42,0.08)' }}>
                        <Box sx={{ width: `${row.pct}%`, height: '100%', borderRadius: 99, bgcolor: row.color }} />
                      </Box>
                    </Box>
                  ))}
                </Stack>
              </Box>
            </Grid>
          </Grid>
        </Box>
      </motion.div>

      {/* FLOATING 3D CARD TOP: Live Telemetry Glass Pill */}
      <Box
        sx={{
          position: 'absolute',
          top: { md: 28, lg: 24 },
          right: isRtl ? { md: 36, lg: 44 } : { md: 24, lg: 36 },
          zIndex: 6,
          animation: `${floatSlow} 5.4s ease-in-out infinite`
        }}
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.7 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ type: 'spring', stiffness: 140, damping: 20, delay: 0.25 }}
        >
          <Box
            sx={{
              px: 2,
              py: 1.5,
              borderRadius: 3.5,
              display: 'flex',
              alignItems: 'center',
              gap: 1.5,
              background: isDark
                ? 'linear-gradient(145deg, rgba(30, 58, 108, 0.92) 0%, rgba(16, 28, 52, 0.94) 100%)'
                : 'linear-gradient(145deg, rgba(255, 255, 255, 0.96) 0%, rgba(236, 244, 255, 0.92) 100%)',
              border: '1.5px solid',
              borderColor: isDark ? 'rgba(144, 202, 249, 0.35)' : 'rgba(30, 136, 229, 0.22)',
              backdropFilter: 'blur(16px)',
              boxShadow: isDark
                ? '0 20px 40px rgba(0, 0, 0, 0.5), 0 6px 0 rgba(21, 101, 192, 0.45)'
                : '0 20px 40px rgba(13, 71, 161, 0.18), 0 6px 0 rgba(197, 218, 246, 0.9)',
              transform: isRtl ? 'rotate(-5deg)' : 'rotate(5deg)'
            }}
          >
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2.5,
                background: 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                display: 'grid',
                placeItems: 'center',
                color: '#fff',
                boxShadow: '0 8px 16px rgba(30, 136, 229, 0.35)'
              }}
            >
              <IconActivityHeartbeat size={22} />
            </Box>
            <Box>
              <Stack direction="row" spacing={0.75} alignItems="center">
                <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: '#10b981' }} />
                <Typography variant="subtitle2" sx={{ fontWeight: 800, color: 'text.primary', lineHeight: 1.2 }}>
                  {isAr ? 'مزامنة مباشرة' : 'Real-Time Sync'}
                </Typography>
              </Stack>
              <Typography variant="caption" sx={{ color: 'text.secondary', fontWeight: 600 }}>
                {isAr ? 'الموقع · المشتريات · المالية' : 'Site · Supply · Finance'}
              </Typography>
            </Box>
          </Box>
        </motion.div>
      </Box>

      {/* FLOATING 3D CARD 1: Berry Purple Gradient Active Projects Slab (Real count when logged in, honest label when anonymous) */}
      <Box
        sx={{
          position: 'absolute',
          top: { md: 135, lg: 145 },
          left: isRtl ? { md: -8, lg: -18 } : { md: -10, lg: -22 },
          zIndex: 5,
          animation: `${floatMedium} 6.2s ease-in-out infinite`
        }}
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.7 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ type: 'spring', stiffness: 140, damping: 22, delay: 0.35 }}
        >
          <Box
            sx={{
              width: { md: 184, lg: 208 },
              p: 1.75,
              borderRadius: 3,
              background: 'linear-gradient(135deg, #4527a0 0%, #7e57c2 100%)',
              color: '#fff',
              transform: isRtl ? 'rotateX(22deg) rotateZ(12deg)' : 'rotateX(22deg) rotateZ(-12deg)',
              boxShadow: '0 18px 34px rgba(69, 39, 160, 0.38), 0 7px 0 #311b92'
            }}
          >
            <Stack direction="row" spacing={1.25} alignItems="center">
              <Box
                sx={{
                  width: 38,
                  height: 38,
                  borderRadius: 2,
                  bgcolor: 'rgba(255,255,255,0.18)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0
                }}
              >
                <IconBuildingSkyscraper size={20} color="#fff" />
              </Box>
              <Box>
                <Typography variant="h4" sx={{ color: '#fff', fontWeight: 800, lineHeight: 1.15 }}>
                  {liveProjectCount !== null ? liveProjectCount : isAr ? 'مشاريع EMG' : 'EMG Sites'}
                </Typography>
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.88)', fontWeight: 600 }}>
                  {liveProjectCount !== null
                    ? isAr
                      ? 'مشروع مسجّل بالمنظومة'
                      : 'Registered Projects'
                    : isAr
                      ? 'متابعة هندسية متكاملة'
                      : 'Unified Site Control'}
                </Typography>
              </Box>
            </Stack>
          </Box>
        </motion.div>
      </Box>

      {/* FLOATING 3D CARD 2: Warm Amber/Coral Site Approvals Slab */}
      <Box
        sx={{
          position: 'absolute',
          bottom: { md: 24, lg: 28 },
          left: isRtl ? { md: 60, lg: 90 } : undefined,
          right: isRtl ? undefined : { md: 48, lg: 72 },
          zIndex: 5,
          animation: `${floatReverse} 6.8s ease-in-out infinite`
        }}
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.7 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ type: 'spring', stiffness: 140, damping: 22, delay: 0.45 }}
        >
          <Box
            sx={{
              width: { md: 196, lg: 218 },
              p: 1.75,
              borderRadius: 3,
              background: 'linear-gradient(135deg, #f59e0b 0%, #ea580c 100%)',
              color: '#fff',
              transform: isRtl ? 'rotateX(20deg) rotateZ(-8deg)' : 'rotateX(20deg) rotateZ(8deg)',
              boxShadow: '0 18px 34px rgba(234, 88, 12, 0.34), 0 7px 0 #c2410c'
            }}
          >
            <Stack direction="row" spacing={1.25} alignItems="center">
              <Box
                sx={{
                  width: 38,
                  height: 38,
                  borderRadius: 2,
                  bgcolor: 'rgba(255,255,255,0.2)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0
                }}
              >
                <IconChecklist size={20} color="#fff" />
              </Box>
              <Box>
                <Typography variant="subtitle1" sx={{ color: '#fff', fontWeight: 800, lineHeight: 1.2 }}>
                  {isAr ? 'اعتماد فني ومالي' : '2-Stage Approval'}
                </Typography>
                <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.92)', fontWeight: 600 }}>
                  {isAr ? 'توريدات · دفعات · كشوف' : 'Supplies · Advances'}
                </Typography>
              </Box>
            </Stack>
          </Box>
        </motion.div>
      </Box>
    </Box>
  );
}

// ==============================|| EMG LANDING - HEADER SECTION ||============================== //

export default function HeaderSection() {
  const { colorScheme } = useColorScheme();
  const {
    state: { i18n }
  } = useConfig();
  const { isLoggedIn } = useAuth();

  const isAr = i18n === 'ar';
  const isDark = colorScheme === ThemeMode.DARK;
  const [liveProjectCount, setLiveProjectCount] = useState<number | null>(null);

  useEffect(() => {
    if (!isLoggedIn) {
      setLiveProjectCount(null);
      return;
    }
    let active = true;
    axios
      .get('/api/emg/projects')
      .then((res) => {
        if (!active) return;
        const rows = Array.isArray(res.data) ? res.data : Array.isArray(res.data?.items) ? res.data.items : null;
        if (rows) setLiveProjectCount(rows.length);
      })
      .catch(() => {
        // Silent fallback when user lacks projects.view permission or offline
      });
    return () => {
      active = false;
    };
  }, [isLoggedIn]);

  const capabilities = isAr
    ? [
        { icon: <IconBuildingSkyscraper size={16} />, label: 'إدارة المواقع' },
        { icon: <IconTruckDelivery size={16} />, label: 'الموردين والتوريدات' },
        { icon: <IconShieldCheck size={16} />, label: 'التقارير والاعتمادات' }
      ]
    : [
        { icon: <IconBuildingSkyscraper size={16} />, label: 'Site Operations' },
        { icon: <IconTruckDelivery size={16} />, label: 'Suppliers & Delivery' },
        { icon: <IconShieldCheck size={16} />, label: 'Reports & Approvals' }
      ];

  return (
    <Container
      maxWidth="lg"
      sx={{
        minHeight: 'calc(100vh - 64px)',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        pt: { xs: 11, md: 9 },
        pb: { xs: 6, md: 6 }
      }}
    >
      <Grid container spacing={{ xs: 5, md: 4 }} sx={{ justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
        {/* Premium Animated Brand & CTA Hero Column */}
        <Grid size={{ xs: 12, md: 5 }}>
          <Stack spacing={3.25} sx={{ alignItems: { xs: 'center', md: 'flex-start' }, textAlign: { xs: 'center', md: 'start' } }}>
            {/* Premium Floating 3D EMG Brand Showcase */}
            <motion.div
              initial={{ opacity: 0, translateY: 50 }}
              animate={{ opacity: 1, translateY: 0 }}
              transition={{ type: 'spring', stiffness: 140, damping: 26 }}
            >
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2.75}
                alignItems="center"
                sx={{ mb: 1 }}
              >
                {/* Floating Orbiting 3D Logo Medallion */}
                <Box
                  sx={{
                    position: 'relative',
                    width: { xs: 112, md: 124 },
                    height: { xs: 112, md: 124 },
                    display: 'grid',
                    placeItems: 'center',
                    flexShrink: 0,
                    animation: `${floatSlow} 4.8s ease-in-out infinite`
                  }}
                >
                  {/* Soft Radial Aura */}
                  <Box
                    aria-hidden="true"
                    sx={{
                      position: 'absolute',
                      inset: -14,
                      borderRadius: '50%',
                      background:
                        'radial-gradient(circle, rgba(30, 136, 229, 0.32) 0%, rgba(94, 53, 177, 0.22) 52%, transparent 74%)',
                      filter: 'blur(16px)',
                      animation: `${pulseGlow} 4.2s ease-in-out infinite`
                    }}
                  />
                  {/* Rotating Architectural Orbit Ring */}
                  <Box
                    aria-hidden="true"
                    sx={{
                      position: 'absolute',
                      inset: -5,
                      borderRadius: '34px',
                      border: '1.5px dashed',
                      borderColor: isDark ? 'rgba(144, 202, 249, 0.42)' : 'rgba(94, 53, 177, 0.28)',
                      animation: `${spinOrbit} 22s linear infinite`
                    }}
                  />
                  {/* Glass & Silver 3D Pedestal */}
                  <Box
                    sx={{
                      position: 'relative',
                      width: '100%',
                      height: '100%',
                      borderRadius: '30px',
                      display: 'grid',
                      placeItems: 'center',
                      background: isDark
                        ? 'linear-gradient(145deg, rgba(30, 58, 108, 0.92) 0%, rgba(15, 23, 42, 0.96) 100%)'
                        : 'linear-gradient(145deg, #ffffff 0%, #eef5ff 100%)',
                      border: '1.5px solid',
                      borderColor: isDark ? 'rgba(144, 202, 249, 0.34)' : 'rgba(30, 136, 229, 0.2)',
                      boxShadow: isDark
                        ? '0 22px 44px rgba(0, 0, 0, 0.55), 0 6px 0 rgba(21, 101, 192, 0.4), inset 0 1px 1px rgba(255,255,255,0.28)'
                        : '0 22px 44px rgba(13, 71, 161, 0.2), 0 6px 0 rgba(203, 221, 248, 0.9), inset 0 1px 1px rgba(255,255,255,0.95)'
                    }}
                  >
                    <Box
                      component="img"
                      src={emgLogo3d}
                      alt="EMG Construction ERP"
                      sx={{
                        width: { xs: 84, md: 94 },
                        height: { xs: 84, md: 94 },
                        objectFit: 'contain',
                        filter: 'drop-shadow(0 10px 16px rgba(13, 71, 161, 0.3))'
                      }}
                    />
                  </Box>
                </Box>

                {/* Shimmering Architectural Brand Title */}
                <Box sx={{ direction: 'ltr', textAlign: { xs: 'center', sm: 'left' } }}>
                  <Typography
                    variant="h1"
                    sx={{
                      fontSize: { xs: '3rem', sm: '3.6rem', lg: '4.1rem' },
                      lineHeight: 0.95,
                      fontWeight: 900,
                      letterSpacing: '-0.03em',
                      background: isDark
                        ? 'linear-gradient(120deg, #ffffff 0%, #90caf9 38%, #ce93d8 72%, #ffffff 100%)'
                        : 'linear-gradient(120deg, #0d47a1 0%, #1e88e5 38%, #5e35b1 72%, #0d47a1 100%)',
                      backgroundSize: '220% auto',
                      WebkitBackgroundClip: 'text',
                      WebkitTextFillColor: 'transparent',
                      animation: `${shimmerText} 7s ease-in-out infinite`
                    }}
                  >
                    EMG
                  </Typography>
                  <Typography
                    sx={{
                      fontSize: { xs: '0.82rem', sm: '0.92rem' },
                      fontWeight: 800,
                      letterSpacing: '0.26em',
                      textTransform: 'uppercase',
                      color: 'text.secondary',
                      mt: 0.75
                    }}
                  >
                    Construction ERP
                  </Typography>
                  <Box
                    sx={{
                      mt: 1.25,
                      mx: { xs: 'auto', sm: 0 },
                      width: 68,
                      height: 3.5,
                      borderRadius: 999,
                      background: 'linear-gradient(90deg, #1e88e5 0%, #5e35b1 100%)'
                    }}
                  />
                </Box>
              </Stack>
            </motion.div>

            {/* Concise Arabic / English Tagline */}
            <motion.div
              initial={{ opacity: 0, translateY: 45 }}
              animate={{ opacity: 1, translateY: 0 }}
              transition={{ type: 'spring', stiffness: 140, damping: 26, delay: 0.14 }}
            >
              <Typography
                variant="h3"
                sx={{
                  fontWeight: 800,
                  color: 'text.primary',
                  fontSize: { xs: '1.35rem', md: '1.55rem' },
                  lineHeight: 1.4,
                  mb: 1
                }}
              >
                {isAr ? 'منظومة إدارة المشروعات الهندسية' : 'Engineering Project Management Platform'}
              </Typography>
              <Typography
                variant="body1"
                sx={{
                  color: 'text.secondary',
                  fontSize: { xs: '0.98rem', md: '1.05rem' },
                  lineHeight: 1.75,
                  maxWidth: 430
                }}
              >
                {isAr
                  ? 'متابعة متكاملة لحركة المواقع، التوريدات، والرقابة المالية الختامية في مكان واحد.'
                  : 'Unified control for site movements, supplier deliveries, and final financial reporting.'}
              </Typography>
            </motion.div>

            {/* Primary Login CTA */}
            <motion.div
              initial={{ opacity: 0, translateY: 45 }}
              animate={{ opacity: 1, translateY: 0 }}
              transition={{ type: 'spring', stiffness: 140, damping: 26, delay: 0.26 }}
            >
              <Stack direction="row" spacing={2} sx={{ justifyContent: { xs: 'center', md: 'flex-start' } }}>
                <AnimateButton>
                  <Button
                    component={RouterLink}
                    to="/login"
                    size="large"
                    variant="contained"
                    color="secondary"
                    startIcon={<IconLogin size={20} />}
                    sx={{
                      px: 4.25,
                      py: 1.4,
                      borderRadius: 2.5,
                      fontWeight: 700,
                      fontSize: '1.02rem',
                      boxShadow: '0 12px 28px rgba(94, 53, 177, 0.34)'
                    }}
                  >
                    {isAr ? 'تسجيل الدخول' : 'Log In'}
                  </Button>
                </AnimateButton>
              </Stack>
            </motion.div>

            {/* Capability Chips */}
            <motion.div
              initial={{ opacity: 0, translateY: 35 }}
              animate={{ opacity: 1, translateY: 0 }}
              transition={{ type: 'spring', stiffness: 140, damping: 26, delay: 0.38 }}
            >
              <Stack
                direction="row"
                flexWrap="wrap"
                useFlexGap
                spacing={1}
                sx={{ justifyContent: { xs: 'center', md: 'flex-start' } }}
              >
                {capabilities.map((cap) => (
                  <Chip
                    key={cap.label}
                    icon={cap.icon}
                    label={cap.label}
                    variant="outlined"
                    size="small"
                    sx={{
                      borderRadius: 2,
                      fontWeight: 600,
                      bgcolor: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(255,255,255,0.85)',
                      borderColor: 'divider'
                    }}
                  />
                ))}
              </Stack>
            </motion.div>
          </Stack>
        </Grid>

        {/* 3D Isometric Engineering Showcase Column */}
        <Grid sx={{ display: { xs: 'none', md: 'flex' } }} size={{ xs: 12, md: 7 }}>
          <EmgHeroIsometric3D isAr={isAr} isDark={isDark} liveProjectCount={liveProjectCount} />
        </Grid>
      </Grid>
    </Container>
  );
}
