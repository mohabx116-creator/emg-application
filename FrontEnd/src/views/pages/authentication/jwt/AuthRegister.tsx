import { SyntheticEvent, useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useDispatch } from 'store';

// material-ui
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import FormControl from '@mui/material/FormControl';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormHelperText from '@mui/material/FormHelperText';
import Grid from '@mui/material/Grid';
import IconButton from '@mui/material/IconButton';
import InputAdornment from '@mui/material/InputAdornment';
import InputLabel from '@mui/material/InputLabel';
import OutlinedInput from '@mui/material/OutlinedInput';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';

// third party
import * as Yup from 'yup';
import { Formik } from 'formik';

// project imports
import AnimateButton from 'ui-component/extended/AnimateButton';
import CustomFormControl from 'ui-component/extended/Form/CustomFormControl';
import useAuth from 'hooks/useAuth';
import useScriptRef from 'hooks/useScriptRef';
import { strengthColor, strengthIndicator } from 'utils/password-strength';
import { openSnackbar } from 'store/slices/snackbar';

// assets
import Visibility from '@mui/icons-material/Visibility';
import VisibilityOff from '@mui/icons-material/VisibilityOff';

// types
import { StringColorProps } from 'types';

// ===========================|| JWT - REGISTER ||=========================== //

export default function JWTRegister({ ...others }) {
  const navigate = useNavigate();
  const scriptedRef = useScriptRef();
  const dispatch = useDispatch();

  const [showPassword, setShowPassword] = useState(false);
  const [checked, setChecked] = useState(true);

  const [strength, setStrength] = useState(0);
  const [level, setLevel] = useState<StringColorProps>();
  const { register } = useAuth();

  const handleClickShowPassword = () => {
    setShowPassword(!showPassword);
  };

  const handleMouseDownPassword = (event: SyntheticEvent) => {
    event.preventDefault();
  };

  const [searchParams] = useSearchParams();
  const authParam = searchParams.get('auth');

  const changePassword = (value: string) => {
    const temp = strengthIndicator(value);
    setStrength(temp);
    setLevel(strengthColor(temp));
  };

  useEffect(() => {
    changePassword('123456');
  }, []);

  return (
    <>
      <Stack sx={{ mb: 2, alignItems: 'center' }}>
        <Typography variant="subtitle1">إنشاء الحساب باستخدام البريد الإلكتروني</Typography>
      </Stack>

      <Formik
        initialValues={{
          email: '',
          password: '',
          firstName: '',
          lastName: '',
          submit: null
        }}
        validationSchema={Yup.object().shape({
          firstName: Yup.string()
            .trim()
            .required('الاسم الأول مطلوب')
            .min(2, 'الاسم الأول يجب أن يكون حرفين على الأقل')
            .max(50, 'الاسم الأول يجب ألا يتجاوز 50 حرفًا')
            .matches(/^[\p{L}\s]+$/u, 'اكتب الاسم الأول بحروف ومسافات فقط'),
          lastName: Yup.string()
            .trim()
            .required('اسم العائلة مطلوب')
            .min(2, 'اسم العائلة يجب أن يكون حرفين على الأقل')
            .max(50, 'اسم العائلة يجب ألا يتجاوز 50 حرفًا')
            .matches(/^[\p{L}\s]+$/u, 'اكتب اسم العائلة بحروف ومسافات فقط'),
          email: Yup.string().email('أدخل بريدًا إلكترونيًا صحيحًا').max(255).required('البريد الإلكتروني مطلوب'),
          password: Yup.string()
            .required('كلمة المرور مطلوبة')
            .test('no-leading-trailing-whitespace', 'لا يمكن أن تبدأ أو تنتهي كلمة المرور بمسافات', (value) => value === value.trim())
            .max(255, 'كلمة المرور يجب ألا تتجاوز 255 حرفًا')
        })}
        onSubmit={async (values, { setErrors, setStatus, setSubmitting }) => {
          try {
            const trimmedFirstName = values.firstName.trim();
            const trimmedLastName = values.lastName.trim();
            const trimmedEmail = values.email.trim();
            await register?.(trimmedEmail, values.password, trimmedFirstName, trimmedLastName);
            if (scriptedRef.current) {
              setStatus({ success: true });
              setSubmitting(false);
              dispatch(
                openSnackbar({
                  open: true,
                  message: 'تم إنشاء الحساب بنجاح.',
                  variant: 'alert',
                  alert: {
                    color: 'success'
                  },
                  close: false
                })
              );

              setTimeout(() => {
                navigate(authParam ? `/login?auth=${authParam}` : '/login', {
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
            <Grid container spacing={{ xs: 0, sm: 2 }}>
              <Grid size={{ xs: 12, sm: 6 }}>
                <CustomFormControl fullWidth error={Boolean(touched.firstName && errors.firstName)}>
                  <InputLabel htmlFor="outlined-adornment-first-register">الاسم الأول</InputLabel>
                  <OutlinedInput
                    id="outlined-adornment-first-register"
                    type="text"
                    name="firstName"
                    value={values.firstName}
                    onBlur={handleBlur}
                    onChange={handleChange}
                  />
                  {touched.firstName && errors.firstName && (
                    <FormHelperText error id="standard-weight-helper-text--register">
                      {errors.firstName}
                    </FormHelperText>
                  )}
                </CustomFormControl>
              </Grid>
              <Grid size={{ xs: 12, sm: 6 }}>
                <CustomFormControl fullWidth error={Boolean(touched.lastName && errors.lastName)}>
                  <InputLabel htmlFor="outlined-adornment-last-register">اسم العائلة</InputLabel>
                  <OutlinedInput
                    id="outlined-adornment-last-register"
                    type="text"
                    name="lastName"
                    value={values.lastName}
                    onBlur={handleBlur}
                    onChange={handleChange}
                  />
                  {touched.lastName && errors.lastName && (
                    <FormHelperText error id="standard-weight-helper-text--register">
                      {errors.lastName}
                    </FormHelperText>
                  )}
                </CustomFormControl>
              </Grid>
            </Grid>
            <CustomFormControl fullWidth error={Boolean(touched.email && errors.email)}>
              <InputLabel htmlFor="outlined-adornment-email-register">البريد الإلكتروني أو اسم المستخدم</InputLabel>
              <OutlinedInput
                id="outlined-adornment-email-register"
                type="email"
                value={values.email}
                name="email"
                onBlur={handleBlur}
                onChange={handleChange}
              />
              {touched.email && errors.email && (
                <FormHelperText error id="standard-weight-helper-text--register">
                  {errors.email}
                </FormHelperText>
              )}
            </CustomFormControl>

            <CustomFormControl fullWidth error={Boolean(touched.password && errors.password)}>
              <InputLabel htmlFor="outlined-adornment-password-register">كلمة المرور</InputLabel>
              <OutlinedInput
                id="outlined-adornment-password-register"
                type={showPassword ? 'text' : 'password'}
                value={values.password}
                name="password"
                label="كلمة المرور"
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
              {touched.password && errors.password && (
                <FormHelperText error id="standard-weight-helper-text-password-register">
                  {errors.password}
                </FormHelperText>
              )}
            </CustomFormControl>

            {strength !== 0 && (
              <FormControl fullWidth>
                <Box sx={{ mb: 2 }}>
                  <Stack direction="row" sx={{ gap: 2, alignItems: 'center' }}>
                    <Box sx={{ width: 85, height: 8, borderRadius: '7px', bgcolor: level?.color }} />
                    <Typography variant="subtitle1" sx={{ fontSize: '0.75rem' }}>
                      {level?.label}
                    </Typography>
                  </Stack>
                </Box>
              </FormControl>
            )}

            <FormControlLabel
              control={<Checkbox checked={checked} onChange={(event) => setChecked(event.target.checked)} name="checked" color="primary" />}
              label={
                <Typography variant="subtitle1">
                  أوافق على&nbsp;
                  <Typography variant="subtitle1" component={Link} to="#">
                    الشروط والأحكام
                  </Typography>
                </Typography>
              }
            />
            {errors.submit && (
              <Box sx={{ mt: 3 }}>
                <FormHelperText error>{errors.submit}</FormHelperText>
              </Box>
            )}

            <Box sx={{ mt: 2 }}>
              <AnimateButton>
                <Button disableElevation disabled={isSubmitting} fullWidth size="large" type="submit" variant="contained" color="secondary">
                  إنشاء الحساب
                </Button>
              </AnimateButton>
            </Box>
          </form>
        )}
      </Formik>
    </>
  );
}
