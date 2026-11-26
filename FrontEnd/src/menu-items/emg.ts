import {
  IconBuilding,
  IconChartBar,
  IconClipboardCheck,
  IconClock,
  IconDashboard,
  IconFileAnalytics,
  IconFileDescription,
  IconFileUpload,
  IconSettings,
  IconTruckDelivery,
  IconUserShield,
  IconUsers
} from '@tabler/icons-react';
import { NavItemType } from 'types';

export const emgOverviewGroup: NavItemType = {
  id: 'emg-overview',
  title: 'نظرة عامة',
  type: 'group',
  children: [
    {
      id: 'emg-dashboard',
      title: 'لوحة التحكم',
      type: 'item',
      url: '/emg/dashboard',
      icon: IconDashboard,
      breadcrumbs: false,
      requiredPermissions: ['projects.view', 'attendance.view', 'notifications.view', 'reports.view', 'suppliers.view', 'advances.view']
    },
    {
      id: 'emg-projects',
      title: 'المشروعات',
      type: 'item',
      url: '/emg/projects',
      icon: IconBuilding,
      breadcrumbs: false,
      requiredPermission: 'projects.view'
    },
    {
      id: 'emg-site-movements',
      title: 'حركات الموقع اليومية',
      type: 'item',
      url: '/emg/site-movements',
      icon: IconClipboardCheck,
      breadcrumbs: false,
      requiredPermissions: ['site-movements.view', 'site-movements.manage', 'projects.view']
    }
  ]
};

export const emgAccountsGroup: NavItemType = {
  id: 'emg-accounts',
  title: 'الحسابات',
  type: 'group',
  children: [
    {
      id: 'emg-suppliers',
      title: 'الموردون',
      type: 'item',
      url: '/emg/suppliers',
      icon: IconTruckDelivery,
      breadcrumbs: false,
      requiredPermissions: ['suppliers.view', 'suppliers.manage']
    },
    {
      id: 'emg-contractor-statements',
      title: 'كشوف المقاولين',
      type: 'item',
      url: '/emg/contractor-statements',
      icon: IconFileDescription,
      breadcrumbs: false,
      requiredPermissions: ['suppliers.view', 'reports.view']
    },
    {
      id: 'emg-reports',
      title: 'التقارير المالية',
      type: 'item',
      url: '/emg/reports',
      icon: IconChartBar,
      breadcrumbs: false,
      requiredPermission: 'reports.view'
    },
    {
      id: 'emg-consolidated-statement',
      title: 'كشف الحساب المجمع',
      type: 'item',
      url: '/emg/reports/consolidated',
      icon: IconFileAnalytics,
      breadcrumbs: false,
      requiredPermission: 'reports.view'
    }
  ]
};

export const emgEmployeesGroup: NavItemType = {
  id: 'emg-employees',
  title: 'الموظفون',
  type: 'group',
  children: [
    {
      id: 'emg-attendance',
      title: 'الحضور والانصراف',
      type: 'item',
      url: '/emg/attendance',
      icon: IconClock,
      breadcrumbs: false,
      requiredPermissions: ['attendance.view', 'attendance.manage']
    }
  ]
};

export const emgDataGroup: NavItemType = {
  id: 'emg-data',
  title: 'البيانات',
  type: 'group',
  children: [
    {
      id: 'emg-imports',
      title: 'استيراد البيانات',
      type: 'item',
      url: '/emg/imports',
      icon: IconFileUpload,
      breadcrumbs: false,
      requiredPermissions: ['data.migrate', 'project-imports.import-approved-history']
    }
  ]
};

export const emgAdministrationGroup: NavItemType = {
  id: 'emg-administration',
  title: 'الإدارة',
  type: 'group',
  children: [
    {
      id: 'emg-users',
      title: 'المستخدمون',
      type: 'item',
      url: '/emg/users',
      icon: IconUsers,
      breadcrumbs: false,
      requiredPermissions: ['users.view', 'users.manage']
    },
    {
      id: 'emg-roles',
      title: 'الأدوار والصلاحيات',
      type: 'item',
      url: '/emg/roles',
      icon: IconUserShield,
      breadcrumbs: false,
      requiredPermissions: ['roles.view', 'roles.manage']
    },
    {
      id: 'emg-profile',
      title: 'الإعدادات',
      type: 'item',
      url: '/emg/profile',
      icon: IconSettings,
      breadcrumbs: false
    }
  ]
};

export const emgGroups: NavItemType[] = [
  emgOverviewGroup,
  emgAccountsGroup,
  emgEmployeesGroup,
  emgDataGroup,
  emgAdministrationGroup
];

export default emgGroups;
