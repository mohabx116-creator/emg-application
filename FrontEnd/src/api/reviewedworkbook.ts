import axios from 'utils/axios';

export type WorkbookControl = {
  sectionId: string;
  streamKey: string;
  currency: string;
  previewAmount: number;
  sourceSummaryAmount: number | null;
  difference: number | null;
  status: string;
  evidenceCoordinate: string;
};

export type WorkbookSourceRecord = {
  recordId: string;
  sectionId: string;
  sheetName: string;
  sourceCoordinate: string;
  sourceCells: string[];
  recordType: number;
  currency: string;
  amount: number | null;
  isSummary: boolean;
  eligibleForPosting: boolean;
  operationalEvidence: {
    target: string;
    date: string;
    description: string;
    quantity: number | null;
    unit: string | null;
    unitPrice: number | null;
    party: string | null;
    reference: string | null;
    amountCell: string;
    requiresManualMapping: boolean;
  } | null;
};

export type ReviewedWorkbookPreview = {
  snapshotId: string;
  projectId: number;
  projectCode: string;
  projectName: string;
  workbook: {
    fileName: string;
    sourceSha256: string;
    sourceProjectCode: string | null;
    sections: { sectionId: string; sheetName: string; sourceRange: string; mappingFamily: string; requiresMapping: boolean }[];
    records: WorkbookSourceRecord[];
    duplicateCandidates: { recordIds: string[] }[];
    reconciliations: WorkbookControl[];
  };
};

export type ReviewedWorkbookBatch = {
  id: number;
  projectId: number;
  sourceFileName: string;
  sourceSha256: string;
  status: string;
  canApprove: boolean;
  canPost: boolean;
  canRevise: boolean;
  version: number;
  originalRecordCount: number;
  totals: { target: string; currency: string; selectedCount: number; selectedAmount: number; postedCount: number; postedAmount: number }[];
  records: {
    id: number;
    sourceCoordinate: string;
    recordType: string;
    currency: string;
    amount: number | null;
    target: string | null;
    targetId: number | null;
    status: string;
    reason: string | null;
    canSelect: boolean;
    needsMappingEvidence: boolean;
  }[];
  reconciliations: WorkbookControl[];
};

const base = '/api/universal-import/review';

export async function previewReviewedWorkbook(projectId: number, file: File) {
  const form = new FormData();
  form.append('projectId', String(projectId));
  form.append('file', file);
  return (await axios.post<ReviewedWorkbookPreview>(`${base}/preview`, form)).data;
}

export async function stageReviewedWorkbook(snapshotId: string, sourceApprovalReference: string, confirmedProject: boolean,
  confirmedRecordIds: string[], mappingEvidence: string) {
  return (await axios.post<ReviewedWorkbookBatch>(`${base}/batches`, {
    snapshotId, sourceApprovalReference, confirmedProject, confirmedRecordIds, mappingEvidence
  })).data;
}

export async function listReviewedWorkbookBatches(projectId: number) {
  return (await axios.get<number[]>(`${base}/projects/${projectId}/batches`)).data;
}

export async function getReviewedWorkbookBatch(batchId: number) {
  return (await axios.get<ReviewedWorkbookBatch>(`${base}/batches/${batchId}`)).data;
}

export async function advanceReviewedWorkbook(batchId: number, action: 'approve' | 'post', version: number) {
  return (await axios.post<ReviewedWorkbookBatch>(`${base}/batches/${batchId}/${action}`, undefined, { params: { version } })).data;
}

export async function reviseReviewedWorkbook(batchId: number, version: number, recordId: number, selected: boolean, evidence: string) {
  return (await axios.put<ReviewedWorkbookBatch>(`${base}/batches/${batchId}/mapping`, { version, recordId, selected, evidence })).data;
}

export type HistoricalSettlementRecord = {
  recordId: number;
  sheetName: string;
  sourceCoordinate: string;
  sourceCells: string[];
  mappingFamily: string;
  stream: string;
  businessEventType: string;
  amount: number | null;
  currency: string;
  target: string | null;
  party: string | null;
  reference: string | null;
  semanticValidationState: string;
  lifecycleStatus: string;
  isReferenceOnly: boolean;
  isDuplicateCandidate: boolean;
  reviewReason: string | null;
  linkedObligationType: string | null;
  linkedObligationId: number | null;
  linkedObligationAmount: number | null;
  operationalPaymentId: number | null;
  operationalPaymentStatus: string | null;
};

export type HistoricalSettlementReview = {
  batchId: number;
  projectId: number;
  sourceFileName: string;
  sourceSha256: string;
  batchStatus: string;
  version: number;
  records: HistoricalSettlementRecord[];
};

export async function listHistoricalSettlementReviews(projectId: number) {
  return (await axios.get<HistoricalSettlementReview[]>(`${base}/projects/${projectId}/settlements`)).data;
}

export async function reviewHistoricalSettlement(batchId: number, version: number, recordId: number, decision: 'confirm' | 'reject', evidence: string, obligationId?: number, obligationAmount?: number, party?: string) {
  return (await axios.put<HistoricalSettlementReview>(`${base}/batches/${batchId}/settlements`, {
    version, recordId, decision, evidence,
    obligationType: obligationId ? 'SupplierSupply' : undefined,
    obligationId, obligationAmount, party
  })).data;
}

export async function recognizeHistoricalSettlement(batchId: number, recordId: number) {
  return (await axios.post<HistoricalSettlementReview>(`${base}/batches/${batchId}/settlements/${recordId}/recognize`)).data;
}
