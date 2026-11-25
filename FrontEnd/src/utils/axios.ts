/**
 * axios setup to use mock service
 */

import axios, { AxiosRequestConfig } from 'axios';
import { installAuthInterceptors } from 'auth/axiosAuthInterceptors';

// EMG is same-origin in production. In development Vite proxies /api, so the
// same relative base works in both environments and preserves auth cookies.
const axiosServices = axios.create({ baseURL: '/', withCredentials: true, timeout: 10000 });

// ==============================|| AXIOS - FOR MOCK SERVICES ||============================== //

installAuthInterceptors(axiosServices);

export default axiosServices;

export async function fetcher(args: string | [string, AxiosRequestConfig]) {
  const [url, config] = Array.isArray(args) ? args : [args];

  const res = await axiosServices.get(url, { ...config });

  return res.data;
}
