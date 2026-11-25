import { Link as RouterLink } from 'react-router-dom';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import useConfig from 'hooks/useConfig';

export default function Footer() {
  const {
    state: { i18n }
  } = useConfig();
  const isAr = i18n === 'ar';

  return (
    <Stack
      direction={{ xs: 'column-reverse', sm: 'row' }}
      sx={{
        alignItems: { xs: 'flex-start', sm: 'center' },
        justifyContent: 'space-between',
        gap: 1.5,
        pt: 3,
        mt: 'auto'
      }}
    >
      <Typography variant="caption" color="text.secondary">
        {isAr ? '© EMG Construction · جميع الحقوق محفوظة' : '© EMG Construction · All rights reserved'}
      </Typography>
      <Stack direction="row" sx={{ gap: 1.5, alignItems: 'center', flexWrap: 'wrap' }}>
        <Link component={RouterLink} to="/emg/profile" underline="hover" variant="caption" color="text.primary">
          {isAr ? 'حسابي' : 'Profile'}
        </Link>
        <Link component={RouterLink} to="/emg/notifications" underline="hover" variant="caption" color="text.primary">
          {isAr ? 'الإشعارات' : 'Notifications'}
        </Link>
        <Link component={RouterLink} to="/emg/projects" underline="hover" variant="caption" color="text.primary">
          {isAr ? 'المشاريع' : 'Projects'}
        </Link>
      </Stack>
    </Stack>
  );
}
