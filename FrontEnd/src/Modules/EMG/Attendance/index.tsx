import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography
} from '@mui/material';
import { IconCalendar, IconClock, IconEdit, IconLogin, IconLogout, IconRefresh, IconTrash, IconUserCheck } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { checkInAttendance, checkOutAttendance, correctEmgTransaction, deleteEmgTransaction, EmgAttendance as AttendanceRecord, getEmgAttendance, getTodayAttendance } from 'api/emg';

const formatTime = (value: string | null) =>
  value ? new Date(value).toLocaleTimeString('ar-EG-u-nu-latn', { hour: '2-digit', minute: '2-digit' }) : '—';

const todayIso = () => new Date().toISOString().slice(0, 10);

export default function EmgAttendance() {
  const [attendance, setAttendance] = useState<AttendanceRecord | null>(null);
  const [records, setRecords] = useState<AttendanceRecord[]>([]);
  const [selectedDate, setSelectedDate] = useState(todayIso);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [notes, setNotes] = useState('');
  const [message, setMessage] = useState<{ severity: 'success' | 'warning'; text: string } | null>(null);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(15);
  const [editing, setEditing] = useState<AttendanceRecord | null>(null);
  const [editForm, setEditForm] = useState({ checkIn: '', checkOut: '', notes: '' });

  const load = (dateStr = selectedDate) => {
    setLoading(true);
    Promise.all([getTodayAttendance(), getEmgAttendance(dateStr)])
      .then(([today, list]) => {
        setAttendance(today);
        setRecords(list);
      })
      .catch(() => setMessage({ severity: 'warning', text: 'تعذر تحميل حالة الحضور.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load(selectedDate);
  }, [selectedDate]);

  const checkIn = async () => {
    setSaving(true);
    try {
      setAttendance(await checkInAttendance(notes));
      setNotes('');
      setMessage({ severity: 'success', text: 'تم تسجيل الحضور بنجاح.' });
      load(selectedDate);
    } catch {
      setMessage({ severity: 'warning', text: 'تعذر تسجيل الحضور. ربما تم تسجيله من قبل.' });
    } finally {
      setSaving(false);
    }
  };

  const checkOut = async () => {
    setSaving(true);
    try {
      setAttendance(await checkOutAttendance(notes));
      setNotes('');
      setMessage({ severity: 'success', text: 'تم تسجيل الانصراف بنجاح.' });
      load(selectedDate);
    } catch {
      setMessage({ severity: 'warning', text: 'يجب تسجيل الحضور أولًا.' });
    } finally {
      setSaving(false);
    }
  };

  const remove = async (record: AttendanceRecord) => {
    if (!window.confirm('سيتم حذف سجل الحضور نهائيًا. هل تريد المتابعة؟')) return;
    try { await deleteEmgTransaction('attendance', record.id); setMessage({ severity: 'success', text: 'تم حذف سجل الحضور.' }); load(selectedDate); }
    catch { setMessage({ severity: 'warning', text: 'تعذر حذف سجل الحضور.' }); }
  };

  const openEdit = (record: AttendanceRecord) => {
    const local = (value: string | null) => value ? new Date(value).toISOString().slice(0, 16) : '';
    setEditing(record); setEditForm({ checkIn: local(record.checkInAtUtc), checkOut: local(record.checkOutAtUtc), notes: record.notes || '' });
  };
  const saveEdit = async () => {
    if (!editing || !editForm.checkIn) return;
    try {
      await correctEmgTransaction('attendance', editing.id, { CheckInAtUtc: new Date(editForm.checkIn).toISOString(), CheckOutAtUtc: editForm.checkOut ? new Date(editForm.checkOut).toISOString() : null, Notes: editForm.notes }, 'تصحيح مباشر');
      setEditing(null); setMessage({ severity: 'success', text: 'تم تصحيح سجل الحضور.' }); load(selectedDate);
    } catch (e: any) { setMessage({ severity: 'warning', text: e?.response?.data?.message || 'تعذر تصحيح سجل الحضور.' }); }
  };

  const paginatedRecords = useMemo(
    () => records.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage),
    [records, page, rowsPerPage]
  );

  const completedCount = useMemo(() => records.filter((r) => Boolean(r.checkOutAtUtc)).length, [records]);

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
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            الحضور والانصراف
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            تسجيل حضورك وانصرافك اليومي ومتابعة سجل الحضور
          </Typography>
        </Box>
        <Button
          variant="outlined"
          color="secondary"
          startIcon={<IconRefresh size={17} />}
          onClick={() => load(selectedDate)}
          disabled={loading}
          sx={{ borderRadius: 2, fontWeight: 700 }}
        >
          تحديث
        </Button>
      </Stack>

      {message && (
        <Alert sx={{ mb: 2.5, borderRadius: 2 }} severity={message.severity} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {loading ? (
        <Stack spacing={2.5}>
          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 8 }}>
              <Skeleton variant="rounded" height={170} sx={{ borderRadius: 3 }} />
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Skeleton variant="rounded" height={170} sx={{ borderRadius: 3 }} />
            </Grid>
          </Grid>
          <Skeleton variant="rounded" height={280} sx={{ borderRadius: 3 }} />
        </Stack>
      ) : (
        <>
          <Grid container spacing={2.25} sx={{ mb: 3 }}>
            {/* Main Check-in / Check-out Hero Card */}
            <Grid size={{ xs: 12, lg: 8 }}>
              <MainCard sx={{ height: '100%', borderRadius: 3 }}>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={2.5} alignItems={{ xs: 'stretch', md: 'center' }}>
                  <Avatar
                    variant="rounded"
                    sx={{
                      width: 60,
                      height: 60,
                      borderRadius: 3,
                      background: attendance?.checkOutAtUtc
                        ? 'linear-gradient(135deg, #059669 0%, #10b981 100%)'
                        : 'linear-gradient(135deg, #0d47a1 0%, #1e88e5 100%)',
                      color: '#fff',
                      flexShrink: 0,
                      boxShadow: '0 8px 20px rgba(13, 71, 161, 0.22)'
                    }}
                  >
                    <IconClock size={30} />
                  </Avatar>

                  <Box sx={{ flex: 1 }}>
                    <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
                      <Typography variant="h3" sx={{ fontWeight: 800 }}>
                        {attendance
                          ? attendance.checkOutAtUtc
                            ? 'تم إنهاء يوم العمل'
                            : 'تم تسجيل حضور اليوم'
                          : 'لم يتم تسجيل حضور اليوم بعد'}
                      </Typography>
                      <Chip
                        size="small"
                        color={attendance ? (attendance.checkOutAtUtc ? 'success' : 'primary') : 'warning'}
                        label={attendance ? (attendance.checkOutAtUtc ? 'مكتمل' : 'حاضر الآن') : 'غير مسجّل'}
                        sx={{ fontWeight: 700 }}
                      />
                    </Stack>

                    <Stack direction="row" spacing={2.5} flexWrap="wrap" useFlexGap sx={{ mt: 1, color: 'text.secondary' }}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        وقت الحضور: <b>{formatTime(attendance?.checkInAtUtc || null)}</b>
                      </Typography>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        وقت الانصراف: <b>{formatTime(attendance?.checkOutAtUtc || null)}</b>
                      </Typography>
                    </Stack>

                    <TextField
                      fullWidth
                      size="small"
                      label="ملاحظات اختيارية"
                      placeholder="أضف ملاحظة عند تسجيل الحضور أو الانصراف..."
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                      sx={{ mt: 2, maxWidth: 460 }}
                    />
                  </Box>

                  <Stack direction={{ xs: 'row', md: 'column' }} spacing={1.25} sx={{ flexShrink: 0 }}>
                    <Button
                      variant="contained"
                      color="secondary"
                      disabled={saving || Boolean(attendance)}
                      startIcon={<IconLogin size={18} />}
                      onClick={checkIn}
                      sx={{ borderRadius: 2, fontWeight: 700, minWidth: 150 }}
                    >
                      تسجيل الحضور
                    </Button>
                    <Button
                      variant="outlined"
                      color="error"
                      disabled={saving || !attendance || Boolean(attendance.checkOutAtUtc)}
                      startIcon={<IconLogout size={18} />}
                      onClick={checkOut}
                      sx={{ borderRadius: 2, fontWeight: 700, minWidth: 150 }}
                    >
                      تسجيل الانصراف
                    </Button>
                  </Stack>
                </Stack>
              </MainCard>
            </Grid>

            {/* Berry Purple Summary Card */}
            <Grid size={{ xs: 12, lg: 4 }}>
              <Card
                sx={{
                  height: '100%',
                  borderRadius: 3,
                  color: '#fff',
                  background: 'linear-gradient(135deg, #311b92 0%, #5e35b1 55%, #7e57c2 100%)',
                  boxShadow: '0 10px 26px rgba(49, 27, 146, 0.22)'
                }}
              >
                <CardContent sx={{ p: 2.5, height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Box>
                      <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.84)', fontWeight: 700 }}>
                        إجمالي سجلات التاريخ المحدد
                      </Typography>
                      <Typography variant="h1" sx={{ color: '#fff', fontWeight: 900, mt: 0.5 }}>
                        {records.length}
                      </Typography>
                    </Box>
                    <Avatar sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', width: 48, height: 48 }}>
                      <IconUserCheck size={24} />
                    </Avatar>
                  </Stack>
                  <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    <Chip
                      size="small"
                      label={`انصرف: ${completedCount}`}
                      sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: '#fff', fontWeight: 700 }}
                    />
                    <Chip
                      size="small"
                      label={`متواجد: ${records.length - completedCount}`}
                      sx={{ bgcolor: 'rgba(255,255,255,0.14)', color: '#fff', fontWeight: 700 }}
                    />
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Attendance Log Table */}
          <MainCard
            content={false}
            sx={{ borderRadius: 3, overflow: 'hidden' }}
            title={
              <Stack direction="row" spacing={1} alignItems="center">
                <IconCalendar size={20} />
                <Typography variant="h4" sx={{ fontWeight: 800 }}>
                  سجل الحضور اليومي
                </Typography>
              </Stack>
            }
            secondary={
              <TextField
                size="small"
                type="date"
                value={selectedDate}
                onChange={(e) => {
                  setSelectedDate(e.target.value || todayIso());
                  setPage(0);
                }}
                sx={{ width: 170 }}
              />
            }
          >
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 800 }}>المستخدم</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>الحضور</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>الانصراف</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>الحالة</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>إجراء</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {paginatedRecords.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5} align="center" sx={{ py: 4 }}>
                        <Typography color="text.secondary" sx={{ fontWeight: 600 }}>
                          لا توجد سجلات حضور لهذا التاريخ.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    paginatedRecords.map((record) => (
                      <TableRow key={record.id} hover>
                        <TableCell sx={{ fontWeight: 700 }}>{record.userName}</TableCell>
                        <TableCell sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatTime(record.checkInAtUtc)}</TableCell>
                        <TableCell sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatTime(record.checkOutAtUtc)}</TableCell>
                        <TableCell>
                          <Chip
                            size="small"
                            color={record.checkOutAtUtc ? 'success' : 'primary'}
                            label={record.status || (record.checkOutAtUtc ? 'مكتمل' : 'حاضر')}
                            sx={{ fontWeight: 700 }}
                          />
                        </TableCell>
                        <TableCell><Stack direction="row" spacing={0.5}><Button size="small" onClick={() => openEdit(record)} startIcon={<IconEdit size={15} />}>تعديل</Button><Button size="small" color="error" onClick={() => void remove(record)} startIcon={<IconTrash size={15} />}>حذف</Button></Stack></TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
            {records.length > 0 && (
              <TablePagination
                component="div"
                count={records.length}
                page={page}
                onPageChange={(_, p) => setPage(p)}
                rowsPerPage={rowsPerPage}
                onRowsPerPageChange={(e) => {
                  setRowsPerPage(parseInt(e.target.value, 10));
                  setPage(0);
                }}
                rowsPerPageOptions={[10, 15, 25, 50]}
                labelRowsPerPage="عدد الصفوف:"
                labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`}
                sx={{ borderTop: 1, borderColor: 'divider' }}
              />
            )}
          </MainCard>
        </>
      )}
      <Dialog open={Boolean(editing)} onClose={() => setEditing(null)} fullWidth maxWidth="sm">
        <DialogTitle>تعديل سجل الحضور</DialogTitle>
        <DialogContent dividers><Stack spacing={2} sx={{ pt: 1 }}>
          <TextField required type="datetime-local" label="وقت الحضور" value={editForm.checkIn} onChange={(e) => setEditForm({ ...editForm, checkIn: e.target.value })} InputLabelProps={{ shrink: true }} />
          <TextField type="datetime-local" label="وقت الانصراف" value={editForm.checkOut} onChange={(e) => setEditForm({ ...editForm, checkOut: e.target.value })} InputLabelProps={{ shrink: true }} />
          <TextField label="ملاحظات" value={editForm.notes} onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })} />
        </Stack></DialogContent>
        <DialogActions><Button onClick={() => setEditing(null)}>إلغاء</Button><Button variant="contained" onClick={() => void saveEdit()} disabled={!editForm.checkIn}>حفظ</Button></DialogActions>
      </Dialog>
    </Box>
  );
}
