import { MouseEvent, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';

// material-ui
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormHelperText from '@mui/material/FormHelperText';
import Grid from '@mui/material/Grid';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import InputLabel from '@mui/material/InputLabel';
import OutlinedInput from '@mui/material/OutlinedInput';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';

// third party
import * as Yup from 'yup';
import { Formik } from 'formik';

// project imports
import AnimateButton from 'ui-component/extended/AnimateButton';
import CustomFormControl from 'ui-component/extended/Form/CustomFormControl';
import useAuth from 'hooks/useAuth';
import useConfig from 'hooks/useConfig';
import useScriptRef from 'hooks/useScriptRef';
import { getLoginErrorMessage } from 'auth/loginResponse';

// assets
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

// ===============================|| JWT - LOGIN ||=============================== //

export default function JWTLogin({ ...others }) {
  const { login, isLoggedIn } = useAuth();
  const {
    state: { i18n }
  } = useConfig();
  const isAr = i18n === 'ar';
  const scriptedRef = useScriptRef();

  const [checked, setChecked] = useState(true);

  const [showPassword, setShowPassword] = useState(false);
  const handleClickShowPassword = () => {
    setShowPassword(!showPassword);
  };

  const handleMouseDownPassword = (event: MouseEvent) => {
    event.preventDefault()!;
  };

  const [searchParams] = useSearchParams();
  const authParam = searchParams.get('auth');

  return (
    <Formik
      initialValues={{
        email: '',
        password: '',
        submit: null
      }}
      validationSchema={Yup.object().shape({
        email: Yup.string()
          .trim()
          .max(255, isAr ? 'البيان يجب ألا يتجاوز 255 حرفًا' : 'Must be at most 255 characters')
          .required(isAr ? 'البريد الإلكتروني أو اسم المستخدم مطلوب' : 'Email or username is required'),
        password: Yup.string()
          .required(isAr ? 'كلمة المرور مطلوبة' : 'Password is required')
          .test(
            'no-leading-trailing-whitespace',
            isAr ? 'لا يمكن أن تبدأ أو تنتهي كلمة المرور بمسافات' : 'Password cannot start or end with spaces',
            (value) => value === value.trim()
          )
          .max(255, isAr ? 'كلمة المرور يجب ألا تتجاوز 255 حرفًا' : 'Password must be at most 255 characters')
      })}
      onSubmit={async (values, { setErrors, setStatus, setSubmitting }) => {
        try {
          const trimmedEmail = values.email.trim();
          await login?.(trimmedEmail, values.password, checked);

          if (scriptedRef.current) {
            setStatus({ success: true });
            setSubmitting(false);
          }
        } catch (err) {
          if (scriptedRef.current) {
            setStatus({ success: false });
            setErrors({ submit: getLoginErrorMessage(err) });
            setSubmitting(false);
          }
        }
      }}
    >
      {({ errors, handleBlur, handleChange, handleSubmit, isSubmitting, setFieldError, touched, values }) => (
        <form noValidate onSubmit={handleSubmit} {...others}>
          <CustomFormControl fullWidth error={Boolean(touched.email && errors.email)}>
            <InputLabel htmlFor="outlined-adornment-email-login">
              {isAr ? 'البريد الإلكتروني أو اسم المستخدم' : 'Email or Username'}
            </InputLabel>
            <OutlinedInput
              id="outlined-adornment-email-login"
              type="text"
              autoComplete="username"
              value={values.email}
              name="email"
              onBlur={handleBlur}
              onChange={(event) => {
                handleChange(event);
                if (errors.submit) setFieldError('submit', undefined);
              }}
              label={isAr ? 'البريد الإلكتروني أو اسم المستخدم' : 'Email or Username'}
            />
            {touched.email && errors.email && (
              <FormHelperText error id="standard-weight-helper-text-email-login">
                {errors.email}
              </FormHelperText>
            )}
          </CustomFormControl>

          <CustomFormControl fullWidth error={Boolean(touched.password && errors.password)}>
            <InputLabel htmlFor="outlined-adornment-password-login">{isAr ? 'كلمة المرور' : 'Password'}</InputLabel>
            <OutlinedInput
              id="outlined-adornment-password-login"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              value={values.password}
              name="password"
              onBlur={handleBlur}
              onChange={(event) => {
                handleChange(event);
                if (errors.submit) setFieldError('submit', undefined);
              }}
              endAdornment={
                <InputAdornment position="end">
                  <IconButton
                    aria-label={isAr ? 'إظهار أو إخفاء كلمة المرور' : 'Toggle password visibility'}
                    onClick={handleClickShowPassword}
                    onMouseDown={handleMouseDownPassword}
                    edge="end"
                    size="large"
                  >
                    {showPassword ? <Visibility /> : <VisibilityOff />}
                  </IconButton>
                </InputAdornment>
              }
              label={isAr ? 'كلمة المرور' : 'Password'}
            />
            {touched.password && errors.password && (
              <FormHelperText error id="standard-weight-helper-text-password-login">
                {errors.password}
              </FormHelperText>
            )}
          </CustomFormControl>

          <Grid container sx={{ alignItems: 'center', justifyContent: 'space-between', mt: 0.5 }}>
            <Grid>
              <FormControlLabel
                control={
                  <Checkbox checked={checked} onChange={(event) => setChecked(event.target.checked)} name="checked" color="primary" />
                }
                label={isAr ? 'تذكر تسجيل الدخول' : 'Keep me signed in'}
              />
            </Grid>
            <Grid>
              <Typography
                variant="subtitle1"
                component={Link}
                to={
                  isLoggedIn
                    ? '/forgot-password'
                    : authParam
                      ? `/forgot-password?auth=${authParam}`
                      : '/forgot-password'
                }
                sx={{ textDecoration: 'none', color: 'primary.main', fontWeight: 600, '&:hover': { textDecoration: 'underline' } }}
              >
                {isAr ? 'نسيت كلمة المرور؟' : 'Forgot password?'}
              </Typography>
            </Grid>
          </Grid>

          {errors.submit && (
            <Box sx={{ mt: 2 }}>
              <Alert severity="error" role="alert" aria-live="assertive">
                {errors.submit}
              </Alert>
            </Box>
          )}
          <Box sx={{ mt: 2.5 }}>
            <AnimateButton>
              <Button
                color="primary"
                disabled={isSubmitting}
                fullWidth
                size="large"
                type="submit"
                variant="contained"
                aria-busy={isSubmitting}
                sx={{ minHeight: 46, fontWeight: 700, borderRadius: 2 }}
              >
                {isSubmitting ? (
                  <CircularProgress color="inherit" size={22} aria-label={isAr ? 'جاري تسجيل الدخول' : 'Signing in'} />
                ) : isAr ? (
                  'تسجيل الدخول'
                ) : (
                  'Sign In'
                )}
              </Button>
            </AnimateButton>
          </Box>
        </form>
      )}
    </Formik>
  );
}
