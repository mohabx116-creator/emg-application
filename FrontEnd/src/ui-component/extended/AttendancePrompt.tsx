import { useEffect, useState } from 'react';
import { Alert, Button, Stack } from '@mui/material';
import { Link } from 'react-router-dom';
import { getTodayAttendance } from 'api/emg';

export default function AttendancePrompt() {
  const [missing, setMissing] = useState(false);
  useEffect(() => { getTodayAttendance().then((attendance) => setMissing(!attendance)).catch(() => setMissing(false)); }, []);
  if (!missing) return null;
  return <Alert severity="info" sx={{ mb: 2 }} action={<Button component={Link} to="/emg/attendance" color="inherit" size="small">تسجيل الحضور</Button>}><Stack direction="row" alignItems="center">لم يتم تسجيل حضورك اليوم بعد.</Stack></Alert>;
}
