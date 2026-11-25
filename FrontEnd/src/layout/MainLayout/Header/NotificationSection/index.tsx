import { Activity, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTheme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import Avatar from '@mui/material/Avatar';
import Button from '@mui/material/Button';
import CardActions from '@mui/material/CardActions';
import Chip from '@mui/material/Chip';
import ClickAwayListener from '@mui/material/ClickAwayListener';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Popper from '@mui/material/Popper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import MainCard from 'ui-component/cards/MainCard';
import Transitions from 'ui-component/extended/Transitions';
import NotificationList from './NotificationList';
import { IconBell } from '@tabler/icons-react';
import { getEmgNotifications, markAllEmgNotificationsRead, markEmgNotificationRead } from 'api/emg';
import type { EmgNotification } from 'api/emg';

export default function NotificationSection() {
  const theme = useTheme(); const downMD = useMediaQuery(theme.breakpoints.down('md')); const [open, setOpen] = useState(false); const [items, setItems] = useState<EmgNotification[]>([]); const anchorRef = useRef<any>(null);
  useEffect(() => { if (open) getEmgNotifications().then(setItems).catch(() => setItems([])); }, [open]);
  const read = async (id: number) => { await markEmgNotificationRead(id); setItems((current) => current.map((item) => item.id === id ? { ...item, isRead: true } : item)); };
  const readAll = async () => { await markAllEmgNotificationsRead(); setItems((current) => current.map((item) => ({ ...item, isRead: true }))); };
  const handleClose = (event: React.MouseEvent<HTMLDivElement> | MouseEvent | TouchEvent) => { if (anchorRef.current && anchorRef.current.contains(event.target)) return; setOpen(false); };
  return <><Box sx={{ ml: 2 }}><Avatar variant="rounded" sx={{ ...theme.typography.commonAvatar, ...theme.typography.mediumAvatar, color: theme.vars.palette.warning.dark, background: theme.vars.palette.warning.light }} ref={anchorRef} aria-controls={open ? 'menu-list-grow' : undefined} aria-haspopup="true" onClick={() => setOpen((value) => !value)}><IconBell stroke={1.5} size="20px" /></Avatar></Box><Popper placement={downMD ? 'bottom' : 'bottom-end'} open={open} anchorEl={anchorRef.current} transition disablePortal modifiers={[{ name: 'offset', options: { offset: [downMD ? 5 : 0, 20] } }]}>{({ TransitionProps }) => <ClickAwayListener onClickAway={handleClose}><Transitions position={downMD ? 'top' : 'top-right'} in={open} {...TransitionProps}><Paper><Activity mode={open ? 'visible' : 'hidden'}><MainCard border={false} elevation={16} content={false} boxShadow shadow={theme.shadows[16]} sx={{ maxWidth: 330 }}><Stack sx={{ gap: 2 }}><Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', pt: 2, px: 2 }}><Stack direction="row" sx={{ gap: 1, alignItems: 'center' }}><Typography variant="subtitle1">الإشعارات</Typography><Chip size="small" label={items.filter((item) => !item.isRead).length} sx={{ color: 'background.default', bgcolor: 'warning.dark' }} /></Stack><Button onClick={readAll} size="small">تحديد الكل كمقروء</Button></Stack><Divider /><Box sx={{ maxHeight: 'calc(100vh - 205px)', overflowX: 'hidden' }}><NotificationList items={items} onRead={read} /></Box></Stack><CardActions sx={{ p: 1.25, justifyContent: 'center' }}><Button component={Link} to="/emg/notifications" size="small" disableElevation>عرض كل الإشعارات</Button></CardActions></MainCard></Activity></Paper></Transitions></ClickAwayListener>}</Popper></>;
}
