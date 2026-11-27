import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EmgProject } from 'api/emg';
import useAuth from 'hooks/useAuth';
import { useEmgProjectsCache } from 'hooks/useEmgData';

type ActiveProjectContextValue = {
  projects: EmgProject[];
  activeProjectId: number | null;
  activeProject: EmgProject | null;
  setActiveProjectId: (projectId: number | null) => void;
  loading: boolean;
  failed: boolean;
  refreshProjects: () => Promise<EmgProject[] | undefined>;
};

const ActiveProjectContext = createContext<ActiveProjectContextValue>({
  projects: [],
  activeProjectId: null,
  activeProject: null,
  setActiveProjectId: () => undefined,
  loading: false,
  failed: false,
  refreshProjects: async () => undefined
});

function storageKeyForUser(userId?: string | number | null): string | null {
  if (!userId) return null;
  return `emg-active-project:${userId}`;
}

export function ActiveProjectProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const { data: projects = [], isLoading, error, mutate } = useEmgProjectsCache(true);
  const storageKey = storageKeyForUser(user?.id);

  const [storedId, setStoredId] = useState<number | null>(() => {
    if (!storageKey) return null;
    try {
      const raw = window.localStorage.getItem(storageKey);
      const parsed = raw ? Number(raw) : NaN;
      return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
    } catch {
      return null;
    }
  });

  // Sync when user changes (per-authenticated-user persistence)
  useEffect(() => {
    if (!storageKey) {
      setStoredId(null);
      return;
    }
    try {
      const raw = window.localStorage.getItem(storageKey);
      const parsed = raw ? Number(raw) : NaN;
      setStoredId(Number.isInteger(parsed) && parsed > 0 ? parsed : null);
    } catch {
      setStoredId(null);
    }
  }, [storageKey]);

  const setActiveProjectId = useCallback(
    (projectId: number | null) => {
      setStoredId(projectId);
      if (!storageKey) return;
      try {
        if (projectId && projectId > 0) {
          window.localStorage.setItem(storageKey, String(projectId));
        } else {
          window.localStorage.removeItem(storageKey);
        }
      } catch {
        // Ignore storage quota / private mode failures
      }
    },
    [storageKey]
  );

  // Scope-safe resolution: never expose a project not in the user's authorized projects list
  const activeProject = useMemo(() => {
    if (projects.length === 0) return null;
    if (storedId) {
      const match = projects.find((p) => p.id === storedId);
      if (match) return match;
    }
    return projects.find((p) => p.status === 'نشط') || projects[0];
  }, [projects, storedId]);

  const activeProjectId = activeProject?.id ?? null;

  useEffect(() => {
    if (activeProjectId && activeProjectId !== storedId && storageKey) {
      try {
        window.localStorage.setItem(storageKey, String(activeProjectId));
      } catch {
        // Ignore
      }
    }
  }, [activeProjectId, storedId, storageKey]);

  const refreshProjects = useCallback(() => mutate(), [mutate]);

  const value = useMemo(
    () => ({
      projects,
      activeProjectId,
      activeProject,
      setActiveProjectId,
      loading: isLoading,
      failed: Boolean(error),
      refreshProjects
    }),
    [projects, activeProjectId, activeProject, setActiveProjectId, isLoading, error, refreshProjects]
  );

  return <ActiveProjectContext.Provider value={value}>{children}</ActiveProjectContext.Provider>;
}

export function useActiveProject() {
  return useContext(ActiveProjectContext);
}
