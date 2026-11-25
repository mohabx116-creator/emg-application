import { ReactNode } from 'react';
import { Box, Button, Stack, Typography } from '@mui/material';
import { Link } from 'react-router-dom';
import { IconArrowRight, IconShieldLock, IconUser } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { hasAnyPermission } from 'Modules/EMG/roleAccess';

type Props = { permissions: string[]; children: ReactNode };

export default function EmgPermissionGuard({ permissions, children }: Props) {
  const { user } = useAuth();
  if (hasAnyPermission(user, permissions)) return <>{children}</>;

  return (
    <Box sx={{ p: { xs: 2, md: 4 }, maxWidth: 640, mx: 'auto' }}>
      <MainCard border boxShadow={false}>
        <Stack spacing={2.5} alignItems="center" textAlign="center" sx={{ py: 2 }}>
          <Box
            sx={{
              width: 56,
              height: 56,
              borderRadius: '50%',
              display: 'grid',
              placeItems: 'center',
              bgcolor: 'error.light',
              color: 'error.dark'
            }}
          >
            <IconShieldLock size={28} stroke={1.75} />
          </Box>
          <Box>
            <Typography variant="h3" gutterBottom>
              ليس لديك صلاحية الوصول إلى هذه الشاشة.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              حسابك الحالي لا يملك الصلاحية المطلوبة لفتح هذا القسم. اطلب الدور أو الصلاحية المناسبة من مسؤول المنظومة.
            </Typography>
          </Box>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ width: { xs: '100%', sm: 'auto' }, pt: 0.5 }}>
            <Button
              component={Link}
              to="/emg/dashboard"
              variant="contained"
              startIcon={<IconArrowRight size={18} />}
              fullWidth
            >
              العودة إلى لوحة التحكم
            </Button>
            <Button component={Link} to="/emg/profile" variant="outlined" startIcon={<IconUser size={18} />} fullWidth>
              البروفايل الشخصي
            </Button>
          </Stack>
        </Stack>
      </MainCard>
    </Box>
  );
}
