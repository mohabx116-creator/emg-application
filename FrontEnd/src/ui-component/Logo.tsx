import { Box } from '@mui/material';
import emgLogo from 'assets/images/emg-logo.png';

export default function Logo({ dark = false }: { dark?: boolean }) {
  return (
    <Box
      component="img"
      src={emgLogo}
      alt="EMG Construction"
      sx={{
        display: 'block',
        width: { xs: 104, sm: 116 },
        height: { xs: 48, sm: 52 },
        objectFit: 'contain',
        objectPosition: 'center',
        borderRadius: 1,
        backgroundColor: dark ? 'common.white' : 'transparent'
      }}
    />
  );
}
