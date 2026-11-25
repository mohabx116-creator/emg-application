import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Skeleton,
  Stack,
  Tab,
  TablePagination,
  Tabs,
  Typography
} from '@mui/material';
import { IconBell, IconBellOff, IconCheck, IconChecks, IconRefresh } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { EmgNotification, getEmgNotifications, markAllEmgNotificationsRead, markEmgNotificationRead } from 'api/emg';

export default function EmgNotifications() {
  const [items, setItems] = useState<EmgNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<'all' | 'unread'>('all');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(15);

  const load = () => {
    setLoading(true);
    setError(null);
    getEmgNotifications()
      .then(setItems)
      .catch(() => setError('تعذر تحميل الإشعارات.'))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const read = async (id: number) => {
    await markEmgNotificationRead(id);
    setItems((current) => current.map((item) => (item.id === id ? { ...item, isRead: true } : item)));
  };

  const readAll = async () => {
    await markAllEmgNotificationsRead();
    setItems((current) => current.map((item) => ({ ...item, isRead: true })));
  };

  const unreadCount = useMemo(() => items.filter((item) => !item.isRead).length, [items]);
  const filtered = useMemo(() => (tab === 'unread' ? items.filter((item) => !item.isRead) : items), [items, tab]);
  const paginated = useMemo(() => filtered.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage), [filtered, page, rowsPerPage]);

  return (
    <Box sx={{ pb: 4 }}>
      {/* Executive Header */}
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'flex-start', sm: 'center' }}
        spacing={2}
        sx={{ mb: 3 }}
      >
        <Box>
          <Stack direction="row" spacing={1.25} alignItems="center">
            <Typography variant="h2" sx={{ fontWeight: 850 }}>
              الإشعارات
            </Typography>
            <Chip
              size="small"
              color={unreadCount > 0 ? 'primary' : 'success'}
              label={unreadCount > 0 ? `${unreadCount} غير مقروء` : 'كلها مقروءة'}
              sx={{ fontWeight: 700 }}
            />
          </Stack>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            متابعة التنبيهات والاعتمادات والإجراءات المطلوبة داخل النظام
          </Typography>
        </Box>

        <Stack direction="row" spacing={1.25}>
          <Button
            variant="outlined"
            color="inherit"
            onClick={load}
            startIcon={<IconRefresh size={16} />}
            disabled={loading}
            sx={{ borderRadius: 2, fontWeight: 700 }}
          >
            تحديث
          </Button>
          <Button
            variant="contained"
            color="secondary"
            onClick={readAll}
            disabled={loading || unreadCount === 0}
            startIcon={<IconChecks size={17} />}
            sx={{ borderRadius: 2, fontWeight: 700 }}
          >
            تحديد الكل كمقروء
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2.5, borderRadius: 2 }}>
          {error}
        </Alert>
      )}

      <MainCard content={false} sx={{ borderRadius: 3, overflow: 'hidden' }}>
        <Box sx={{ px: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Tabs
            value={tab}
            onChange={(_, v) => {
              setTab(v);
              setPage(0);
            }}
          >
            <Tab value="all" label={`كل الإشعارات (${items.length})`} />
            <Tab value="unread" label={`غير مقروء (${unreadCount})`} />
          </Tabs>
        </Box>

        <Box sx={{ p: 2.5 }}>
          {loading ? (
            <Stack spacing={1.5}>
              {[0, 1, 2, 3].map((i) => (
                <Skeleton key={i} variant="rounded" height={86} sx={{ borderRadius: 2.5 }} />
              ))}
            </Stack>
          ) : paginated.length === 0 ? (
            <Stack spacing={1.25} alignItems="center" sx={{ py: 5, textAlign: 'center' }}>
              <Avatar sx={{ width: 52, height: 52, bgcolor: 'action.hover', color: 'text.secondary' }}>
                <IconBellOff size={26} />
              </Avatar>
              <Typography variant="h4" color="text.secondary" sx={{ fontWeight: 700 }}>
                {tab === 'unread' ? 'لا توجد إشعارات غير مقروءة' : 'لا توجد إشعارات حاليًا'}
              </Typography>
            </Stack>
          ) : (
            <Stack spacing={1.25}>
              {paginated.map((item) => (
                <Card
                  key={item.id}
                  variant="outlined"
                  sx={{
                    borderRadius: 2.5,
                    borderColor: item.isRead ? 'divider' : 'primary.main',
                    bgcolor: item.isRead ? 'background.paper' : 'rgba(30, 136, 229, 0.05)',
                    transition: 'box-shadow 180ms ease',
                    '&:hover': { boxShadow: '0 4px 16px rgba(15, 23, 42, 0.06)' }
                  }}
                >
                  <CardContent sx={{ p: 2, '&:last-child': { pb: 2 } }}>
                    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1.5}>
                      <Stack direction="row" spacing={1.5} alignItems="flex-start">
                        <Avatar
                          sx={{
                            width: 38,
                            height: 38,
                            bgcolor: item.isRead ? 'action.hover' : 'primary.main',
                            color: item.isRead ? 'text.secondary' : '#fff',
                            flexShrink: 0,
                            mt: 0.25
                          }}
                        >
                          <IconBell size={18} />
                        </Avatar>
                        <Box>
                          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
                            <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                              {item.title}
                            </Typography>
                            {!item.isRead && <Chip label="جديد" color="primary" size="small" sx={{ height: 20, fontWeight: 700 }} />}
                          </Stack>
                          <Typography variant="body2" color="text.primary" sx={{ mt: 0.5 }}>
                            {item.message}
                          </Typography>
                          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                            {new Date(item.createdAtUtc).toLocaleString('ar-EG')}
                          </Typography>
                        </Box>
                      </Stack>

                      {!item.isRead && (
                        <Button
                          size="small"
                          variant="outlined"
                          color="primary"
                          startIcon={<IconCheck size={15} />}
                          onClick={() => read(item.id)}
                          sx={{ borderRadius: 2, fontWeight: 700, flexShrink: 0 }}
                        >
                          تحديد كمقروء
                        </Button>
                      )}
                    </Stack>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          )}
        </Box>

        {!loading && filtered.length > rowsPerPage && (
          <TablePagination
            component="div"
            count={filtered.length}
            page={page}
            onPageChange={(_, p) => setPage(p)}
            rowsPerPage={rowsPerPage}
            onRowsPerPageChange={(e) => {
              setRowsPerPage(parseInt(e.target.value, 10));
              setPage(0);
            }}
            rowsPerPageOptions={[10, 15, 25, 50]}
            labelRowsPerPage="عدد الإشعارات:"
            labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`}
            sx={{ borderTop: 1, borderColor: 'divider' }}
          />
        )}
      </MainCard>
    </Box>
  );
}
