import { isRouteErrorResponse, useRouteError } from 'react-router-dom';

// material-ui
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router-dom';

// ==============================|| ELEMENT ERROR - COMMON ||============================== //

export default function ErrorBoundary() {
  const error = useRouteError();

  if (isRouteErrorResponse(error)) {
    if (error.status === 404) {
      return <Box dir="rtl" sx={{ minHeight: 320, display: 'grid', placeItems: 'center', textAlign: 'center', p: 3 }}><Box><Typography variant="h2" sx={{ mb: 1 }}>الصفحة غير موجودة</Typography><Typography color="text.secondary" sx={{ mb: 2 }}>الرابط الذي فتحته غير صحيح أو لم يعد متاحًا.</Typography><Button component={Link} to="/emg/dashboard" variant="contained">العودة إلى لوحة التحكم</Button></Box></Box>;
    }

    if (error.status === 401) {
      return <Alert severity="error">ليس لديك صلاحية لعرض هذه الصفحة.</Alert>;
    }

    if (error.status === 503) {
      return <Alert severity="error">الخدمة غير متاحة حاليًا. حاول مرة أخرى بعد قليل.</Alert>;
    }

    if (error.status === 418) {
      return <Alert severity="error">حدثت مشكلة غير متوقعة. تواصل مع مسؤول المنظومة.</Alert>;
    }
  }

  return <Alert severity="error">تعذر تحميل الصفحة. حاول مرة أخرى.</Alert>;
}
