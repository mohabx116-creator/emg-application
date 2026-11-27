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

export async function getSectionWork<T extends { id: number }>(projectId: number, section: WorkSectionKey, operational: Promise<T[]>) {
  const [records, { data }] = await Promise.all([
    operational,
    axios.get<{ rows: HistoricalWork[]; linkedOperationalIds: number[] }>(`/api/projects/${projectId}/historical-work/${section}`)
  ]);
  const linked = new Set(data.linkedOperationalIds);
  return { records: records.filter(record => !linked.has(record.id)), historical: data.rows };
}

export async function getHistoricalAccount(type: 'suppliers' | 'contractors', id: number, signal: AbortSignal) {
  const response = await axios.get<{ works: HistoricalWork[]; settlements: HistoricalSettlement[] }>(
    `/api/${type}/${id}/historical-account`, { signal, validateStatus: status => status === 200 || status === 404 }
  );
  return response.status === 404 ? { works: [], settlements: [] } : response.data;
}