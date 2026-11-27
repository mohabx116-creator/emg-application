import { lazy, ReactNode } from 'react';

import MainLayout from 'layout/MainLayout';
import Loadable from 'ui-component/Loadable';
import AuthGuard from 'utils/route-guard/AuthGuard';
import EmgPermissionGuard from 'utils/route-guard/EmgPermissionGuard';
import NotFound from './NotFound';

const Dashboard = Loadable(lazy(() => import('views/dashboard/EMG')));
const Projects = Loadable(lazy(() => import('Modules/EMG/Projects')));
const ProjectDetails = Loadable(lazy(() => import('Modules/EMG/ProjectDetails')));
const ActiveProjectRedirect = Loadable(lazy(() => import('Modules/EMG/ActiveProjectRedirect')));
const SiteMovements = Loadable(lazy(() => import('Modules/EMG/SiteMovements')));
const SiteMovementDetails = Loadable(lazy(() => import('Modules/EMG/SiteMovementDetails')));
const Excavation = Loadable(lazy(() => import('Modules/EMG/Excavation')));
const Concrete = Loadable(lazy(() => import('Modules/EMG/Concrete')));
const Steel = Loadable(lazy(() => import('Modules/EMG/Steel')));
const Finishing = Loadable(lazy(() => import('Modules/EMG/Finishing')));
const Suppliers = Loadable(lazy(() => import('Modules/EMG/Suppliers')));
const SupplierDetails = Loadable(lazy(() => import('Modules/EMG/SupplierDetails')));
const ContractorStatements = Loadable(lazy(() => import('Modules/EMG/contractorstatements')));
const Advances = Loadable(lazy(() => import('Modules/EMG/Advances')));
const AdvanceDetails = Loadable(lazy(() => import('Modules/EMG/AdvanceDetails')));
const Attendance = Loadable(lazy(() => import('Modules/EMG/Attendance')));
const Notifications = Loadable(lazy(() => import('Modules/EMG/Notifications')));
const Profile = Loadable(lazy(() => import('Modules/EMG/Profile')));
const Users = Loadable(lazy(() => import('Modules/EMG/Users')));
const Roles = Loadable(lazy(() => import('Modules/EMG/Roles')));
const Reports = Loadable(lazy(() => import('Modules/EMG/Reports')));
const ReportBuilder = Loadable(lazy(() => import('Modules/EMG/ReportBuilder')));
const ConsolidatedStatement = Loadable(lazy(() => import('Modules/EMG/ConsolidatedStatement')));
const ReportsDetails = Loadable(lazy(() => import('Modules/EMG/ReportsDetails')));
const Imports = Loadable(lazy(() => import('Modules/EMG/Imports')));
const ImportsLanding = Loadable(lazy(() => import('Modules/EMG/Imports/Landing')));
const ApprovedHistory = Loadable(lazy(() => import('Modules/EMG/Imports/ApprovedHistory')));

const guard = (permissions: string[], element: ReactNode) => (
  <EmgPermissionGuard permissions={permissions}>{element}</EmgPermissionGuard>
);

const ProductionMainRoutes = {
  path: '/',
  element: (
    <AuthGuard>
      <MainLayout />
    </AuthGuard>
  ),
  children: [
    { path: '/emg', element: guard(['projects.view', 'attendance.view', 'notifications.view', 'reports.view', 'suppliers.view', 'advances.view'], <Dashboard />) },
    { path: '/emg/dashboard', element: guard(['projects.view', 'attendance.view', 'notifications.view', 'reports.view', 'suppliers.view', 'advances.view'], <Dashboard />) },
    { path: '/emg/projects', element: guard(['projects.view'], <Projects />) },
    { path: '/emg/site-movements', element: guard(['site-movements.view', 'site-movements.manage', 'projects.view'], <ActiveProjectRedirect />) },
    { path: '/emg/projects/:projectId', element: guard(['projects.view'], <ProjectDetails />) },
    { path: '/emg/projects/:projectId/movements', element: guard(['projects.view', 'site-movements.view'], <SiteMovements />) },
    { path: '/emg/projects/:projectId/advances', element: guard(['advances.manage', 'advances.view'], <Advances />) },
    { path: '/emg/site-movements/:movementId', element: guard(['projects.view', 'site-movements.view'], <SiteMovementDetails />) },
    { path: '/emg/projects/:projectId/excavation', element: guard(['projects.view'], <Excavation />) },
    { path: '/emg/projects/:projectId/concrete', element: guard(['projects.view'], <Concrete />) },
    { path: '/emg/projects/:projectId/steel', element: guard(['projects.view'], <Steel />) },
    { path: '/emg/projects/:projectId/finishing', element: guard(['projects.view'], <Finishing />) },
    { path: '/emg/suppliers', element: guard(['suppliers.manage', 'suppliers.view'], <Suppliers />) },
    { path: '/emg/suppliers/:supplierId', element: guard(['suppliers.manage', 'suppliers.view'], <SupplierDetails />) },
    { path: '/emg/contractor-statements', element: guard(['suppliers.view'], <ContractorStatements />) },
    { path: '/emg/advances', element: guard(['advances.manage', 'advances.view'], <Advances />) },
    { path: '/emg/advances/:advanceId', element: guard(['advances.manage', 'advances.view'], <AdvanceDetails />) },
    { path: '/emg/attendance', element: guard(['attendance.manage', 'attendance.view'], <Attendance />) },
    { path: '/emg/notifications', element: guard(['notifications.view'], <Notifications />) },
    { path: '/emg/profile', element: <Profile /> },
    { path: '/emg/users', element: guard(['users.view', 'users.manage'], <Users />) },
    { path: '/emg/roles', element: guard(['roles.view', 'roles.manage'], <Roles />) },
    { path: '/emg/reports', element: guard(['reports.view'], <Reports />) },
    { path: '/emg/reports/builder', element: guard(['reports.view'], <ReportBuilder />) },
    { path: '/emg/reports/consolidated', element: guard(['reports.view'], <ConsolidatedStatement />) },
    { path: '/emg/reports/:projectId', element: guard(['reports.view'], <ReportsDetails />) },
    { path: '/emg/imports', element: guard(['data.migrate'], <ImportsLanding />) },
    { path: '/emg/imports/new', element: guard(['data.migrate'], <Imports />) },
    { path: '/emg/imports/update', element: guard(['data.migrate'], <Imports />) },
    { path: '/emg/imports/approved-history', element: guard(['project-imports.import-approved-history'], <ApprovedHistory />) },
    { path: '*', element: <NotFound /> }
  ]
};

export default ProductionMainRoutes;
