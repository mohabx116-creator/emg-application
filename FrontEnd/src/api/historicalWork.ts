import axios from 'utils/axios';
import type { WorkSectionKey } from 'Modules/EMG/sectionFinancials';

export type SourceReference = {
  id: number;
  projectId: number;
  projectName: string;
  sourceFileName: string;
  sourceSha256: string;
  sheet: string;
  cell: string;
  category: string;
  currency: string;
  amount: number;
  party: string;
};

export type HistoricalWork = SourceReference & {
  family: string;
  workDate: string | null;
  description: string;
  unit: string;
  quantity: number | null;
  rate: number | null;
  progress: number | null;
  supplierId: number | null;
  contractorId: number | null;
  operationalTable: string | null;
  operationalId: number | null;
};

export type HistoricalSettlement = SourceReference & {
  settlementDate: string | null;
  narrative: string;
};

export function describeSectionError(label: string, error: unknown): string {
  const status = (error as { status?: number } | null)?.status;
  if (status === 403) return `لا تملك صلاحية قراءة ${label} (HTTP 403).`;
  if (status === 404 && label === 'بيانات المشروع') return 'لم يُعثر على المشروع ضمن المشروعات المتاحة لحسابك (HTTP 404).';
  return status ? `تعذر تحميل ${label} (HTTP ${status}).` : `تعذر الاتصال بخدمة ${label}.`;
}

export async function getSectionWork<T extends { id: number }>(projectId: number, section: WorkSectionKey, operational: Promise<T[]>) {
  const [native, history] = await Promise.allSettled([
    operational,
    axios.get<{ rows: HistoricalWork[]; linkedOperationalIds: number[] }>(`/api/projects/${projectId}/historical-work/${section}`)
  ]);
  const warnings: string[] = [];
  if (native.status === 'rejected') warnings.push(describeSectionError('السجلات التشغيلية', native.reason));
  if (history.status === 'rejected') warnings.push(describeSectionError('تفاصيل الأعمال التاريخية', history.reason));
  const source = history.status === 'fulfilled' ? history.value.data : null;
  const linked = new Set(source?.linkedOperationalIds);
  return {
    records: native.status === 'fulfilled' ? native.value.filter(record => !linked.has(record.id)) : [],
    historical: source?.rows ?? [],
    warnings
  };
}

export async function getHistoricalAccount(type: 'suppliers' | 'contractors', id: number, signal: AbortSignal) {
  const response = await axios.get<{ works: HistoricalWork[]; settlements: HistoricalSettlement[] }>(
    `/api/${type}/${id}/historical-account`, { signal, validateStatus: status => status === 200 || status === 404 }
  );
  return response.status === 404 ? { works: [], settlements: [] } : response.data;
}