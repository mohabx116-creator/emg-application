import { useLocation, useNavigate } from 'react-router-dom';

// project imports
import useAuth from 'hooks/useAuth';
import { GuardProps } from 'types';
import { useEffect } from 'react';
import { isPasswordRotationRequired, PASSWORD_ROTATION_PATH } from 'auth/passwordRotation';

// ==============================|| AUTH GUARD ||============================== //

/**
 * Authentication guard for routes
 * @param {PropTypes.node} children children element/node
 */
export default function AuthGuard({ children }: GuardProps) {
  const { isLoggedIn, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (!isLoggedIn) {
      navigate('/login', { replace: true });
    }
  }, [isLoggedIn, navigate]);

  useEffect(() => {
    if (isLoggedIn && isPasswordRotationRequired(user) && location.pathname !== PASSWORD_ROTATION_PATH) {
      navigate(PASSWORD_ROTATION_PATH, { replace: true });
    }
  }, [isLoggedIn, location.pathname, navigate, user]);

  if (!isLoggedIn || (isPasswordRotationRequired(user) && location.pathname !== PASSWORD_ROTATION_PATH)) return null;
  return children;
}
