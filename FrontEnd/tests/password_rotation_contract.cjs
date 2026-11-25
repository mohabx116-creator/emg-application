const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const axios = require('axios').default;

const storage = new Map();
const sessionStorageMap = new Map();
global.localStorage = {
  getItem: (key) => storage.get(key) ?? null,
  setItem: (key, value) => storage.set(key, value),
  removeItem: (key) => storage.delete(key)
};
global.sessionStorage = {
  getItem: (key) => sessionStorageMap.get(key) ?? null,
  setItem: (key, value) => sessionStorageMap.set(key, value),
  removeItem: (key) => sessionStorageMap.delete(key)
};

let assertions = 0;
function check(value, message) {
  assertions += 1;
  assert.ok(value, message);
}

async function run() {
  const contract = await import('../src/auth/passwordRotation.ts');
  const interceptors = await import('../src/auth/axiosAuthInterceptors.ts');
  const loginResponse = await import('../src/auth/loginResponse.ts');
  contract.clearPasswordRotationRequired();
  const jwtContextSource = fs.readFileSync(path.resolve('src/contexts/JWTContext.tsx'), 'utf8');
  check(!jwtContextSource.includes('serviceToken'), 'active cookie-auth context does not persist bearer tokens');
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'normal users are not forced to rotate');
  contract.markPasswordRotationRequired();
  check(contract.isPasswordRotationRequired({ mustChangePassword: false }), 'refresh marker survives in storage');
  storage.set('serviceToken', 'expired-user-token');
  contract.synchronizePasswordRotationState(false, false, false);
  storage.delete('serviceToken');
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'expired session cleanup removes the previous user marker');
  contract.synchronizePasswordRotationState(false, false);
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'normal login does not inherit forced rotation');
  contract.synchronizePasswordRotationState(true, false);
  check(contract.isPasswordRotationRequired({ mustChangePassword: true }), 'valid forced session is restricted');
  contract.synchronizePasswordRotationState(true, false);
  check(contract.isPasswordRotationRequired({ mustChangePassword: true }), 'valid forced session remains forced after refresh');
  contract.synchronizePasswordRotationState(false, false);
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'current backend state clears a stale rotation marker');
  contract.clearPasswordRotationRequired();
  check(contract.isPasswordRotationRequired({ mustChangePassword: true }), 'current server state forces rotation');
  check(contract.isAnonymousAuthRequest('/api/account/login'), 'login is anonymous');
  check(contract.isAnonymousAuthRequest('/api/account/password-reset/request'), 'reset request is anonymous');
  check(contract.isAnonymousAuthRequest('/api/account/password-reset/confirm'), 'reset confirmation is anonymous');
  check(!contract.isAnonymousAuthRequest('/api/projects'), 'business APIs are not anonymous');
  check('/emg/account/password-change' === interceptors.buildPasswordRotationUrl('/emg'), 'basename is preserved');

  const observed = [];
  const client = axios.create({
    adapter: async (config) => {
      observed.push(config);
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    }
  });
  client.defaults.headers.common.Authorization = 'Bearer stale-token';
  storage.set('serviceToken', 'stale-token');
  interceptors.installAuthInterceptors(client);
  await client.post('/api/account/login', {});
  await client.post('/api/account/password-reset/request', {});
  await client.post('/api/account/password-reset/confirm', {});
  check(observed.slice(0, 3).every((config) => !String(config.headers?.Authorization || config.headers?.authorization).includes('Bearer')), 'anonymous requests remove inherited bearer headers');
  await client.get('/api/projects');
  check(!observed[3].headers.Authorization && !observed[3].headers.authorization, 'cookie-auth requests do not add bearer headers');

  const redirects = [];
  global.window = {
    location: {
      pathname: '/emg/projects',
      href: 'http://localhost/emg/projects',
      replace: (url) => redirects.push(url)
    }
  };
  const guardedClient = axios.create({
    adapter: async (config) => {
      throw new axios.AxiosError('rotation required', 'ERR_BAD_REQUEST', config, null, {
        status: 403,
        statusText: 'Forbidden',
        headers: {},
        config,
        data: { code: 'PASSWORD_CHANGE_REQUIRED', message: 'Password rotation required' }
      });
    }
  });
  interceptors.installAuthInterceptors(guardedClient);
  contract.clearPasswordRotationRequired();
  await guardedClient.get('/api/projects').catch(() => undefined);
  check(contract.isPasswordRotationRequired({ mustChangePassword: false }), '403 rotation responses set the forced state');
  check(redirects.includes('/account/password-change'), '403 rotation responses redirect to the forced screen');

  window.location.href = 'http://127.0.0.1:3100/login';
  window.location.pathname = '/login';
  const unauthorizedClient = axios.create({
    adapter: async (config) => {
      throw new axios.AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401, statusText: 'Unauthorized', headers: {}, config, data: ''
      });
    }
  });
  interceptors.installAuthInterceptors(unauthorizedClient);
  const loginDenied = await unauthorizedClient.post(loginResponse.LOGIN_URL, {}).catch(error => error);
  const sessionDenied = await unauthorizedClient.get(loginResponse.SESSION_PROFILE_URL).catch(error => error);
  check(loginDenied.response?.status === 401, 'empty 401 login responses retain HTTP status');
  check(loginResponse.getLoginErrorMessage(loginDenied) === 'بيانات الدخول غير صحيحة.', 'credential failure is shown on login 401');
  check(loginResponse.getLoginErrorMessage(sessionDenied).includes('تعذر تأكيد جلسة'), 'profile 401 is a session error');
  const offline = new axios.AxiosError('Network Error', 'ERR_NETWORK');
  check(loginResponse.getLoginErrorMessage(offline) === 'تعذر الاتصال بالخدمة.', 'transport errors display the connectivity message');
  assert.throws(() => loginResponse.readAccountUser(1), loginResponse.UnexpectedAuthResponseError);
  assertions += 1;
  assert.throws(() => loginResponse.readSessionProfile({ user: { id: '1' } }), loginResponse.UnexpectedAuthResponseError);
  assertions += 1;
  check(loginResponse.readSessionProfile({ user: { id: '1', mustChangePassword: true } }).mustChangePassword, 'a valid session profile preserves the rotation flag');

  contract.synchronizePasswordRotationState(true, false);
  check(contract.isPasswordRotationRequired({ mustChangePassword: false }), 'failed password change keeps forced state');
  contract.clearPasswordRotationRequired();
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'successful change clears forced state');
  contract.markPasswordRotationRequired();
  contract.clearPasswordRotationRequired();
  check(!contract.isPasswordRotationRequired({ mustChangePassword: false }), 'logout clears forced state');

  const guardSource = fs.readFileSync(path.resolve('src/utils/route-guard/AuthGuard.tsx'), 'utf8');
  const rotationSource = fs.readFileSync(path.resolve('src/views/pages/authentication/jwt/PasswordRotation.tsx'), 'utf8');
  const passwordSource = fs.readFileSync(path.resolve('src/views/application/users/account-profile/Profile1/ChangePassword.tsx'), 'utf8');
  check(guardSource.includes('PASSWORD_ROTATION_PATH'), 'operational routes use the rotation guard');
  check(rotationSource.includes('logout()') && rotationSource.includes("navigate('/login?reason=password-changed"), 'successful rotation requires fresh authentication');
  check(passwordSource.includes('onCompleted?.()'), 'successful password change notifies the forced-flow owner');

  console.log(`password rotation integration contract: ${assertions} assertions passed`);
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
