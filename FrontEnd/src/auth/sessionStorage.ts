export const SERVICE_TOKEN_KEY = 'serviceToken';

type AuthStorage = 'local' | 'session';

function getStorage(kind: AuthStorage): Storage | null {
  if (typeof window !== 'undefined') return kind === 'local' ? window.localStorage : window.sessionStorage;
  if (kind === 'local' && typeof localStorage !== 'undefined') return localStorage;
  if (kind === 'session' && typeof sessionStorage !== 'undefined') return sessionStorage;
  return null;
}

export function readStoredSession(): { token: string; storage: AuthStorage } | null {
  const localToken = getStorage('local')?.getItem(SERVICE_TOKEN_KEY);
  if (localToken) return { token: localToken, storage: 'local' };

  const sessionToken = getStorage('session')?.getItem(SERVICE_TOKEN_KEY);
  return sessionToken ? { token: sessionToken, storage: 'session' } : null;
}

export function writeSessionToken(token: string, remember: boolean): void {
  clearStoredSession();
  getStorage(remember ? 'local' : 'session')?.setItem(SERVICE_TOKEN_KEY, token);
}

export function clearStoredSession(): void {
  getStorage('local')?.removeItem(SERVICE_TOKEN_KEY);
  getStorage('session')?.removeItem(SERVICE_TOKEN_KEY);
}
