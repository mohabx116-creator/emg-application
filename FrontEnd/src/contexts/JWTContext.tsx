import { createContext, ReactElement, useEffect, useReducer } from 'react';
import { Chance } from 'chance';
import { LOGIN, LOGOUT } from 'store/actions';
import accountReducer from 'store/accountReducer';
import Loader from 'ui-component/Loader';
import axios from 'utils/axios';
import { InitialLoginContextProps, JWTContextType } from 'types/auth';
import { clearPasswordRotationRequired, synchronizePasswordRotationState } from 'auth/passwordRotation';
import {
  CSRF_URL,
  LOGIN_URL,
  SESSION_PROFILE_URL,
  UnexpectedAuthResponseError,
  readAccountUser,
  readSessionProfile
} from 'auth/loginResponse';

const chance = new Chance();
const initialState: InitialLoginContextProps = { isLoggedIn: false, isInitialized: false, user: null };
const JWTContext = createContext<JWTContextType | null>(null);

async function ensureCsrf(): Promise<void> {
  const { data } = await axios.get<{ csrf?: boolean }>(CSRF_URL);
  if (data?.csrf !== true) throw new UnexpectedAuthResponseError();
}

async function loadSessionProfile() {
  const { data } = await axios.get<unknown>(SESSION_PROFILE_URL);
  return readSessionProfile(data);
}

export function JWTProvider({ children }: { children: ReactElement }) {
  const [state, dispatch] = useReducer(accountReducer, initialState);

  useEffect(() => {
    const init = async () => {
      try {
        await ensureCsrf();
        const user = await loadSessionProfile();
        synchronizePasswordRotationState(user.mustChangePassword === true, false);
        dispatch({ type: LOGIN, payload: { isLoggedIn: true, user } });
      } catch {
        clearPasswordRotationRequired();
        dispatch({ type: LOGOUT });
      }
    };
    void init();
  }, []);

  const login = async (email: string, password: string, remember = true) => {
    const { data } = await axios.post<unknown>(LOGIN_URL, { email, password, rememberMe: remember });
    readAccountUser(data);
    const user = await loadSessionProfile();
    await ensureCsrf();
    synchronizePasswordRotationState(user.mustChangePassword === true, false);
    dispatch({ type: LOGIN, payload: { isLoggedIn: true, user } });
  };

  const logout = async () => {
    try { await ensureCsrf(); await axios.post('/api/account/logout'); } catch { /* session may already be revoked after password change */ }
    clearPasswordRotationRequired();
    dispatch({ type: LOGOUT });
  };

  const register = async (email: string, password: string, firstName: string, lastName: string) => {
    const response = await axios.post('/api/account/register', { id: chance.bb_pin(), email, password, firstName, lastName });
    window.sessionStorage.setItem('users', JSON.stringify(response.data));
  };

  const refreshUser = async () => {
    const user = await loadSessionProfile();
    synchronizePasswordRotationState(user.mustChangePassword === true, false);
    dispatch({ type: LOGIN, payload: { isLoggedIn: true, user } });
  };

  const resetPassword = async (email: string) => { await axios.post('/api/account/password-reset/request', { email: email.trim() }); };

  if (!state.isInitialized) return <Loader />;
  return <JWTContext value={{ ...state, login, logout, register, resetPassword, refreshUser, updateProfile: () => undefined }}>{children}</JWTContext>;
}

export default JWTContext;
