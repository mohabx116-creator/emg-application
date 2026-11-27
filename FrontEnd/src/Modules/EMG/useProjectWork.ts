import { useCallback, useEffect, useRef, useState } from 'react';
import { getEmgProject, type EmgProject } from 'api/emg';
import { describeSectionError, getSectionWork, type HistoricalWork } from 'api/historicalWork';
import type { WorkSectionKey } from './sectionFinancials';

export default function useProjectWork<T extends { id: number }>(
  id: number, section: WorkSectionKey, readOperational: (id: number) => Promise<T[]>
) {
  const sequence = useRef(0);
  const [project, setProject] = useState<EmgProject | null>(null);
  const [projectError, setProjectError] = useState('');
  const [details, setDetails] = useState<{ records: T[]; historical: HistoricalWork[]; warnings: string[] }>({
    records: [], historical: [], warnings: []
  });
  const [loading, setLoading] = useState(true);
  const load = useCallback(() => {
    const request = ++sequence.current;
    setLoading(true);
    setProjectError('');
    const current = () => request === sequence.current;
    if (!Number.isInteger(id) || id < 1) {
      setProject(null);
      setProjectError('رقم المشروع غير صالح.');
      setLoading(false);
      return;
    }
    const projectRequest = getEmgProject(id).then(data => {
      if (current()) setProject(data);
    }).catch(error => {
      if (current()) {
        setProject(null);
        setProjectError(describeSectionError('بيانات المشروع', error));
      }
    });
    const detailRequest = getSectionWork(id, section, readOperational(id)).then(data => {
      if (current()) setDetails(data);
    }).catch(error => {
      if (current()) setDetails({ records: [], historical: [], warnings: [describeSectionError('سجلات القسم', error)] });
    });
    void Promise.all([projectRequest, detailRequest]).finally(() => {
      if (current()) setLoading(false);
    });
  }, [id, section, readOperational]);

  useEffect(() => {
    setProject(null);
    setDetails({ records: [], historical: [], warnings: [] });
    load();
    return () => { sequence.current++; };
  }, [load]);
  return { project: project?.id === id ? project : null, projectError, ...details, loading, load };
}
