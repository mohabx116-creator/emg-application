// material-ui
import { useTheme } from '@mui/material/styles';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

// project imports

// ==============================|| PROFILE MENU - UPGRADE PLAN CARD ||============================== //

export default function UpgradePlanCard() {
  const theme = useTheme();

  const cardSX = {
    content: '""',
    position: 'absolute',
    width: 200,
    height: 200,
    borderColor: 'warning.main'
  };

  return (
    <Card
      sx={{
        bgcolor: 'warning.light',
        ...theme.applyStyles('dark', { bgcolor: 'dark.800' }),
        my: 2,
        overflow: 'hidden',
        position: 'relative',
        '&:after': {
          border: '19px solid ',
          borderRadius: '50%',
          top: '65px',
          right: '-150px',
          ...cardSX
        },
        '&:before': {
          border: '3px solid ',
          borderRadius: '50%',
          top: '145px',
          right: '-70px',
          ...cardSX
        }
      }}
    >
      <CardContent>
        <Stack sx={{ gap: 2 }}>
          <Typography variant="h4">نظام EMG</Typography>
          <Typography
            variant="subtitle2"
            sx={{
              color: 'grey.900',
              opacity: 0.6,
              ...theme.applyStyles('dark', { color: 'text.secondary', opacity: 1 })
            }}
          >
            إدارة المشاريع والرقابة المالية والتشغيلية في مكان واحد.
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
