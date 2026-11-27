import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Grid, Stack, TextField, Typography } from '@mui/material';
import { IconPlus, IconRefresh, IconTruckDelivery } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import { createEmgSupplier, EmgSupplier, getEmgSuppliers } from 'api/emg';
import { Link } from 'react-router-dom';
import useAuth from 'hooks/useAuth';

const money = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const formatMoney = (value: number) => `${money.format(value || 0)} ج.م`;

type SupplierForm = { name: string; supplierType: string; phone: string; taxNumber: string; notes: string };
const emptyForm: SupplierForm = { name: '', supplierType: '', phone: '', taxNumber: '', notes: '' };

export default function EmgSuppliers() {
  const { user } = useAuth();
  const canCreate = user?.permissions?.includes('suppliers.create');
  const [suppliers, setSuppliers] = useState<EmgSupplier[]>([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState<SupplierForm>(emptyForm);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ severity: 'success' | 'error'; text: string } | null>(null);

  const load = () => { setLoading(true); getEmgSuppliers().then(setSuppliers).catch(() => setMessage({ severity: 'error', text: 'تعذر تحميل قائمة الموردين.' })).finally(() => setLoading(false)); };
  useEffect(() => { load(); }, []);

  const filtered = useMemo(() => suppliers.filter((supplier) => `${supplier.name} ${supplier.supplierType} ${supplier.phone}`.toLowerCase().includes(search.toLowerCase())), [suppliers, search]);
  const potentialDuplicates = useMemo(() => {
    const normalize = (value: string) => value.trim().toLowerCase().replace(/[أإآ]/g, 'ا').replace(/ة/g, 'ه').replace(/[ـ:()\-_\/]/g, ' ').replace(/\s+/g, ' ');
    const name = normalize(form.name);
    const phone = form.phone.replace(/\D/g, '');
    const taxNumber = normalize(form.taxNumber);
    if (!name && !phone && !taxNumber) return [];
    return suppliers.filter((supplier) => {
      const supplierName = normalize(supplier.name);
      const supplierPhone = supplier.phone.replace(/\D/g, '');
      const supplierTax = normalize(supplier.taxNumber);
      const nameMatch = name.length >= 3 && (supplierName.includes(name) || name.includes(supplierName));
      const phoneMatch = phone.length >= 7 && supplierPhone === phone;
      const taxMatch = taxNumber.length >= 3 && supplierTax === taxNumber;
      return nameMatch || phoneMatch || taxMatch;
    }).slice(0, 5);
  }, [form.name, form.phone, form.taxNumber, suppliers]);
  const totals = useMemo(() => suppliers.reduce((result, supplier) => ({ supplies: result.supplies + supplier.totalSupplies, paid: result.paid + supplier.totalPaid, remaining: result.remaining + supplier.remaining }), { supplies: 0, paid: 0, remaining: 0 }), [suppliers]);

  const submit = async (event: FormEvent) => {
    event.preventDefault(); setSaving(true); setMessage(null);
    try { await createEmgSupplier(form); setForm(emptyForm); setOpen(false); setMessage({ severity: 'success', text: 'تم إضافة المورد بنجاح.' }); load(); }
    catch (error: any) { setMessage({ severity: 'error', text: error?.message || 'تعذر إضافة المورد.' }); }
    finally { setSaving(false); }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={2} sx={{ mb: 3 }}>
        <Box><Typography variant="h2">الموردين</Typography><Typography color="text.secondary">قائمة الموردين وتوريداتهم ومدفوعاتهم وحساباتهم</Typography></Box>
        {canCreate && <Button variant="contained" startIcon={<IconPlus size={18} />} onClick={() => setOpen(true)}>إضافة مورد</Button>}
      </Stack>
      {message && <Alert severity={message.severity} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 4 }}><MainCard title="عدد الموردين"><Typography variant="h3">{suppliers.length}</Typography></MainCard></Grid>
        <Grid size={{ xs: 12, md: 4 }}><MainCard title="إجمالي التوريدات"><Typography variant="h3" color="primary">{formatMoney(totals.supplies)}</Typography></MainCard></Grid>
        <Grid size={{ xs: 12, md: 4 }}><MainCard title="إجمالي المتبقي"><Typography variant="h3" color="warning.main">{formatMoney(totals.remaining)}</Typography></MainCard></Grid>
      </Grid>

      <MainCard title="قائمة الموردين" secondary={<Stack direction="row" spacing={1}><TextField size="small" placeholder="بحث في الموردين" value={search} onChange={(event) => setSearch(event.target.value)} /><Button onClick={load} startIcon={<IconRefresh size={16} />}>تحديث</Button></Stack>}>
        {loading ? <Box sx={{ minHeight: 180, display: 'grid', placeItems: 'center' }}><CircularProgress /></Box> : <Grid container spacing={2}>{filtered.map((supplier) => <Grid key={supplier.id} size={{ xs: 12, md: 6, xl: 4 }}><Card component={Link} to={`/emg/suppliers/${supplier.id}`} variant="outlined" sx={{ textDecoration: 'none', color: 'inherit', display: 'block', '&:hover': { borderColor: 'primary.main' } }}><CardContent><Stack direction="row" justifyContent="space-between" alignItems="flex-start"><Stack direction="row" spacing={1.5} alignItems="center"><Box sx={{ p: 1, bgcolor: 'primary.lighter', color: 'primary.main', borderRadius: 2, display: 'flex' }}><IconTruckDelivery size={22} /></Box><Box><Typography variant="h4">{supplier.name}</Typography><Typography variant="body2" color="text.secondary">{supplier.supplierType || 'نوع غير محدد'}</Typography></Box></Stack><Chip size="small" label={supplier.isActive ? 'نشط' : 'غير نشط'} color={supplier.isActive ? 'success' : 'default'} /></Stack><Stack spacing={0.75} sx={{ mt: 2 }}><Stack direction="row" justifyContent="space-between"><Typography color="text.secondary">التوريدات</Typography><Typography>{formatMoney(supplier.totalSupplies)}</Typography></Stack><Stack direction="row" justifyContent="space-between"><Typography color="text.secondary">المدفوع</Typography><Typography color="success.main">{formatMoney(supplier.totalPaid)}</Typography></Stack><Stack direction="row" justifyContent="space-between"><Typography color="text.secondary">المتبقي</Typography><Typography fontWeight={700} color="warning.main">{formatMoney(supplier.remaining)}</Typography></Stack></Stack><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>{supplier.phone || 'بدون هاتف'} · {supplier.suppliesCount} توريدة</Typography></CardContent></Card></Grid>)}</Grid>}
        {!loading && filtered.length === 0 && <Typography color="text.secondary">لا يوجد موردون مطابقون للبحث.</Typography>}
      </MainCard>

      <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm"><DialogTitle>إضافة مورد جديد</DialogTitle><Box component="form" onSubmit={submit}><DialogContent><Stack spacing={2} sx={{ pt: 1 }}><TextField required label="اسم المورد" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />{potentialDuplicates.length > 0 && <Alert severity="warning">يوجد موردون محتملون بنفس البيانات. راجع القائمة قبل إنشاء سجل جديد: {potentialDuplicates.map((supplier) => supplier.name).join('، ')}</Alert>}<TextField label="نوع المورد" placeholder="خامات، معدات، عمالة..." value={form.supplierType} onChange={(event) => setForm({ ...form, supplierType: event.target.value })} /><TextField label="رقم الهاتف" value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} /><TextField label="الرقم الضريبي" value={form.taxNumber} onChange={(event) => setForm({ ...form, taxNumber: event.target.value })} /><TextField label="ملاحظات" multiline minRows={2} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></Stack></DialogContent><DialogActions><Button onClick={() => setOpen(false)}>إلغاء</Button><Button type="submit" variant="contained" disabled={saving}>{saving ? 'جاري الحفظ...' : 'حفظ المورد'}</Button></DialogActions></Box></Dialog>
    </Box>
  );
}
