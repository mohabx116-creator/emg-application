import { lazy } from 'react';
import SimpleLayout from 'layout/SimpleLayout';
import Loadable from 'ui-component/Loadable';
import PasswordRotationGuard from 'utils/route-guard/PasswordRotationGuard';

const PasswordRotation = Loadable(lazy(() => import('views/pages/authentication/jwt/PasswordRotation')));

const ProductionSimpleRoutes = {
  path: '/',
  element: <SimpleLayout />,
  children: [
    {
      path: '/account/password-change',
      element: (
        <PasswordRotationGuard>
          <PasswordRotation />
        </PasswordRotationGuard>
      )
    }
  ]
};

export default ProductionSimpleRoutes;
