import { AxiosError, isAxiosError } from 'axios';
import type { UserProfile } from 'types/user-profile';

export const LOGIN_URL = '/api/account/login';
export const SESSION_PROFILE_URL = '/api/account/me';
export const CSRF_URL = '/api/account/csrf';

const unexpectedResponse = 'تعذر إكمال تسجيل الدخول بسبب استجابة غير متوقعة من الخادم.';

export class UnexpectedAuthResponseError extends Error {
  constructor() {
    super(unexpectedResponse);
  }
}

export function readAccountUser(data: unknown): UserProfile {
  const user = (data as { user?: UserProfile } | null)?.user;
  if (!user || typeof user.id !== 'string' || !user.id) throw new UnexpectedAuthResponseError();
  return user;
}

export function readSessionProfile(data: unknown): UserProfile {
  const user = readAccountUser(data);
  if (typeof user.mustChangePassword !== 'boolean') throw new UnexpectedAuthResponseError();
  return user;
}

export function getLoginErrorMessage(error: unknown): string {
  if (error instanceof UnexpectedAuthResponseError) return error.message;
  if (!isAxiosError(error)) return 'تعذر إكمال تسجيل الدخول. حاول مرة أخرى.';

  const status = error.response?.status;
  const loginRequest = error.config?.url?.split('?')[0] === LOGIN_URL;
  if (status === 429) return 'تم تجاوز عدد محاولات الدخول المسموح. حاول مرة أخرى بعد قليل.';
  if (status === 401 && loginRequest) return 'بيانات الدخول غير صحيحة.';
  if ((status === 401 || status === 403) && !loginRequest) return 'تعذر تأكيد جلسة تسجيل الدخول. حاول مرة أخرى.';
  if (status === 400 && loginRequest) return 'لم يقبل الخادم بيانات تسجيل الدخول. راجع الحقول وحاول مرة أخرى.';
  if (status !== undefined && status >= 500) return 'خدمة الدخول غير متاحة حاليًا. حاول مرة أخرى.';
  if (
    !error.response &&
    (error.code === AxiosError.ERR_NETWORK || error.code === AxiosError.ECONNABORTED || error.code === AxiosError.ETIMEDOUT)
  ) {
    return 'تعذر الاتصال بالخدمة.';
  }
  return unexpectedResponse;
}
