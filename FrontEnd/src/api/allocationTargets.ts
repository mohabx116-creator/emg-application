import axios from 'utils/axios';

export type AllocationTarget = {
  kind: 'movement' | 'supplier';
  id: number;
  description: string;
  currency: string;
  amount: number;
  remaining: number;
};

export async function getAllocationTargets(advanceId: number) {
  return (await axios.get<AllocationTarget[]>(`/api/advances/${advanceId}/allocation-targets`)).data;
}