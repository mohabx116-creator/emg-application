import { AxiosError } from 'axios';

export const PASSWORD_ROTATION_PATH = '/account/password-change';

export type PasswordRotationUser = { mustChangePassword?: boolean };

export function isPasswordRotationRequired(user: PasswordRotationUser | null | undefined): boolean {
  return user?.mustChangePassword === true || sessionStorage.getItem('emg_password_rotation_required') === 'true';
}

export function markPasswordRotationRequired(): void {
  sessionStorage.setItem('emg_password_rotation_required', 'true');
}
export function clearPasswordRotationRequired(): void {
  sessionStorage.removeItem('emg_password_rotation_required');
}
export function synchronizePasswordRotationState(accountRequiresRotation: boolean, _unused = false): boolean {
  if (accountRequiresRotation) markPasswordRotationRequired();
  else clearPasswordRotationRequired();
  return accountRequiresRotation;
}
export function isPasswordRotationError(error: unknown): boolean {
  const data = (error instanceof AxiosError ? error.response?.data : (error as { response?: { data?: unknown } } | null)?.response?.data) as { code?: unknown } | undefined;
  return data?.code === 'PASSWORD_CHANGE_REQUIRED';
}
export function isAnonymousAuthRequest(url?: string): boolean {
  const path = url?.split('?')[0] || '';
  return ['/api/account/login', '/api/account/csrf', '/api/account/password-reset/request', '/api/account/password-reset/confirm'].includes(path);
}
