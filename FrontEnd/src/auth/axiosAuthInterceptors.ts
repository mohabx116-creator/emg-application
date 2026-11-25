import { AxiosError, AxiosHeaders } from 'axios';
import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { CSRF_URL, LOGIN_URL, SESSION_PROFILE_URL } from './loginResponse.ts';

import {
  isAnonymousAuthRequest,
  isPasswordRotationError,
  markPasswordRotationRequired,
  PASSWORD_ROTATION_PATH
} from './passwordRotation.ts';
function readCookie(name: string): string | null {
  const match = document.cookie.split('; ').find((item) => item.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : null;
}

export function removeAuthorizationHeader(config: InternalAxiosRequestConfig): void {
  const headers = config.headers;
  if (!headers) return;

  if (headers instanceof AxiosHeaders) {
    headers.delete('Authorization');
    headers.delete('authorization');
    return;
  }

  const plainHeaders = headers as Record<string, unknown>;
  delete plainHeaders.Authorization;
  delete plainHeaders.authorization;
}

export function buildPasswordRotationUrl(baseName = ''): string {
  const normalizedBaseName = baseName && baseName !== '/' ? `/${baseName.replace(/^\/+|\/+$/g, '')}` : '';
  return `${normalizedBaseName}${PASSWORD_ROTATION_PATH}`;
}

export function installAuthInterceptors(axiosServices: AxiosInstance): void {
  axiosServices.interceptors.request.use(
    async (config) => {
      // Auth V2 uses the HttpOnly session cookie. Remove inherited bearer
      // defaults for every request, especially anonymous endpoints.
      removeAuthorizationHeader(config);
      if (isAnonymousAuthRequest(config.url)) return config;

      const method = (config.method || 'get').toLowerCase();
      if (method !== 'get' && method !== 'head' && method !== 'options') {
        const csrf = readCookie('emg_csrf');
        if (csrf) config.headers['X-CSRF-TOKEN'] = csrf;
      }
      return config;
    },
    (error) => Promise.reject(error)
  );

  axiosServices.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => {
      if (isPasswordRotationError(error)) {
        markPasswordRotationRequired();
        if (!window.location.pathname.includes(PASSWORD_ROTATION_PATH)) {
          window.location.replace(buildPasswordRotationUrl(import.meta.env?.VITE_APP_BASE_NAME));
        }
      }
      const currentPath = window.location.pathname;
      const isPublicLandingRoute = currentPath === '/' || currentPath === '/pages/landing';
      if (error.response?.status === 401 && !window.location.href.includes('/login') && !isPublicLandingRoute) {
        window.location.pathname = '/login';
      }
      const path = error.config?.url?.split('?')[0];
      if (path === LOGIN_URL || path === SESSION_PROFILE_URL || path === CSRF_URL) {
        return Promise.reject(error);
      }
      const payload = error.response?.data as
        | { message?: string; checks?: unknown[]; matches?: unknown[]; [key: string]: unknown }
        | undefined;
      if (payload && typeof payload === 'object') {
        const status = error.response?.status;
        const details = Array.isArray(payload.checks) ? payload.checks : Array.isArray(payload.matches) ? payload.matches : [];
        if (payload.message && details.length > 0) {
          return Promise.reject({ ...payload, status, message: `${payload.message} ${details.join(' · ')}` });
        }
        if (payload.message) return Promise.reject({ ...payload, status });
      }
      return Promise.reject(payload || 'تعذر الاتصال بالخدمة.');
    }
  );
}
