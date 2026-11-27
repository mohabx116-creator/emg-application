import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Card, CardActionArea, CardContent, Chip, Skeleton, Stack, Typography } from '@mui/material';
import { IconBuilding, IconChevronLeft } from '@tabler/icons-react';
import { useActiveProject } from 'contexts/ActiveProjectContext';

export default function ActiveProjectRedirect({ section = 'movements' }: { section?: 'movements' | 'advances' }) {
  const navigate = useNavigate();
  const { projects, activeProjectId, setActiveProjectId, loading } = useActiveProject();

  useEffect(() => {
    if (!loading && activeProjectId) {
      navigate(`/emg/projects/${activeProjectId}/${section}`, { replace: true });
    }
  }, [loading, activeProjectId, navigate, section]);

  if (loading) {
    return (
      <Stack spacing={2} sx={{ py: 2 }}>
        <Skeleton variant="rounded" height={56} sx={{ borderRadius: 2.5 }} />
        <Skeleton variant="rounded" height={120} sx={{ borderRadius: 3 }} />
      </Stack>
    );
  }

  if (projects.length === 0) {
    return (
      <Alert severity="info" sx={{ borderRadius: 2.5 }}>
        لا توجد مشروعات مسندة لحسابك حالياً. يرجى التواصل مع مدير النظام أو مراجعة صفحة المشروعات.
      </Alert>
    );
  }

  return (
    <Box sx={{ maxWidth: 680, mx: 'auto', py: 2 }}>
      <Typography variant="h3" sx={{ fontWeight: 800, mb: 1 }}>
        {section === 'advances' ? 'اختر المشروع لفتح الدفعات تحت الحساب' : 'اختر المشروع لفتح حركات الموقع اليومية'}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>
        سيتم حفظ اختيارك تلقائياً لفتح هذا المشروع مباشرة في المرات القادمة.
      </Typography>
      <Stack spacing={1.5}>
        {projects.map((project) => (
          <Card key={project.id} variant="outlined" sx={{ borderRadius: 3 }}>
            <CardActionArea
              onClick={() => {
                setActiveProjectId(project.id);
                navigate(`/emg/projects/${project.id}/${section}`, { replace: true });
              }}
            >
              <CardContent sx={{ py: 1.75 }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1.5}>
                  <Stack direction="row" spacing={1.5} alignItems="center" sx={{ minWidth: 0 }}>
                    <IconBuilding size={22} />
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="subtitle1" sx={{ fontWeight: 800 }} noWrap>
                        {project.name}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {project.code} · {project.location || '—'}
                      </Typography>
                    </Box>
                  </Stack>
                  <Stack direction="row" spacing={1} alignItems="center">
                    <Chip size="small" label={project.status} color={project.status === 'نشط' ? 'success' : 'default'} />
                    <IconChevronLeft size={18} />
                  </Stack>
                </Stack>
              </CardContent>
            </CardActionArea>
          </Card>
        ))}
      </Stack>
      <Button sx={{ mt: 2 }} onClick={() => navigate('/emg/projects')}>
        عرض جميع المشروعات
      </Button>
    </Box>
  );
}
