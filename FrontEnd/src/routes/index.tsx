import { lazy } from 'react';
import { createBrowserRouter } from 'react-router-dom';

// routes
import Loadable from 'ui-component/Loadable';
import LoginRoutes from './LoginRoutes';
import ProductionMainRoutes from './ProductionMainRoutes';
import ProductionSimpleRoutes from './ProductionSimpleRoutes';

const LandingPage = Loadable(lazy(() => import('views/pages/landing')));

// ==============================|| ROUTING RENDER ||============================== //

const router = createBrowserRouter(
  [{ path: '/', element: <LandingPage /> }, LoginRoutes, ProductionSimpleRoutes, ProductionMainRoutes],
  {
    basename: import.meta.env.VITE_APP_BASE_NAME
  }
);

export default router;
