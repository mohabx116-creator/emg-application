import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  IconButton,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme
} from '@mui/material';
import {
  IconEdit,
  IconKey,
  IconPlus,
  IconRefresh,
  IconSearch,
  IconUserCheck,
  IconUserShield,
  IconUserX,
  IconX
} from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import {
  assignEmgUserProject,
  createEmgUser,
  EmgUser,
  getEmgUsers,
  resetEmgUserPassword,
  unassignEmgUserProject,
  updateEmgUser
} from 'api/emg';
import { useEmgProjectsCache } from 'hooks/useEmgData';

const roleOptions: Array<[string, string]> = [
  ['user', 'مستخدم'],
  ['engineer', 'مهندس موقع'],
  ['project_manager', 'مدير مشاريع'],
  ['accountant', 'محاسب'],
  ['director', 'مدير عام'],
  ['admin', 'مسؤول النظام']
];

const roleLabel = (code: string) => roleOptions.find(([key]) => key === code)?.[1] || code;

type UserForm = {
  email: string;
  displayName: string;
  password: string;
  roles: string[];
  projectIds: number[];
  isActive: boolean;
};

const emptyForm: UserForm = {
  email: '',
  displayName: '',
  password: '',
  roles: ['user'],
  projectIds: [],
  isActive: true
};

export default function EmgUsers() {
  const theme = useTheme();
  const downSM = useMediaQuery(theme.breakpoints.down('sm'));
  const downMD = useMediaQuery(theme.breakpoints.down('md'));
  const { user } = useAuth();
  const { data: projects = [] } = useEmgProjectsCache(true);

  const [users, setUsers] = useState<EmgUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<EmgUser | null>(null);
  const [form, setForm] = useState<UserForm>(emptyForm);
  const [resetTarget, setResetTarget] = useState<EmgUser | null>(null);
  const [resetPasswordValue, setResetPasswordValue] = useState('');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [saving, setSaving] = useState(false);

  const canViewUsers = user?.role === 'admin' || user?.permissions?.includes('users.view') || user?.permissions?.includes('users.manage');

  const projectMap = useMemo(() => new Map(projects.map((p) => [p.id, p])), [projects]);

  const load = () => {
    setLoading(true);
    getEmgUsers()
      .then((loadedUsers) => setUsers(loadedUsers))
      .catch(() => setMessage({ type: 'error', text: 'تعذر تحميل المستخدمين. تحقق من صلاحيات الحساب.' }))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (canViewUsers) load();
  }, [canViewUsers]);

  const filteredUsers = useMemo(() => {
    const q = search.trim().toLowerCase();
    return users.filter((item) => {
      if (statusFilter === 'active' && !item.isActive) return false;
      if (statusFilter === 'inactive' && item.isActive) return false;
      if (!q) return true;
      const rolesText = item.roles.map(roleLabel).join(' ');
      return `${item.displayName} ${item.email} ${rolesText}`.toLowerCase().includes(q);
    });
  }, [users, search, statusFilter]);

  const closeDialog = () => {
    setOpen(false);
    setEditing(null);
    setForm(emptyForm);
  };

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm);
    setOpen(true);
  };

  const openEdit = (item: EmgUser) => {
    setEditing(item);
    setForm({
      email: item.email,
      displayName: item.displayName,
      password: '',
      roles: item.roles.length ? item.roles : ['user'],
      projectIds: [...item.projectIds],
      isActive: item.isActive
    });
    setOpen(true);
  };

  const saveUser = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      if (editing) {
        await updateEmgUser(editing.id, {
          email: form.email.trim(),
          displayName: form.displayName.trim(),
          roles: form.roles,
          role: form.roles[0] || 'user',
          isActive: form.isActive
        });

        // Sync project assignments in one save
        const toAdd = form.projectIds.filter((pid) => !editing.projectIds.includes(pid));
        const toRemove = editing.projectIds.filter((pid) => !form.projectIds.includes(pid));
        for (const pid of toAdd) await assignEmgUserProject(editing.id, pid);
        for (const pid of toRemove) await unassignEmgUserProject(editing.id, pid);

        setMessage({ type: 'success', text: 'تم تحديث بيانات المستخدم وأدواره والمشروعات المسندة بنجاح.' });
      } else {
        // Auto-generate internal userName from email prefix so technical identifier stays hidden from form
        const derivedUserName = form.email
          .trim()
          .split('@')[0]
          .replace(/[^a-zA-Z0-9._-]/g, '')
          .slice(0, 32) || `user${Date.now().toString().slice(-5)}`;
        const created = await createEmgUser({
          userName: derivedUserName,
          email: form.email.trim(),
          displayName: form.displayName.trim(),
          password: form.password || undefined,
          roles: form.roles,
          role: form.roles[0] || 'user',
          isActive: true
        });
        for (const pid of form.projectIds) {
          await assignEmgUserProject(created.id, pid);
        }
        setMessage({ type: 'success', text: 'تم إنشاء الحساب وتعيين الأدوار والمشروعات بنجاح.' });
      }
      closeDialog();
      load();
    } catch (error: any) {
      setMessage({ type: 'error', text: error?.response?.data?.message || error?.message || 'تعذر حفظ بيانات المستخدم.' });
    } finally {
      setSaving(false);
    }
  };

  const handleResetPassword = async () => {
    if (!resetTarget) return;
    setSaving(true);
    try {
      await resetEmgUserPassword(resetTarget.id, resetPasswordValue.trim() || undefined);
      setMessage({
        type: 'success',
        text: resetPasswordValue.trim()
          ? `تم تعيين كلمة مرور جديدة لحساب «${resetTarget.displayName}» بنجاح. سيُطلب تغييرها عند الدخول القادم.`
          : `تمت إعادة كلمة مرور حساب «${resetTarget.displayName}» إلى القيمة الافتراضية بنجاح.`
      });
      setResetTarget(null);
      setResetPasswordValue('');
      load();
    } catch (error: any) {
      setMessage({
        type: 'error',
        text: error?.response?.data?.message || 'تعذر إعادة تعيين كلمة المرور. تأكد من استيفاء شروط كلمة المرور القوية.'
      });
    } finally {
      setSaving(false);
    }
  };

  const toggleActive = async (item: EmgUser) => {
    const actionLabel = item.isActive ? 'تعطيل' : 'تفعيل';
    if (item.isActive && !window.confirm(`هل تريد تعطيل حساب «${item.displayName}»؟ لن يتمكن المستخدم من تسجيل الدخول بعد التعطيل.`)) {
      return;
    }
    try {
      await updateEmgUser(item.id, {
        email: item.email,
        displayName: item.displayName,
        roles: item.roles,
        role: item.roles[0] || 'user',
        isActive: !item.isActive
      });
      setMessage({ type: 'success', text: `تم ${actionLabel} حساب «${item.displayName}» بنجاح.` });
      load();
    } catch {
      setMessage({ type: 'error', text: `تعذر ${actionLabel} الحساب.` });
    }
  };

  if (!canViewUsers) return <Alert severity="error">ليس لديك صلاحية عرض المستخدمين.</Alert>;

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={2} sx={{ mb: 2.5 }}>
        <Box>
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            إدارة المستخدمين
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.35 }}>
            إدارة الحسابات، الأدوار الوظيفية، حالة الحساب، والمشروعات المسندة
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={load} startIcon={<IconRefresh size={16} />} sx={{ borderRadius: 2, fontWeight: 700 }}>
            تحديث
          </Button>
          <Button variant="contained" startIcon={<IconPlus size={18} />} onClick={openCreate} sx={{ borderRadius: 2, fontWeight: 700 }}>
            إضافة مستخدم
          </Button>
        </Stack>
      </Stack>

      {message && (
        <Alert severity={message.type} sx={{ mb: 2, borderRadius: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      <MainCard
        content={false}
        sx={{ borderRadius: 3, overflow: 'hidden' }}
        title={
          <Stack direction="row" spacing={1} alignItems="center">
            <IconUserShield size={20} />
            <Typography variant="h4" sx={{ fontWeight: 800 }}>
              المستخدمون ({filteredUsers.length})
            </Typography>
          </Stack>
        }
      >
        {/* Search & Status Filter Toolbar */}
        <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'action.hover' }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', sm: 'center' }}>
            <TextField
              size="small"
              placeholder="ابحث بالاسم الكامل أو البريد الإلكتروني أو الدور الوظيفي..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              sx={{ flex: 1, bgcolor: 'background.paper', borderRadius: 1 }}
              InputProps={{
                startAdornment: (
                  <InputAdornment position="start">
                    <IconSearch size={17} />
                  </InputAdornment>
                )
              }}
            />
            <Stack direction="row" spacing={0.75}>
              {[
                ['all', 'الكل'],
                ['active', 'نشط'],
                ['inactive', 'معطّل']
              ].map(([key, label]) => (
                <Chip
                  key={key}
                  clickable
                  size="small"
                  label={label}
                  color={statusFilter === key ? 'primary' : 'default'}
                  variant={statusFilter === key ? 'filled' : 'outlined'}
                  onClick={() => setStatusFilter(key as 'all' | 'active' | 'inactive')}
                  sx={{ fontWeight: 700 }}
                />
              ))}
            </Stack>
          </Stack>
        </Box>

        {loading ? (
          <Stack spacing={1.25} sx={{ p: 2 }}>
            {[0, 1, 2, 3].map((i) => (
              <Skeleton key={i} variant="rounded" height={54} />
            ))}
          </Stack>
        ) : filteredUsers.length === 0 ? (
          <Typography color="text.secondary" sx={{ py: 5, textAlign: 'center' }}>
            لا يوجد مستخدمون مطابقون للبحث.
          </Typography>
        ) : downMD ? (
          <Stack spacing={1.25} sx={{ p: 1.5 }}>
            {filteredUsers.map((item) => (
              <Card key={item.id} variant="outlined" sx={{ borderRadius: 2.5 }}>
                <CardContent sx={{ p: 1.75, '&:last-child': { pb: 1.75 } }}>
                  <Stack spacing={1}>
                    <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
                      <Box>
                        <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
                          {item.displayName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {item.email}
                        </Typography>
                      </Box>
                      <Chip size="small" label={item.isActive ? 'نشط' : 'غير نشط'} color={item.isActive ? 'success' : 'default'} />
                    </Stack>
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                      {item.roles.map((r) => (
                        <Chip key={r} size="small" variant="outlined" label={roleLabel(r)} />
                      ))}
                    </Stack>
                    <Typography variant="caption" color="text.secondary">
                      المشروعات المسندة:{' '}
                      {item.projectIds.length > 0
                        ? item.projectIds.map((pid) => projectMap.get(pid)?.name || `#${pid}`).join('، ')
                        : 'لا يوجد'}
                    </Typography>
                    <Stack direction="row" spacing={1}>
                      <Button fullWidth size="small" variant="outlined" startIcon={<IconEdit size={15} />} onClick={() => openEdit(item)}>
                        تعديل
                      </Button>
                      <Button fullWidth size="small" variant="outlined" color="warning" startIcon={<IconKey size={15} />} onClick={() => setResetTarget(item)}>
                        كلمة المرور
                      </Button>
                      <Button size="small" variant="outlined" color={item.isActive ? 'error' : 'success'} onClick={() => void toggleActive(item)}>
                        {item.isActive ? 'تعطيل' : 'تفعيل'}
                      </Button>
                    </Stack>
                  </Stack>
                </CardContent>
              </Card>
            ))}
          </Stack>
        ) : (
          <TableContainer sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 800 }}>الاسم الكامل</TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>البريد الإلكتروني لتسجيل الدخول</TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>الدور الوظيفي</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 800 }}>
                    حالة الحساب
                  </TableCell>
                  <TableCell sx={{ fontWeight: 800 }}>المشروعات المسندة</TableCell>
                  <TableCell align="center" sx={{ fontWeight: 800 }}>
                    الإجراءات
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredUsers.map((item) => (
                  <TableRow key={item.id} hover>
                    <TableCell sx={{ fontWeight: 800 }}>
                      {item.displayName}
                      {item.mustChangePassword && (
                        <Typography variant="caption" color="warning.main" sx={{ display: 'block' }}>
                          يتطلب تغيير كلمة المرور
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell sx={{ direction: 'ltr', textAlign: 'end' }}>{item.email}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                        {(item.roles.length ? item.roles : ['user']).map((r) => (
                          <Chip key={r} size="small" variant="outlined" label={roleLabel(r)} sx={{ fontWeight: 600 }} />
                        ))}
                      </Stack>
                    </TableCell>
                    <TableCell align="center">
                      <Chip size="small" label={item.isActive ? 'نشط' : 'غير نشط'} color={item.isActive ? 'success' : 'default'} sx={{ fontWeight: 700 }} />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 260 }}>
                      {item.projectIds.length === 0 ? (
                        <Typography variant="caption" color="text.secondary">
                          —
                        </Typography>
                      ) : (
                        <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                          {item.projectIds.slice(0, 3).map((pid) => (
                            <Chip key={pid} size="small" label={projectMap.get(pid)?.name || `#${pid}`} />
                          ))}
                          {item.projectIds.length > 3 && <Chip size="small" variant="outlined" label={`+${item.projectIds.length - 3}`} />}
                        </Stack>
                      )}
                    </TableCell>
                    <TableCell align="center" sx={{ whiteSpace: 'nowrap' }}>
                      <Stack direction="row" spacing={0.5} justifyContent="center">
                        <Tooltip title="تعديل المستخدم والمشروعات">
                          <IconButton size="small" color="primary" onClick={() => openEdit(item)}>
                            <IconEdit size={18} />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title="إعادة تعيين كلمة المرور">
                          <IconButton size="small" color="warning" onClick={() => setResetTarget(item)}>
                            <IconKey size={18} />
                          </IconButton>
                        </Tooltip>
                        <Tooltip title={item.isActive ? 'تعطيل الحساب بأمان' : 'تفعيل الحساب'}>
                          <IconButton size="small" color={item.isActive ? 'error' : 'success'} onClick={() => void toggleActive(item)}>
                            {item.isActive ? <IconUserX size={18} /> : <IconUserCheck size={18} />}
                          </IconButton>
                        </Tooltip>
                      </Stack>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </MainCard>

      {/* Dialog: Add / Edit User (Technical internal userName hidden) */}
      <Dialog open={open} onClose={closeDialog} fullWidth maxWidth="sm" fullScreen={downSM}>
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Typography variant="h3" sx={{ fontWeight: 800 }}>
            {editing ? 'تعديل بيانات المستخدم' : 'إضافة مستخدم جديد'}
          </Typography>
          <IconButton onClick={closeDialog} size="small">
            <IconX size={20} />
          </IconButton>
        </DialogTitle>
        <Box component="form" onSubmit={saveUser}>
          <DialogContent dividers>
            <Stack spacing={2} sx={{ pt: 0.5 }}>
              <TextField
                required
                fullWidth
                label="الاسم الكامل"
                value={form.displayName}
                onChange={(event) => setForm({ ...form, displayName: event.target.value })}
              />
              <TextField
                required
                fullWidth
                type="email"
                label="البريد الإلكتروني لتسجيل الدخول"
                value={form.email}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
              />
              {!editing && (
                <TextField
                  fullWidth
                  type="password"
                  label="كلمة المرور الأولية"
                  helperText="اتركها فارغة لاستخدام كلمة المرور الافتراضية المؤقتة"
                  value={form.password}
                  onChange={(event) => setForm({ ...form, password: event.target.value })}
                />
              )}
              <FormControl fullWidth>
                <InputLabel>الدور الوظيفي</InputLabel>
                <Select
                  multiple
                  value={form.roles}
                  label="الدور الوظيفي"
                  renderValue={(selected) => (
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                      {(selected as string[]).map((value) => (
                        <Chip key={value} size="small" label={roleLabel(value)} />
                      ))}
                    </Stack>
                  )}
                  onChange={(event) =>
                    setForm({ ...form, roles: typeof event.target.value === 'string' ? event.target.value.split(',') : event.target.value })
                  }
                >
                  {roleOptions.map(([value, label]) => (
                    <MenuItem key={value} value={value}>
                      {label}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControl fullWidth>
                <InputLabel>المشروعات المسندة</InputLabel>
                <Select
                  multiple
                  value={form.projectIds}
                  label="المشروعات المسندة"
                  renderValue={(selected) => (
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                      {(selected as number[]).map((pid) => (
                        <Chip key={pid} size="small" label={projectMap.get(pid)?.name || `#${pid}`} />
                      ))}
                    </Stack>
                  )}
                  onChange={(event) => setForm({ ...form, projectIds: event.target.value as number[] })}
                >
                  {projects.map((p) => (
                    <MenuItem key={p.id} value={p.id}>
                      {p.name} ({p.code})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {editing && (
                <FormControl fullWidth>
                  <InputLabel>حالة الحساب</InputLabel>
                  <Select value={form.isActive ? 'active' : 'inactive'} label="حالة الحساب" onChange={(event) => setForm({ ...form, isActive: event.target.value === 'active' })}>
                    <MenuItem value="active">نشط</MenuItem>
                    <MenuItem value="inactive">غير نشط (معطّل)</MenuItem>
                  </Select>
                </FormControl>
              )}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={closeDialog}>إلغاء</Button>
            <Button type="submit" disabled={saving || !form.roles.length} variant="contained" sx={{ fontWeight: 700 }}>
              {saving ? 'جاري الحفظ...' : 'حفظ التغييرات'}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>

      {/* Dialog: Reset Password with clear Arabic feedback */}
      <Dialog open={Boolean(resetTarget)} onClose={() => setResetTarget(null)} fullWidth maxWidth="xs">
        <DialogTitle>إعادة تعيين كلمة المرور</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography variant="body2">
              حساب المستخدم: <b>{resetTarget?.displayName}</b> ({resetTarget?.email})
            </Typography>
            <TextField
              fullWidth
              type="password"
              label="كلمة المرور الجديدة (اختياري)"
              helperText="اترك الحقل فارغاً لاستخدام كلمة المرور الافتراضية المؤقتة. سيُطلب من المستخدم تغييرها عند تسجيل الدخول القادم."
              value={resetPasswordValue}
              onChange={(e) => setResetPasswordValue(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetTarget(null)} disabled={saving}>
            إلغاء
          </Button>
          <Button variant="contained" color="warning" disabled={saving} onClick={() => void handleResetPassword()}>
            {saving ? 'جاري التنفيذ...' : 'تأكيد إعادة التعيين'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
