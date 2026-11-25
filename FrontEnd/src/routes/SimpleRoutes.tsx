import { lazy } from 'react';

// project imports
import SimpleLayout from 'layout/SimpleLayout';
import Loadable from 'ui-component/Loadable';
import PasswordRotationGuard from 'utils/route-guard/PasswordRotationGuard';

// login routing
const ContactUs = Loadable(lazy(() => import('views/pages/contact-us')));
const SaasPageFaqs = Loadable(lazy(() => import('views/pages/saas-pages/Faqs')));
const SaasPagePrivacyPolicy = Loadable(lazy(() => import('views/pages/saas-pages/PrivacyPolicy')));
const PasswordRotation = Loadable(lazy(() => import('views/pages/authentication/jwt/PasswordRotation')));

// ==============================|| SIMPLE ROUTING ||============================== //

const SimpleRoutes = {
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
    },
    {
      path: '/pages/contact-us',
      element: <ContactUs />
    },
    {
      path: '/pages/faqs',
      element: <SaasPageFaqs />
    },
    {
      path: '/pages/privacy-policy',
      element: <SaasPagePrivacyPolicy />
    }
  ]
};

export default SimpleRoutes;
