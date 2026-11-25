// material-ui
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import useConfig from 'hooks/useConfig';

// ==============================|| FOOTER - AUTHENTICATION ||============================== //

export default function AuthFooter() {
  const {
    state: { i18n }
  } = useConfig();
  const isAr = i18n === 'ar';

  return (
    <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
      <Typography variant="caption" color="text.secondary" fontWeight={600}>
        EMG Construction ERP
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {isAr ? '© جميع الحقوق محفوظة' : '© All rights reserved'}
      </Typography>
    </Stack>
  );
}
