import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router-dom';

export default function NotFound() {
  return <Box dir="rtl" sx={{ minHeight: '70vh', display: 'grid', placeItems: 'center', textAlign: 'center', p: 3 }}><Box><Typography variant="h1" color="primary.main">404</Typography><Typography variant="h2" sx={{ mb: 1 }}>الصفحة غير موجودة</Typography><Typography color="text.secondary" sx={{ mb: 3 }}>تأكد من الرابط أو ارجع إلى لوحة تحكم EMG.</Typography><Button component={Link} to="/emg/dashboard" variant="contained">لوحة التحكم</Button></Box></Box>;
}
