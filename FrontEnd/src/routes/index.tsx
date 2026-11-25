import { Navigate } from 'react-router-dom';
import { createBrowserRouter } from 'react-router-dom';

// routes
import LoginRoutes from './LoginRoutes';
import ProductionMainRoutes from './ProductionMainRoutes';
import ProductionSimpleRoutes from './ProductionSimpleRoutes';

// ==============================|| ROUTING RENDER ||============================== //

const router = createBrowserRouter(
  [{ path: '/', element: <Navigate to="/login" replace /> }, LoginRoutes, ProductionSimpleRoutes, ProductionMainRoutes],
  {
    basename: import.meta.env.VITE_APP_BASE_NAME
  }
);

export default router;
