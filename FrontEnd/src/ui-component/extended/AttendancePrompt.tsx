import { Alert, Button, Stack } from '@mui/material';
import { Link, useLocation } from 'react-router-dom';
import { useTodayAttendanceCache } from 'hooks/useEmgData';

export default function AttendancePrompt() {
  const { pathname } = useLocation();
  const { data: attendance, isLoading } = useTodayAttendanceCache(true);

  if (isLoading || attendance !== null || pathname.startsWith('/emg/attendance')) {
    return null;
  }

  return (
    <Alert
      severity="info"
      sx={{ mb: 2, borderRadius: 2 }}
      action={
        <Button component={Link} to="/emg/attendance" color="inherit" size="small" sx={{ fontWeight: 700 }}>
          تسجيل الحضور
        </Button>
      }
    >
      <Stack direction="row" alignItems="center">
        لم يتم تسجيل حضورك اليوم بعد.
      </Stack>
    </Alert>
  );
}
