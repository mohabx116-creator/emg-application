import { UserProfile } from 'types/user-profile';

export type EmgUser = UserProfile | null | undefined;

export type PrimaryWorkspaceRole = 'admin' | 'director' | 'accountant' | 'project_manager' | 'engineer' | 'user';

export const ROLE_PRIORITY: readonly PrimaryWorkspaceRole[] = [
  'admin',
  'director',
  'accountant',
  'project_manager',
  'engineer',
  'user'
];

export function getUserRoles(user: EmgUser): string[] {
  return Array.from(
    new Set(
      [...(user?.roles || []), ...(user?.role ? [user.role] : [])]
        .map((role) => role?.trim().toLowerCase())
        .filter(Boolean)
    )
  );
}

export function isSystemAdmin(user: EmgUser): boolean {
  return getUserRoles(user).includes('admin');
}

export function hasPermission(user: EmgUser, permission: string): boolean {
  return isSystemAdmin(user) || Boolean(user?.permissions?.includes(permission));
}

export function hasAnyPermission(user: EmgUser, permissions: string[]): boolean {
  return isSystemAdmin(user) || permissions.some((permission) => Boolean(user?.permissions?.includes(permission)));
}

export function hasRole(user: EmgUser, role: string): boolean {
  return getUserRoles(user).includes(role.trim().toLowerCase());
}

/**
 * Resolves the deterministic primary workspace role using the canonical priority:
 * Admin -> Director -> Accountant -> Project Manager -> Engineer -> General User.
 * Note: This affects default landing presentation only and never expands or restricts permissions.
 */
export function resolvePrimaryRole(user: EmgUser): PrimaryWorkspaceRole {
  const roles = getUserRoles(user);
  for (const candidate of ROLE_PRIORITY) {
    if (roles.includes(candidate)) {
      return candidate;
    }
  }
  return 'user';
}

export function isSiteEngineer(user: EmgUser): boolean {
  return resolvePrimaryRole(user) === 'engineer';
}

export function roleTitle(user: EmgUser): string {
  const primary = resolvePrimaryRole(user);
  switch (primary) {
    case 'admin':
      return 'مسؤول المنظومة';
    case 'director':
      return 'المدير العام';
    case 'accountant':
      return 'المحاسب';
    case 'project_manager':
      return 'مدير المشروعات';
    case 'engineer':
      return 'مهندس الموقع';
    default:
      return 'مستخدم EMG';
  }
}

export function roleSubtitle(user: EmgUser): string {
  const primary = resolvePrimaryRole(user);
  switch (primary) {
    case 'admin':
      return 'إدارة المنظومة والمستخدمين والصلاحيات ومركز المراجعة والرقابة الشاملة';
    case 'director':
      return 'نظرة عامة على محفظة المشاريع والاعتمادات المعلقة والمؤشرات المالية والتقارير';
    case 'accountant':
      return 'الرقابة المالية وحسابات الموردين والدفعات واستيراد الأرشيف المعتمد والتقارير المالية';
    case 'project_manager':
      return 'متابعة المشاريع المسندة والتقدم التنفيذي والاعتمادات الفنية للموردين والتقارير';
    case 'engineer':
      return 'متابعة مشاريعك وتسجيل أعمال الموقع اليومية (الحفر، الخرسانة، الحديد، التشطيبات)';
    default:
      return 'مساحة العمل المخصصة حسب الصلاحيات المسندة لحسابك';
  }
}

export function resolveDefaultLandingPath(_user?: EmgUser): string {
  return '/emg/dashboard';
}
