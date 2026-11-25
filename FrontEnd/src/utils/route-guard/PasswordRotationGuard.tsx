import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import useAuth from 'hooks/useAuth';
import { isPasswordRotationRequired, PASSWORD_ROTATION_PATH } from 'auth/passwordRotation';
import { GuardProps } from 'types';

export default function PasswordRotationGuard({ children }: GuardProps) {
  const { isLoggedIn, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const required = isPasswordRotationRequired(user);

  useEffect(() => {
    if (!isLoggedIn) navigate('/login', { replace: true });
    else if (!required && location.pathname === PASSWORD_ROTATION_PATH) navigate('/emg', { replace: true });
  }, [isLoggedIn, location.pathname, navigate, required]);

  if (!isLoggedIn || !required) return null;
  return children;
}
