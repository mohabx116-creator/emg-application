import { useNavigate } from 'react-router-dom';

// project imports
import useAuth from 'hooks/useAuth';
import { DASHBOARD_PATH } from 'config';
import { GuardProps } from 'types';
import { useEffect } from 'react';
import { resolveDefaultLandingPath } from 'Modules/EMG/roleAccess';

// ==============================|| GUEST GUARD ||============================== //

/**
 * Guest guard for routes having no auth required
 * @param {PropTypes.node} children children element/node
 */

export default function GuestGuard({ children }: GuardProps) {
  const { isLoggedIn, user } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isLoggedIn) {
      const target = resolveDefaultLandingPath(user) || DASHBOARD_PATH;
      navigate(target, { replace: true });
    }
  }, [isLoggedIn, user, navigate]);

  return children;
}
