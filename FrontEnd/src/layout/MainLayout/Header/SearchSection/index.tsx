import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import ClickAwayListener from '@mui/material/ClickAwayListener';
import InputAdornment from '@mui/material/InputAdornment';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import OutlinedInput from '@mui/material/OutlinedInput';
import Popper from '@mui/material/Popper';
import Typography from '@mui/material/Typography';
import { useTheme } from '@mui/material/styles';
import { IconAdjustmentsHorizontal, IconSearch } from '@tabler/icons-react';
import { searchEmg, type EmgSearchResult } from 'api/emg';

const labels: Record<string, string> = { project: 'مشروع', supplier: 'مورد', 'site-movement': 'حركة موقع', 'advance-payment': 'دفعة تحت الحساب', excavation: 'حفر', concrete: 'خرسانات', steel: 'حديد', finishing: 'تشطيبات' };
const routeFor = (item: EmgSearchResult) => item.entityType === 'project' ? `/emg/projects/${item.entityId}` : item.entityType === 'supplier' ? `/emg/suppliers/${item.entityId}` : item.entityType === 'site-movement' ? `/emg/site-movements/${item.entityId}` : item.entityType === 'advance-payment' ? `/emg/advances/${item.entityId}` : item.projectId ? `/emg/projects/${item.projectId}` : '/emg/projects';

export default function SearchSection() {
  const theme = useTheme(); const navigate = useNavigate(); const [value, setValue] = useState(''); const [items, setItems] = useState<EmgSearchResult[]>([]); const [open, setOpen] = useState(false); const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  useEffect(() => { const query = value.trim(); if (query.length < 2) { setItems([]); return; } const timer = window.setTimeout(() => { searchEmg(query).then(setItems).catch(() => setItems([])); }, 250); return () => window.clearTimeout(timer); }, [value]);
  const choose = (item: EmgSearchResult) => { navigate(routeFor(item)); setOpen(false); setValue(''); };
  const input = <OutlinedInput id="input-search-header" value={value} onFocus={(event) => { setAnchor(event.currentTarget); setOpen(value.trim().length >= 2); }} onChange={(event) => { setValue(event.target.value); setAnchor(event.currentTarget); setOpen(event.target.value.trim().length >= 2); }} placeholder="بحث في النظام" startAdornment={<InputAdornment position="start"><IconSearch stroke={1.5} size="16px" /></InputAdornment>} endAdornment={<InputAdornment position="end"><Avatar variant="rounded" sx={{ ...theme.typography.commonAvatar, ...theme.typography.mediumAvatar, color: theme.vars.palette.secondary.dark, background: theme.vars.palette.secondary.light }}><IconAdjustmentsHorizontal stroke={1.5} size="19px" /></Avatar></InputAdornment>} sx={{ width: { xs: '100%', md: 434 }, ml: 2, px: 2 }} />;
  return <><Box sx={{ display: { xs: 'block', md: 'none' }, px: 1, width: '100%' }}>{input}</Box><Box sx={{ display: { xs: 'none', md: 'block' } }}>{input}</Box><Popper open={open && value.trim().length >= 2} anchorEl={anchor} placement="bottom-start" sx={{ zIndex: 1300, width: { xs: 'calc(100vw - 32px)', md: 434 } }}><ClickAwayListener onClickAway={() => setOpen(false)}><Card sx={{ mt: 1, maxHeight: 420, overflowY: 'auto' }}><List dense>{items.map((item) => <ListItemButton key={`${item.entityType}-${item.entityId}`} onClick={() => choose(item)}><ListItemText primary={<Typography fontWeight={700}>{item.title}</Typography>} secondary={`${labels[item.entityType] || 'نتيجة'} · ${item.subtitle} · ${item.status}`} /></ListItemButton>)}{!items.length && <Box sx={{ p: 2 }}><Typography color="text.secondary">جاري البحث أو لا توجد نتائج مطابقة.</Typography></Box>}</List></Card></ClickAwayListener></Popper></>;
}
