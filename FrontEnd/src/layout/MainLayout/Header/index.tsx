import { Activity } from 'react';

// material-ui
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';

// project imports
import LogoSection from '../LogoSection';
import SearchSection from './SearchSection';
import MobileSection from './MobileSection';
import ProfileSection from './ProfileSection';
import LocalizationSection from './LocalizationSection';
import FullScreenSection from './FullScreenSection';
import NotificationSection from './NotificationSection';
import GlobalBackButton from './GlobalBackButton';

import { handlerDrawerOpen, useGetMenuMaster } from 'api/menu';
import { MenuOrientation } from 'config';
import useConfig from 'hooks/useConfig';

// assets
import { IconMenu2 } from '@tabler/icons-react';

// ==============================|| MAIN NAVBAR / HEADER ||============================== //

export default function Header() {
  const theme = useTheme();
  const downMD = useMediaQuery(theme.breakpoints.down('md'));

  const {
    state: { menuOrientation }
  } = useConfig();
  const { menuMaster } = useGetMenuMaster();
  const drawerOpen = menuMaster.isDashboardDrawerOpened;
  const isHorizontal = menuOrientation === MenuOrientation.HORIZONTAL && !downMD;

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        width: '100%',
        minWidth: 0,
        gap: { xs: 0.75, sm: 1, md: 0 }
      }}
    >
      {/* logo & toggler button */}
      <Box sx={{ width: downMD ? 'auto' : 228, display: 'flex', alignItems: 'center', flexShrink: 0 }}>
        <Box component="span" sx={{ display: { xs: 'none', md: 'block' }, flexGrow: 1 }}>
          <LogoSection />
        </Box>
        <Activity mode={!isHorizontal ? 'visible' : 'hidden'}>
          <Avatar
            variant="rounded"
            sx={{
              ...theme.typography.commonAvatar,
              ...theme.typography.mediumAvatar,
              overflow: 'hidden',
              transition: 'all .2s ease-in-out',
              color: theme.vars.palette.secondary.dark,
              background: theme.vars.palette.secondary.light,
              '&:hover': {
                color: theme.vars.palette.secondary.light,
                background: theme.vars.palette.secondary.dark
              },
              ...theme.applyStyles('dark', {
                color: theme.vars.palette.secondary.main,
                background: theme.vars.palette.dark.main,
                '&:hover': {
                  color: theme.vars.palette.secondary.light,
                  background: theme.vars.palette.secondary.main
                }
              })
            }}
            onClick={() => handlerDrawerOpen(!drawerOpen)}
          >
            <IconMenu2 stroke={1.5} size="20px" />
          </Avatar>
        </Activity>
      </Box>

      {/* header search */}
      <Box sx={{ flex: { xs: '1 1 0', md: '0 0 auto' }, minWidth: 0 }}>
        <SearchSection />
      </Box>
      <Box sx={{ display: { xs: 'none', md: 'block' }, flexGrow: 1 }} />

      {/* safe global navigation back action */}
      <Box sx={{ display: 'flex', alignItems: 'center', flexShrink: 0, mr: { xs: 0, sm: 0.5, md: 1 } }}>
        <GlobalBackButton />
      </Box>

      {/* notification */}
      <NotificationSection />

      {/* full screen toggler */}
      <Box sx={{ display: { xs: 'none', lg: 'block' }, flexShrink: 0 }}>
        <FullScreenSection />
      </Box>

      {/* profile */}
      <ProfileSection />
    </Box>
  );
}
