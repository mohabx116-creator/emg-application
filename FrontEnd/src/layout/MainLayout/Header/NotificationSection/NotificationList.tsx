import { useTheme } from '@mui/material/styles';
import Avatar from '@mui/material/Avatar';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemAvatar from '@mui/material/ListItemAvatar';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import { IconBell } from '@tabler/icons-react';
import type { EmgNotification } from 'api/emg';

type Props = { items: EmgNotification[]; onRead: (id: number) => void };

export default function NotificationList({ items, onRead }: Props) {
  const theme = useTheme();
  return <List sx={{ width: '100%', maxWidth: { xs: 300, md: 330 }, py: 0 }}>{items.slice(0, 8).map((item) => <Box key={item.id} sx={{ p: 1.5, borderBottom: '1px solid', borderColor: 'divider', bgcolor: item.isRead ? 'background.paper' : 'primary.light' }}><ListItem alignItems="flex-start" disablePadding secondaryAction={!item.isRead ? <Button size="small" onClick={() => onRead(item.id)}>قراءة</Button> : undefined}><ListItemAvatar><Avatar sx={{ color: theme.palette.primary.main, bgcolor: theme.palette.primary.light }}><IconBell size={18} /></Avatar></ListItemAvatar><ListItemText primary={<Stack direction="row" spacing={1} alignItems="center"><Typography variant="subtitle2">{item.title}</Typography>{!item.isRead && <Chip label="جديد" color="primary" size="small" />}</Stack>} secondary={<><Typography component="span" variant="body2">{item.message}</Typography><br /><Typography component="span" variant="caption" color="text.secondary">{new Date(item.createdAtUtc).toLocaleString('ar-EG-u-nu-latn')}</Typography></>} /></ListItem></Box>)}{!items.length && <Typography color="text.secondary" sx={{ p: 2, textAlign: 'center' }}>لا توجد إشعارات حاليًا.</Typography>}</List>;
}
