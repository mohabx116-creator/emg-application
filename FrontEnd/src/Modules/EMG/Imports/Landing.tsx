import { Box, Button, Grid, Stack, Typography } from '@mui/material';
import { IconArrowLeft, IconFileUpload, IconHistory, IconRefresh } from '@tabler/icons-react';
import { useNavigate } from 'react-router-dom';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { hasPermission } from '../roleAccess';

export default function ImportsLanding() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const canMigrate = hasPermission(user, 'data.migrate');
  const canApprovedHistory = hasPermission(user, 'project-imports.import-approved-history');

  return (
    <Box sx={{ direction: 'rtl', pb: 4 }}>
      <Stack spacing={0.75} sx={{ mb: 3 }}>
        <Typography variant="h2" sx={{ fontWeight: 850 }}>
          استيراد البيانات
        </Typography>
        <Typography color="text.secondary">
          ارفع ملف Excel الأصلي للمشروع، راجع التحليل والأخطاء المكتشفة، ثم اعتمد الاستيراد بأمان.
        </Typography>
      </Stack>

      <Grid container spacing={2.5}>
        {canMigrate && (
          <>
            <Grid size={{ xs: 12, md: canApprovedHistory ? 4 : 6 }}>
              <MainCard sx={{ height: '100%', borderRadius: 3, border: '1px solid', borderColor: 'primary.light' }}>
                <Stack spacing={2.25} sx={{ p: { xs: 0.5, md: 1.5 } }}>
                  <Box sx={{ width: 52, height: 52, borderRadius: 2.5, display: 'grid', placeItems: 'center', bgcolor: 'primary.lighter', color: 'primary.main' }}>
                    <IconFileUpload size={26} />
                  </Box>
                  <Box>
                    <Typography variant="h3" sx={{ mb: 0.75, fontWeight: 800 }}>
                      استيراد مشروع جديد من Excel
                    </Typography>
                    <Typography color="text.secondary" variant="body2">
                      تحليل تلقائي للشيتات والبيانات، عرض الصفوف الصالحة والأخطاء مع الخلية والسبب، وإنشاء المشروع مباشرة دون إنشائه يدوياً مسبقاً.
                    </Typography>
                  </Box>
                  <Button variant="contained" endIcon={<IconArrowLeft size={18} />} onClick={() => navigate('/emg/imports/new')} sx={{ alignSelf: 'flex-start', borderRadius: 2, fontWeight: 700 }}>
                    بدء استيراد مشروع جديد
                  </Button>
                </Stack>
              </MainCard>
            </Grid>

            <Grid size={{ xs: 12, md: canApprovedHistory ? 4 : 6 }}>
              <MainCard sx={{ height: '100%', borderRadius: 3, border: '1px solid', borderColor: 'warning.light' }}>
                <Stack spacing={2.25} sx={{ p: { xs: 0.5, md: 1.5 } }}>
                  <Box sx={{ width: 52, height: 52, borderRadius: 2.5, display: 'grid', placeItems: 'center', bgcolor: 'warning.lighter', color: 'warning.dark' }}>
                    <IconRefresh size={26} />
                  </Box>
                  <Box>
                    <Typography variant="h3" sx={{ mb: 0.75, fontWeight: 800 }}>
                      تحديث مشروع موجود
                    </Typography>
                    <Typography color="text.secondary" variant="body2">
                      أعد استيراد ملف Excel لتحديث البيانات المستوردة لمشروع مسجّل مع الحفاظ على الحركات اليدوية.
                    </Typography>
                  </Box>
                  <Button variant="contained" color="warning" endIcon={<IconArrowLeft size={18} />} onClick={() => navigate('/emg/imports/update')} sx={{ alignSelf: 'flex-start', borderRadius: 2, fontWeight: 700 }}>
                    بدء تحديث مشروع
                  </Button>
                </Stack>
              </MainCard>
            </Grid>
          </>
        )}

        {canApprovedHistory && (
          <Grid size={{ xs: 12, md: canMigrate ? 4 : 6 }}>
            <MainCard sx={{ height: '100%', borderRadius: 3, border: '1px solid', borderColor: 'secondary.light' }}>
              <Stack spacing={2.25} sx={{ p: { xs: 0.5, md: 1.5 } }}>
                <Box sx={{ width: 52, height: 52, borderRadius: 2.5, display: 'grid', placeItems: 'center', bgcolor: 'secondary.lighter', color: 'secondary.main' }}>
                  <IconHistory size={26} />
                </Box>
                <Box>
                  <Typography variant="h3" sx={{ mb: 0.75, fontWeight: 800 }}>
                    استيراد الأرشيف التاريخي المعتمد
                  </Typography>
                  <Typography color="text.secondary" variant="body2">
                    استيراد ومراجعة الأرشيف المالي المغلق والمعتمد للمشروعات التاريخية مع إثبات المصدر.
                  </Typography>
                </Box>
                <Button variant="contained" color="secondary" endIcon={<IconArrowLeft size={18} />} onClick={() => navigate('/emg/imports/approved-history')} sx={{ alignSelf: 'flex-start', borderRadius: 2, fontWeight: 700 }}>
                  فتح الأرشيف المعتمد
                </Button>
              </Stack>
            </MainCard>
          </Grid>
        )}
      </Grid>
    </Box>
  );
}
