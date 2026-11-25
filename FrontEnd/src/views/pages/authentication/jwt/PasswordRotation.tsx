import { useNavigate } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

import useAuth from 'hooks/useAuth';
import ChangePassword from 'views/application/users/account-profile/Profile1/ChangePassword';
import { clearPasswordRotationRequired } from 'auth/passwordRotation';

export default function PasswordRotation() {
  const { logout } = useAuth();
  const navigate = useNavigate();

  const completeRotation = () => {
    clearPasswordRotationRequired();
    logout();
    navigate('/login?reason=password-changed', { replace: true });
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: { xs: 2, sm: 4 } }}>
      <Paper elevation={3} sx={{ width: '100%', maxWidth: 720, p: { xs: 2, sm: 4 } }}>
        <Stack spacing={2.5}>
          <div>
            <Typography variant="h3" gutterBottom>
              تحديث كلمة المرور مطلوب
            </Typography>
            <Typography color="text.secondary">
              حدّث كلمة المرور للمتابعة. لن تظهر لك شاشات التشغيل قبل إتمام التحديث.
            </Typography>
          </div>
          <Alert severity="warning">بعد الحفظ سيتم تسجيل خروجك تلقائيًا. سجّل الدخول مرة أخرى باستخدام كلمة المرور الجديدة.</Alert>
          <ChangePassword forced onCompleted={completeRotation} />
          <Button color="inherit" onClick={logout} sx={{ alignSelf: 'flex-start' }}>
            تسجيل الخروج
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}
