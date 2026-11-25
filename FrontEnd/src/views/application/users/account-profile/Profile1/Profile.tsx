import { ChangeEvent, useEffect, useRef, useState } from 'react';
import Grid from '@mui/material/Grid';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import SubCard from 'ui-component/cards/SubCard';
import Avatar from 'ui-component/extended/Avatar';
import Avatar3 from 'assets/images/users/avatar-3.png';
import { getEmgProfile, EmgProfile, uploadEmgProfileImage } from 'api/emg';

export default function Profile() {
  const [profile, setProfile] = useState<EmgProfile | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => { getEmgProfile().then(setProfile).catch(() => undefined); }, []);
  if (!profile) return <Typography>جاري تحميل بيانات البروفايل...</Typography>;
  const changeImage = async (event: ChangeEvent<HTMLInputElement>) => { const file = event.target.files?.[0]; if (!file) return; try { setProfile(await uploadEmgProfileImage(file)); } catch { /* keep current profile if the upload is rejected */ } };
  const imageUrl = profile.profileImagePath || Avatar3;
  return <Grid container spacing={3}><Grid size={{ lg: 4, xs: 12 }}><SubCard title={<Stack direction="row" spacing={2} alignItems="center"><Box onClick={() => inputRef.current?.click()} sx={{ cursor: 'pointer' }}><Avatar alt={profile.displayName} src={imageUrl} /></Box><input ref={inputRef} hidden type="file" accept="image/png,image/jpeg,image/webp" onChange={changeImage} /><Box><Typography variant="subtitle1">{profile.displayName}</Typography><Typography variant="subtitle2">{profile.userName}</Typography></Box><Chip size="small" label="EMG" color="primary" /></Stack>}><Table size="small"><TableBody><TableRow><TableCell>اسم المستخدم</TableCell><TableCell>{profile.userName}</TableCell></TableRow><TableRow><TableCell>البريد</TableCell><TableCell>{profile.email}</TableCell></TableRow><TableRow><TableCell>الحالة</TableCell><TableCell>{profile.mustChangePassword ? 'يجب تغيير كلمة المرور' : 'نشط'}</TableCell></TableRow></TableBody></Table><Typography variant="caption" color="text.secondary">اضغط على الصورة لتغييرها</Typography></SubCard></Grid><Grid size={{ lg: 8, xs: 12 }}><SubCard title="بيانات الحساب"><Typography variant="body1">يمكنك تعديل الاسم الظاهر من تبويب البيانات الشخصية، وتغيير كلمة المرور من التبويب المخصص. اسم المستخدم ثابت ولا يمكن تغييره.</Typography></SubCard></Grid></Grid>;
}
