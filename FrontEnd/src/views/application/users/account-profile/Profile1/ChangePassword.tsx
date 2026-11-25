import { FormEvent, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
import TextField from '@mui/material/TextField';
import SubCard from 'ui-component/cards/SubCard';
import AnimateButton from 'ui-component/extended/AnimateButton';
import { changeEmgPassword } from 'api/emg';

type ChangePasswordProps = {
  forced?: boolean;
  onCompleted?: () => void;
};

export default function ChangePassword({ forced = false, onCompleted }: ChangePasswordProps) {
  const [currentPassword, setCurrentPassword] = useState(''); const [newPassword, setNewPassword] = useState(''); const [confirmPassword, setConfirmPassword] = useState(''); const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null); const [saving, setSaving] = useState(false);
  const submit = async (event: FormEvent) => { event.preventDefault(); if (newPassword.length < 8 || newPassword !== confirmPassword) { setMessage({ type: 'error', text: 'تأكد أن كلمة المرور 8 أحرف على الأقل وأن التأكيد مطابق.' }); return; } setSaving(true); try { await changeEmgPassword(currentPassword, newPassword); setCurrentPassword(''); setNewPassword(''); setConfirmPassword(''); setMessage({ type: 'success', text: 'تم تغيير كلمة المرور بنجاح.' }); onCompleted?.(); } catch (error: any) { setMessage({ type: 'error', text: error?.message || 'كلمة المرور الحالية غير صحيحة.' }); } finally { setSaving(false); } };
  return <Grid container spacing={3}><Grid size={12}>{message && <Alert severity={message.type}>{message.text}</Alert>}</Grid><Grid size={12}><SubCard title={forced ? 'تحديث كلمة المرور قبل المتابعة' : 'تغيير كلمة المرور'}><form onSubmit={submit} noValidate autoComplete="off"><Grid container spacing={3} sx={{ mb: 1.75 }}><Grid size={{ xs: 12, md: 6 }}><TextField required type="password" fullWidth label="كلمة المرور الحالية" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} /></Grid></Grid><Grid container spacing={3} sx={{ mb: 1.75 }}><Grid size={{ xs: 12, md: 6 }}><TextField required type="password" fullWidth label="كلمة المرور الجديدة" helperText="8 أحرف على الأقل" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} /></Grid><Grid size={{ xs: 12, md: 6 }}><TextField required type="password" fullWidth label="تأكيد كلمة المرور" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} /></Grid></Grid><Grid container sx={{ justifyContent: 'flex-end', mt: 3 }}><Grid><AnimateButton><Button type="submit" variant="contained" disabled={saving}>{saving ? 'جاري الحفظ...' : 'تغيير كلمة المرور'}</Button></AnimateButton></Grid></Grid></form></SubCard></Grid></Grid>;
}
