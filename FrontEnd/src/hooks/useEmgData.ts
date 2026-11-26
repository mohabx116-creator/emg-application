import useSWR, { mutate as globalMutate } from 'swr';
import {
  EmgAdvance,
  EmgAttendance,
  EmgProject,
  EmgSupplier,
  getEmgAdvances,
  getEmgProjects,
  getEmgSuppliers,
  getTodayAttendance
} from 'api/emg';
import useAuth from 'hooks/useAuth';
import { hasPermission } from 'Modules/EMG/roleAccess';

const DEFAULT_SWR_OPTIONS = {
  revalidateOnFocus: false,
  dedupingInterval: 30_000,
  shouldRetryOnError: false
};

export function useEmgProjectsCache(enabled = true) {
  const { user, isLoggedIn } = useAuth();
  const allowed = enabled && isLoggedIn && hasPermission(user, 'projects.view');
  const key = allowed && user?.id ? ['emg/projects', user.id] : null;
  return useSWR<EmgProject[]>(key, () => getEmgProjects(), DEFAULT_SWR_OPTIONS);
}

export function useEmgSuppliersCache(enabled = true) {
  const { user, isLoggedIn } = useAuth();
  const allowed = enabled && isLoggedIn && (hasPermission(user, 'suppliers.view') || hasPermission(user, 'suppliers.manage'));
  const key = allowed && user?.id ? ['emg/suppliers', user.id] : null;
  return useSWR<EmgSupplier[]>(key, () => getEmgSuppliers(), DEFAULT_SWR_OPTIONS);
}

export function useTodayAttendanceCache(enabled = true) {
  const { user, isLoggedIn } = useAuth();
  const allowed = enabled && isLoggedIn && (hasPermission(user, 'attendance.view') || hasPermission(user, 'attendance.manage'));
  const key = allowed && user?.id ? ['emg/attendance/today', user.id] : null;
  return useSWR<EmgAttendance | null>(key, () => getTodayAttendance(), {
    ...DEFAULT_SWR_OPTIONS,
    dedupingInterval: 60_000
  });
}

export function useEmgAdvancesCache(projectId?: number, enabled = true) {
  const { user, isLoggedIn } = useAuth();
  const allowed = enabled && isLoggedIn && (hasPermission(user, 'advances.view') || hasPermission(user, 'advances.manage'));
  const key = allowed && user?.id ? ['emg/advances', user.id, projectId ?? 'all'] : null;
  return useSWR<EmgAdvance[]>(key, () => getEmgAdvances(projectId), DEFAULT_SWR_OPTIONS);
}

export function invalidateEmgCache(prefix: 'emg/projects' | 'emg/suppliers' | 'emg/attendance/today' | 'emg/advances') {
  return globalMutate((key) => Array.isArray(key) && key[0] === prefix);
}
