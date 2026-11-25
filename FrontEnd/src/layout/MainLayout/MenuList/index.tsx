import { Activity, memo, useLayoutEffect, useState } from 'react';

// material-ui
import { Theme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import Divider from '@mui/material/Divider';
import List from '@mui/material/List';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// project imports
import NavItem from './NavItem';
import NavGroup from './NavGroup';
import { MenuOrientation } from 'config';
import emgGroups from 'menu-items/emg';
import useConfig from 'hooks/useConfig';
import useAuth from 'hooks/useAuth';

import { HORIZONTAL_MAX_ITEM } from 'config';
import { useGetMenuMaster } from 'api/menu';
import { hasAnyPermission, isSystemAdmin } from 'Modules/EMG/roleAccess';

// types
import { NavItemType } from 'types';
import { UserProfile } from 'types/user-profile';

function filterMenuByPermission(items: NavItemType[], user: UserProfile | null | undefined): NavItemType[] {
  return items.reduce<NavItemType[]>((visible, item) => {
    const allowed =
      (!item.requiredPermission && !item.requiredPermissions) ||
      (item.requiredPermission ? hasAnyPermission(user, [item.requiredPermission]) : false) ||
      (item.requiredPermissions ? hasAnyPermission(user, item.requiredPermissions) : false) ||
      isSystemAdmin(user);
    const children = item.children ? filterMenuByPermission(item.children, user) : undefined;
    if (!allowed || (item.children && !children?.length)) return visible;
    visible.push(children ? { ...item, children } : item);
    return visible;
  }, []);
}

// ==============================|| SIDEBAR MENU LIST ||============================== //

function MenuList() {
  const downMD = useMediaQuery((theme: Theme) => theme.breakpoints.down('md'));

  const {
    state: { menuOrientation }
  } = useConfig();
  const { menuMaster } = useGetMenuMaster();
  const { user } = useAuth();
  const drawerOpen = menuMaster.isDashboardDrawerOpened;
  const isHorizontal = menuOrientation === MenuOrientation.HORIZONTAL && !downMD;

  const [selectedID, setSelectedID] = useState<string | undefined>('');
  const [menuItems, setMenuItems] = useState<{ items: NavItemType[] }>({ items: [] });

  useLayoutEffect(() => {
    // Production navigation is deliberately allowlisted. Berry demonstration
    // menus are not imported into the production bundle or exposed by URL.
    setMenuItems({ items: filterMenuByPermission(emgGroups, user) });
  }, [user]);

  // last menu-item to show in horizontal menu bar
  const lastItem = isHorizontal ? HORIZONTAL_MAX_ITEM : null;

  let lastItemIndex = menuItems.items.length - 1;
  let remItems: NavItemType[] = [];
  let lastItemId: string;

  if (lastItem && lastItem < menuItems.items.length) {
    lastItemId = menuItems.items[lastItem - 1].id!;
    lastItemIndex = lastItem - 1;
    remItems = menuItems.items.slice(lastItem - 1, menuItems.items.length).map((item) => ({
      title: item.title,
      elements: item.children,
      icon: item.icon,
      ...(item.url && {
        url: item.url
      })
    }));
  }

  const berryStartIndex = menuItems.items.findIndex((item) => !item.id?.startsWith('emg'));

  const navItems = menuItems.items.slice(0, lastItemIndex + 1).map((item, index) => {
    const berrySectionHeader = !isHorizontal && index === berryStartIndex && berryStartIndex !== -1 ? (
      <Box
        key="berry-components-section"
        sx={{
          px: drawerOpen ? 1.5 : 0.5,
          pt: 1.25,
          pb: 0.75,
          mt: 0.5,
          borderTop: '1px solid',
          borderColor: 'divider'
        }}
      >
        <Typography
          variant="overline"
          sx={{
            display: 'block',
            color: 'primary.main',
            fontWeight: 700,
            letterSpacing: 0,
            textAlign: drawerOpen ? 'right' : 'center'
          }}
        >
          مكونات Berry
        </Typography>
        {drawerOpen && (
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'right' }}>
            مكونات القالب المتاحة لمسؤول المنظومة
          </Typography>
        )}
      </Box>
    ) : null;

    const renderItem = (() => {
    switch (item.type) {
      case 'group':
        if (item.url && item.id !== lastItemId) {
          return (
            <List key={item.id}>
              <NavItem item={item} level={1} isParents setSelectedID={() => setSelectedID('')} />
              <Activity mode={!isHorizontal && index !== 0 ? 'visible' : 'hidden'}>
                <Divider sx={{ py: 0.5 }} />
              </Activity>
            </List>
          );
        }

        return (
          <NavGroup
            key={item.id}
            setSelectedID={setSelectedID}
            selectedID={selectedID}
            item={item}
            lastItem={lastItem!}
            remItems={remItems}
            lastItemId={lastItemId}
          />
        );
      default:
        return (
          <Typography key={item.id} variant="h6" align="center" sx={{ color: 'error.main' }}>
            Menu Items Error
          </Typography>
        );
    }
    })();

    return berrySectionHeader ? <Box key={`section-${item.id}`}>{berrySectionHeader}{renderItem}</Box> : renderItem;
  });

  return !isHorizontal ? <Box {...(drawerOpen && { sx: { mt: 1.5 } })}>{navItems}</Box> : <>{navItems}</>;
}

export default memo(MenuList);
