import {
  IconBell,
  IconBuilding,
  IconCash,
  IconChartBar,
  IconChecklist,
  IconClock,
  IconDashboard,
  IconFileAnalytics,
  IconFileUpload,
  IconTable,
  IconTruckDelivery,
  IconUser,
  IconUserShield,
  IconUsers,
  IconClipboardCheck,
  IconFileDescription
} from '@tabler/icons-react';
import { NavItemType } from 'types';

export const emgOverviewGroup: NavItemType = {
  id: 'emg-overview',
  title: 'نظرة عامة',
  caption: 'لوحة التحكم والمشروعات',
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
      title: 'المشاريع',
      type: 'item',
      url: '/emg/projects',
      icon: IconBuilding,
      breadcrumbs: false,
      requiredPermission: 'projects.view'
    }
  ]
};

export const emgSiteOperationsGroup: NavItemType = {
  id: 'emg-site-operations',
  title: 'عمليات الموقع',
  caption: 'حركة الموقع اليومية والحضور',
  type: 'group',
  children: [
    {
      id: 'emg-site-movements',
      title: 'حركة الموقع اليومية',
      type: 'item',
      url: '/emg/projects',
      icon: IconClipboardCheck,
      breadcrumbs: false,
      requiredPermissions: ['site-movements.view', 'site-movements.manage']
    },
    {
      id: 'emg-attendance',
      title: 'الحضور والانصراف',
      type: 'item',
      url: '/emg/attendance',
      icon: IconClock,
      breadcrumbs: false,
      requiredPermissions: ['attendance.manage', 'attendance.view']
    }
  ]
};

export const emgProcurementGroup: NavItemType = {
  id: 'emg-procurement',
  title: 'المشتريات والموردين',
  caption: 'توريدات ودفعات الموردين',
  type: 'group',
  children: [
    {
      id: 'emg-suppliers',
      title: 'الموردين',
      type: 'item',
      url: '/emg/suppliers',
      icon: IconTruckDelivery,
      breadcrumbs: false,
      requiredPermissions: ['suppliers.manage', 'suppliers.view']
    },
    {
      id: 'emg-advances',
      title: 'دفعات تحت الحساب',
      type: 'item',
      url: '/emg/advances',
      icon: IconCash,
      breadcrumbs: false,
      requiredPermissions: ['advances.manage', 'advances.view']
    },
    {
      id: 'emg-contractor-statements',
      title: 'كشوف المقاولين',
      type: 'item',
      url: '/emg/contractor-statements',
      icon: IconFileDescription,
      breadcrumbs: false,
      requiredPermission: 'suppliers.view'
    }
  ]
};

export const emgApprovalsGroup: NavItemType = {
  id: 'emg-approvals',
  title: 'المراجعة والاعتمادات',
  caption: 'الاعتمادات الفنية والمالية',
  type: 'group',
  children: [
    {
      id: 'emg-review-center',
      title: 'مركز المراجعة',
      type: 'item',
      url: '/emg/review-center',
      icon: IconChecklist,
      breadcrumbs: false,
      requiredPermissions: [
        'project-imports.approve',
        'project-imports.reject',
        'workflow.execute',
        'suppliers.supplies-accept-technical',
        'suppliers.supplies-approve-financial'
      ]
    }
  ]
};

export const emgReportsGroup: NavItemType = {
  id: 'emg-reports-group',
  title: 'التقارير والأرشيف',
  caption: 'التقارير المالية والأرشيف التاريخي',
  type: 'group',
  children: [
    {
      id: 'emg-reports',
      title: 'التقارير',
      type: 'item',
      url: '/emg/reports',
      icon: IconChartBar,
      breadcrumbs: false,
      requiredPermission: 'reports.view'
    },
    {
      id: 'emg-report-builder',
      title: 'مركز التقارير المالي',
      type: 'item',
      url: '/emg/reports/builder',
      icon: IconTable,
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
    },
    {
      id: 'emg-approved-history',
      title: 'استيراد أرشيف معتمد',
      type: 'item',
      url: '/emg/imports/approved-history',
      icon: IconFileUpload,
      breadcrumbs: false,
      requiredPermission: 'project-imports.import-approved-history'
    },
    {
      id: 'emg-imports',
      title: 'ترحيل البيانات',
      type: 'item',
      url: '/emg/imports',
      icon: IconFileUpload,
      breadcrumbs: false,
      requiredPermission: 'data.migrate'
    }
  ]
};

export const emgAdministrationGroup: NavItemType = {
  id: 'emg-administration',
  title: 'الإدارة والأمان',
  caption: 'المستخدمون والأدوار والإعدادات',
  type: 'group',
  children: [
    {
      id: 'emg-users',
      title: 'إدارة المستخدمين',
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
      id: 'emg-notifications',
      title: 'الإشعارات',
      type: 'item',
      url: '/emg/notifications',
      icon: IconBell,
      breadcrumbs: false,
      requiredPermission: 'notifications.view'
    },
    {
      id: 'emg-profile',
      title: 'البروفايل',
      type: 'item',
      url: '/emg/profile',
      icon: IconUser,
      breadcrumbs: false
    }
  ]
};

export const emgGroups: NavItemType[] = [
  emgOverviewGroup,
  emgSiteOperationsGroup,
  emgProcurementGroup,
  emgApprovalsGroup,
  emgReportsGroup,
  emgAdministrationGroup
];

export default emgGroups;
