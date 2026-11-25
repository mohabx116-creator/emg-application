import { SyntheticEvent, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

// material-ui
import Button from '@mui/material/Button';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import Grid from '@mui/material/Grid';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import InputLabel from '@mui/material/InputLabel';
import OutlinedInput from '@mui/material/OutlinedInput';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// third party
import * as Yup from 'yup';
import { Formik } from 'formik';

// project imports
import useAuth from 'hooks/useAuth';
import useScriptRef from 'hooks/useScriptRef';
import axios from 'utils/axios';
import AnimateButton from 'ui-component/extended/AnimateButton';
import CustomFormControl from 'ui-component/extended/Form/CustomFormControl';
import { strengthColor, strengthIndicator } from 'utils/password-strength';

import { dispatch } from 'store';
import { openSnackbar } from 'store/slices/snackbar';

// assets
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

// types
import { StringColorProps } from 'types';

// ========================|| JWT - RESET PASSWORD ||======================== //

export default function AuthResetPassword({ link, ...others }: { link?: string }) {
  const navigate = useNavigate();
  const scriptedRef = useScriptRef();

  const [showPassword, setShowPassword] = useState(false);
  const [strength, setStrength] = useState(0);
  const [level, setLevel] = useState<StringColorProps>();

  const { isLoggedIn } = useAuth();

  const handleClickShowPassword = () => {
    setShowPassword(!showPassword);
  };

  const handleMouseDownPassword = (event: SyntheticEvent) => {
    event.preventDefault();
  };

  const changePassword = (value: string) => {
    const temp = strengthIndicator(value);
    setStrength(temp);
    setLevel(strengthColor(temp));
  };

  useEffect(() => {
    changePassword('');
  }, []);

  const [searchParams] = useSearchParams();
  const authParam = searchParams.get('auth');
  // Reset credentials must remain in the explicit one-time URL, never in browser storage.
  const initialEmail = searchParams.get('email') || '';
  const initialToken = searchParams.get('token') || '';

  return (
    <Formik
      initialValues={{
        email: initialEmail,
        token: initialToken,
        password: '',
        confirmPassword: '',
        submit: null
      }}
      validationSchema={Yup.object().shape({
        email: Yup.string().email('أدخل بريدًا إلكترونيًا صحيحًا').required('البريد الإلكتروني مطلوب'),
        token: Yup.string().required('رمز الاسترداد مطلوب'),
        password: Yup.string().max(255).required('كلمة المرور مطلوبة'),
        confirmPassword: Yup.string()
          .required('تأكيد كلمة المرور مطلوب')
          .test('confirmPassword', 'كلمتا المرور غير متطابقتين', (confirmPassword, yup) => yup.parent.password === confirmPassword)
      })}
      onSubmit={async (values, { setErrors, setStatus, setSubmitting }) => {
        try {
          await axios.post('/api/account/password-reset/confirm', {
            email: values.email.trim(),
            token: values.token.trim(),
            newPassword: values.password
          });
          if (scriptedRef.current) {
            setStatus({ success: true });
            setSubmitting(false);

            dispatch(
              openSnackbar({
                open: true,
                message: 'تم تغيير كلمة المرور بنجاح.',
                variant: 'alert',
                alert: {
                  color: 'success'
                },
                close: false
              })
            );

            setTimeout(() => {
              navigate(isLoggedIn ? `/pages/login/${link || 'login3'}` : authParam ? `/login?auth=${authParam}` : '/login', {
                replace: true
              });
            }, 1500);
          }
        } catch (err: any) {
          console.error(err);
          if (scriptedRef.current) {
            setStatus({ success: false });
            setErrors({ submit: err.message });
            setSubmitting(false);
          }
        }
      }}
    >
      {({ errors, handleBlur, handleChange, handleSubmit, isSubmitting, touched, values }) => (
        <form noValidate onSubmit={handleSubmit} {...others}>
          <CustomFormControl fullWidth error={Boolean(touched.email && errors.email)}>
            <InputLabel htmlFor="outlined-adornment-email-reset">البريد الإلكتروني أو اسم المستخدم</InputLabel>
            <OutlinedInput id="outlined-adornment-email-reset" type="email" value={values.email} name="email" onBlur={handleBlur} onChange={handleChange} label="البريد الإلكتروني أو اسم المستخدم" />
            {touched.email && errors.email && <FormHelperText error>{errors.email}</FormHelperText>}
          </CustomFormControl>

          <CustomFormControl fullWidth error={Boolean(touched.token && errors.token)}>
            <InputLabel htmlFor="outlined-adornment-token-reset">رمز الاسترداد</InputLabel>
            <OutlinedInput id="outlined-adornment-token-reset" value={values.token} name="token" onBlur={handleBlur} onChange={handleChange} label="رمز الاسترداد" />
            {touched.token && errors.token && <FormHelperText error>{errors.token}</FormHelperText>}
          </CustomFormControl>

          <CustomFormControl fullWidth error={Boolean(touched.password && errors.password)}>
            <InputLabel htmlFor="outlined-adornment-password-reset">كلمة المرور الجديدة</InputLabel>
            <OutlinedInput
              id="outlined-adornment-password-reset"
              type={showPassword ? 'text' : 'password'}
              value={values.password}
              name="password"
              onBlur={handleBlur}
              onChange={(e) => {
                handleChange(e);
                changePassword(e.target.value);
              }}
              endAdornment={
                <InputAdornment position="end">
                  <IconButton
                    aria-label="toggle password visibility"
                    onClick={handleClickShowPassword}
                    onMouseDown={handleMouseDownPassword}
                    edge="end"
                    size="large"
                  >
                    {showPassword ? <Visibility /> : <VisibilityOff />}
                  </IconButton>
                </InputAdornment>
              }
            />
          </CustomFormControl>
          {touched.password && errors.password && (
            <FormControl fullWidth>
              <FormHelperText error id="standard-weight-helper-text-reset">
                {errors.password}
              </FormHelperText>
            </FormControl>
          )}
          {strength !== 0 && (
            <FormControl fullWidth>
              <Box sx={{ mb: 2 }}>
                <Grid container spacing={2} sx={{ alignItems: 'center' }}>
                  <Grid>
                    <Box
                      sx={{
                        width: 85,
                        height: 8,
                        borderRadius: '7px',
                        bgcolor: level?.color
                      }}
                    />
                  </Grid>
                  <Grid>
                    <Typography variant="subtitle1" sx={{ fontSize: '0.75rem' }}>
                      {level?.label}
                    </Typography>
                  </Grid>
                </Grid>
              </Box>
            </FormControl>
          )}

          <CustomFormControl fullWidth error={Boolean(touched.confirmPassword && errors.confirmPassword)}>
            <InputLabel htmlFor="outlined-adornment-confirm-password">تأكيد كلمة المرور</InputLabel>
            <OutlinedInput
              id="outlined-adornment-confirm-password"
              type="password"
              value={values.confirmPassword}
              name="confirmPassword"
              label="تأكيد كلمة المرور"
              onBlur={handleBlur}
              onChange={handleChange}
            />
          </CustomFormControl>

          {touched.confirmPassword && errors.confirmPassword && (
            <FormControl fullWidth>
              <FormHelperText error id="standard-weight-helper-text-confirm-password">
                {' '}
                {errors.confirmPassword}{' '}
              </FormHelperText>
            </FormControl>
          )}

          {errors.submit && (
            <Box sx={{ mt: 3 }}>
              <FormHelperText error>{errors.submit}</FormHelperText>
            </Box>
          )}
          <Box sx={{ mt: 1 }}>
            <AnimateButton>
              <Button disableElevation disabled={isSubmitting} fullWidth size="large" type="submit" variant="contained" color="secondary">
                تغيير كلمة المرور
              </Button>
            </AnimateButton>
          </Box>
        </form>
      )}
    </Formik>
  );
}
