import { FormEvent, useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
import TextField from '@mui/material/TextField';
import SubCard from 'ui-component/cards/SubCard';
import { EmgProfile, getEmgProfile, updateEmgProfile } from 'api/emg';

export default function PersonalAccount() {
  const [profile, setProfile] = useState<EmgProfile | null>(null); const [displayName, setDisplayName] = useState(''); const [message, setMessage] = useState<string | null>(null); const [saving, setSaving] = useState(false);
  useEffect(() => { getEmgProfile().then((value) => { setProfile(value); setDisplayName(value.displayName); }); }, []);
  const save = async (event: FormEvent) => { event.preventDefault(); setSaving(true); try { setProfile(await updateEmgProfile(displayName)); setMessage('تم حفظ الاسم الظاهر.'); } catch { setMessage('تعذر حفظ البيانات.'); } finally { setSaving(false); } };
  if (!profile) return <Alert severity="info">جاري تحميل بيانات الحساب...</Alert>;
  return <Grid container spacing={3}><Grid size={{ xs: 12, md: 6 }}><SubCard title="البيانات الشخصية"><form onSubmit={save}><Grid container spacing={3}><Grid size={12}><TextField fullWidth label="اسم المستخدم" value={profile.userName} disabled /></Grid><Grid size={12}><TextField fullWidth label="الاسم الظاهر" value={displayName} onChange={(e) => setDisplayName(e.target.value)} /></Grid><Grid size={12}><TextField fullWidth label="البريد الإلكتروني" value={profile.email} disabled /></Grid><Grid size={12}><Button type="submit" variant="contained" disabled={saving}>{saving ? 'جاري الحفظ...' : 'حفظ البيانات'}</Button></Grid>{message && <Grid size={12}><Alert severity="success">{message}</Alert></Grid>}</Grid></form></SubCard></Grid><Grid size={{ xs: 12, md: 6 }}><SubCard title="معلومات الحساب"><Alert severity={profile.mustChangePassword ? 'warning' : 'success'}>{profile.mustChangePassword ? 'يجب تغيير كلمة المرور الحالية.' : 'الحساب يعمل بشكل طبيعي.'}</Alert></SubCard></Grid></Grid>;
}
