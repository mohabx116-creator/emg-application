import { Box, Button, Grid, Stack, Typography } from '@mui/material';
import { IconFileUpload, IconRefresh, IconArrowLeft } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';

export default function ImportsLanding() {
  const navigate = useNavigate();
  return <Box sx={{ direction: 'rtl', pb: 4 }}>
    <Stack spacing={0.75} sx={{ mb: 3 }}>
      <Typography variant="h2" sx={{ fontWeight: 800 }}>ترحيل البيانات</Typography>
      <Typography color="text.secondary">اختر العملية المطلوبة للبدء. كل مسار مستقل وواضح قبل حفظ أي بيانات.</Typography>
    </Stack>
    <Grid container spacing={2.5}>
      <Grid size={{ xs: 12, md: 6 }}>
        <MainCard sx={{ height: '100%', border: '1px solid', borderColor: 'primary.light', boxShadow: '0 12px 30px rgba(33, 150, 243, 0.12)' }}>
          <Stack spacing={2.25} sx={{ p: { xs: 1, md: 2 } }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, display: 'grid', placeItems: 'center', bgcolor: 'primary.lighter', color: 'primary.main' }}><IconFileUpload size={28} /></Box>
            <Box><Typography variant="h3" sx={{ mb: 0.75 }}>إنشاء مشروع جديد</Typography><Typography color="text.secondary">استورد ملف Excel لأول مرة، راجع التصنيف والخطة، ثم أنشئ مشروعًا جديدًا تحت المراجعة.</Typography></Box>
            <Button variant="contained" endIcon={<IconArrowLeft size={18} />} onClick={() => navigate('/emg/imports/new')} sx={{ alignSelf: 'flex-start' }}>بدء إنشاء مشروع</Button>
          </Stack>
        </MainCard>
      </Grid>
      <Grid size={{ xs: 12, md: 6 }}>
        <MainCard sx={{ height: '100%', border: '1px solid', borderColor: 'warning.light', boxShadow: '0 12px 30px rgba(245, 158, 11, 0.12)' }}>
          <Stack spacing={2.25} sx={{ p: { xs: 1, md: 2 } }}>
            <Box sx={{ width: 56, height: 56, borderRadius: 2, display: 'grid', placeItems: 'center', bgcolor: 'warning.lighter', color: 'warning.dark' }}><IconRefresh size={28} /></Box>
            <Box><Typography variant="h3" sx={{ mb: 0.75 }}>تحديث مشروع موجود</Typography><Typography color="text.secondary">أعد استيراد ملف Excel لتحديث الحركات المستوردة فقط مع الحفاظ على الإدخالات اليدوية.</Typography></Box>
            <Button variant="contained" color="warning" endIcon={<IconArrowLeft size={18} />} onClick={() => navigate('/emg/imports/update')} sx={{ alignSelf: 'flex-start' }}>بدء تحديث مشروع</Button>
          </Stack>
        </MainCard>
      </Grid>
    </Grid>
  </Box>;
}
