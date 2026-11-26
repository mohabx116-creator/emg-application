import axios from 'utils/axios';

export type EmgProject = {
  id: number;
  name: string;
  code: string;
  clientName: string;
  location: string;
  status: string;
  engineerName: string;
  openMovements: number;
  totalIssued: number;
  totalReceived: number;
  totalWorks: number;
  totalPayments: number;
  cashBalance: number;
  cashBalanceStatus: string;
};

export type EmgWorkbookSheetPreview = {
  order: number;
  originalName: string;
  classification: string;
  recognized: boolean;
  isSourceData: boolean;
};

export type EmgWorkbookPreview = {
  fileName: string;
  sizeBytes: number;
  isValid: boolean;
  requiresReview: boolean;
  sheets: EmgWorkbookSheetPreview[];
  warnings: string[];
};

export type EmgWorkbookMigrationPlan = {
  mode: string;
  suggestedProjectName: string;
  suggestedProjectCode: string;
  suggestedClientName?: string;
  suggestedOwnerName?: string;
  suggestedPayerName?: string;
  suggestedLocation?: string;
  sheets: Array<{ order: number; originalName: string; classification: string; recognized: boolean; willCreateData: boolean; sourceRows: number; candidateRows: number; detectionProfile: string; confidence: string; diagnostics?: string[] }>;
  warnings: string[];
};

export type EmgFinalAccount = {
  sections?: Array<{ category: string; currency: string; works: number; allocatedFunding: number; fundingLessWorks: number }>;
  historicalFundingAdjustments?: number;
  projectId: number;
  projectName: string;
  clientName: string;
  payerName: string;
  officeSharePercentage: number;
  totalIncoming: number;
  totalExpenses: number;
  supplierOutstanding: number;
  netBeforeOfficeShare: number;
  officeShareAmount: number;
  netAfterOfficeShare: number;
  payerBalance: number;
  balanceStatus: string;
  status: string;
  totalOutgoing?: number;
  officePaid?: number;
  officeNetDue?: number;
  hasUnresolvedImportRecords?: boolean;
  outgoingByCategory?: Record<string, number>;
  incomingByCategory?: Record<string, number>;
  totalWorks?: number;
  totalPayments?: number;
  cashBalance?: number;
  cashBalanceStatus?: string;
};

export type EmgProjectReport = {
  finalAccount: EmgFinalAccount;
  movements: Array<{
    id: number;
    date: string;
    type: string;
    description: string;
    supplierName: string;
    amount: number;
    status: string;
    reference: string;
  }>;
  advances: Array<{
    id: number;
    date: string;
    clientName: string;
    payerName: string;
    amount: number;
    allocated: number;
    remaining: number;
    status: string;
    reference: string;
  }>;
  supplierSupplies: Array<{
    id: number;
    date: string;
    supplierName: string;
    supplyType: string;
    itemName: string;
    unit: string;
    quantity: number;
    unitPrice: number;
    totalAmount: number;
    invoiceNumber: string;
  }>;
  supplierPayments: Array<{ id: number; date: string; supplierName: string; amount: number; method: string; reference: string }>;
};

export type EmgHistoricalProjectReport = {
  projectId: number;
  totals: Array<{ ledgerStream: string; currency: string; amount: number; entryCount: number }>;
  openingBalances: Array<{ streamKey: string; currency: string; amount: number; sourceReference: string; provenanceJson: string }>;
  entries: Array<{ id: number; batchId: number; sourceFileName: string; sourceSha256: string; sheetName: string; sourceCoordinate: string; mappingFamily: string; businessEventType: string; ledgerStream: string; amount: number; currency: string; postedAtUtc: string; provenanceJson: string }>;
};

export type EmgContractor = { id: number; name: string; phone: string; taxNumber: string; notes: string; isActive: boolean };
export type EmgContract = { id: number; projectId: number; projectName: string; contractorId: number; contractorName: string; contractNumber: string; currency: string; status: string; itemCount: number };
export type EmgContractItem = { id: number; contractId: number; description: string; unit: string; approvedQuantity: number; unitRate: number; sourceReference: string; usedQuantity: number; remainingQuantity: number };
export type EmgContractorStatement = { id: number; projectId: number; projectName: string; contractorId: number; contractorName: string; contractId: number; contractNumber: string; periodFrom: string; periodTo: string; currency: string; status: string; grossAmount: number; previousPaymentsAmount: number; deductionsAmount: number; netAmount: number; sourceReference: string; lines: Array<{ id: number; contractItemId: number; description: string; sourceEntityType: string; sourceEntityId: number; quantity: number; unit: string; unitRate: number; grossAmount: number; sourceReference: string }> };
export type EmgContractorPayment = { id: number; projectId: number; contractorId: number; contractorStatementId: number | null; amount: number; currency: string; paymentDate: string; paymentMethod: string; paymentSource: string; reference: string; notes: string };
export type EmgEligibleContractorWork = { sourceEntityType: string; sourceEntityId: number; description: string; unit: string; quantity: number; unitRate: number; status: string; sourceReference: string; alreadyUsed: boolean };

export type EmgOperationalAudit = {
  projectId: number;
  excavationTotal: number;
  concreteTotal: number;
  steelTotal: number;
  finishingTotal: number;
  supplierSuppliesTotal: number;
  supplierPaymentsTotal: number;
  checks: Array<{ name: string; isValid: boolean; difference: number; message: string }>;
};

export type EmgStatementColumn = {
  column: string;
  heading: string;
  importedAmount: number | null;
  currentAmount: number | null;
  diagnosticAmount: number | null;
  unresolvedAmount: number;
  unresolvedCount: number;
  isOfficial: boolean;
  status: string;
  diagnostic: string;
};

export type EmgStatementDimension = {
  importedAmount: number;
  currentAmount: number;
  importedCount: number;
  currentCount: number;
  meaning: string;
};

export type EmgConsolidatedStatementRow = {
  projectId: number;
  projectCode: string;
  projectName: string;
  columns: EmgStatementColumn[];
  dimensions: {
    operationalCosts: EmgStatementDimension;
    supplierSupplies: EmgStatementDimension;
    supplierPayments: EmgStatementDimension;
    ownerFunding: EmgStatementDimension;
    advanceAllocations: EmgStatementDimension;
    officePayments: EmgStatementDimension;
    unresolvedFinancialRecords: EmgStatementDimension;
  };
  officeAccrual: {
    diagnosticAmount: number | null;
    officeSharePercentage: number;
    basisAmount: number;
    isOfficial: boolean;
    status: string;
    diagnostic: string;
  };
  netTransactionDiagnostics: Array<{ scenario: string; amount: number | null; formula: string; status: string }>;
  unresolvedRecords: Array<{
    entityType: string;
    entityId: number;
    amount: number;
    status: string;
    sourceText: string;
    sourceSheet: string;
    sourceRow: number | null;
    diagnostic: string;
  }>;
  isComplete: boolean;
  completenessStatus: string;
};

export type EmgConsolidatedStatementPreview = {
  generatedAtUtc: string;
  snapshotRule: string;
  projects: EmgConsolidatedStatementRow[];
};

export type EmgReportBuilderQuery = {
  projectIds?: number[] | null;
  fromDate?: string | null;
  toDate?: string | null;
  categories?: string[] | null;
  parties?: string[] | null;
  dimensions?: string[] | null;
  detailLevel: 'totals' | 'transactions' | 'both';
  groupBy?: string[] | null;
  columns?: string[] | null;
  snapshot: 'imported' | 'current' | 'both';
  sortBy: 'project' | 'category' | 'party' | 'date' | 'amount';
  includeUnresolved: boolean;
};

export type EmgReportBuilderPresentation = 'detailed' | 'consolidated' | 'custom';

export type EmgReportBuilderPreview = {
  generatedAtUtc: string;
  query: EmgReportBuilderQuery;
  visibleColumns: string[];
  summary: Array<{
    groupKey: string;
    groupLabel: string;
    importedAmount: number;
    currentAmount: number;
    unresolvedAmount: number;
    recordCount: number;
    unresolvedCount: number;
    isComplete: boolean;
  }>;
  transactions: Array<{
    projectId: number;
    projectCode: string;
    projectName: string;
    entityType: string;
    entityId: number;
    category: string;
    dimension: string;
    party: string;
    transactionDate: string | null;
    importedAmount: number;
    currentAmount: number;
    unresolvedAmount: number;
    status: string;
    sourceText: string;
    sourceSheet: string;
    sourceRow: number | null;
    isUnresolved: boolean;
    diagnostic: string;
  }>;
  warnings: string[];
  isComplete: boolean;
};

export type EmgSiteMovement = {
  id: number;
  projectId: number;
  projectName: string;
  supplierId: number | null;
  supplierName: string;
  contractorId: number | null;
  contractorName: string;
  counterpartyType: string;
  currency: string;
  fundingSource: string;
  recipient: string;
  workCategory: string;
  movementType: string;
  description: string;
  unit: string;
  quantity: number | null;
  unitPrice: number | null;
  totalAmount: number;
  movementDate: string;
  status: string;
  reference: string;
  details: string;
  expenseCategory: string;
  paidTo: string;
  paymentMethod: string;
  isPaid: boolean;
  invoiceId?: number | null;
};
export type EmgEntrySuggestion = {
  suggestedMovementType: string;
  suggestedEntryKind: string;
  suggestedExpenseCategory: string;
  suggestedUnit: string;
  matchedSupplierName: string | null;
  confidence: number;
  requiresConfirmation: boolean;
  alternatives: string[];
  checks: string[];
  explanation: string;
};
export type EmgAttachment = {
  id: number;
  entityType: string;
  entityId: number;
  originalFileName: string;
  contentType: string;
  sizeInBytes: number;
  uploadedByUserId: number | null;
  uploadedAtUtc: string;
};
export type EmgWorkflowResult = {
  entityType: string;
  entityId: number;
  previousStatus: string;
  currentStatus: string;
  action: string;
  actionDateUtc: string;
};
export type EmgWorkflowAudit = {
  entityType: string;
  entityId: number;
  status: string;
  canApprove: boolean;
  canPost: boolean;
  canSubmit: boolean;
  canAcceptTechnically: boolean;
  canApproveFinancially: boolean;
  canReject: boolean;
  canCorrectSupply: boolean;
  rejectionReason: string | null;
  blockingChecks: string[];
  warnings: string[];
};
export type EmgWorkflowReviewItem = {
  entityType: string;
  entityId: number;
  projectId: number;
  projectName: string;
  description: string;
  amount: number;
  status: string;
  date: string;
  hasAttachment: boolean;
  canApprove: boolean;
  canPost: boolean;
  canAcceptTechnically: boolean;
  canApproveFinancially: boolean;
  canReject: boolean;
  blockingChecks: string[];
  warnings: string[];
};

export type EmgSupplier = {
  id: number;
  name: string;
  supplierType: string;
  phone: string;
  taxNumber: string;
  notes: string;
  isActive: boolean;
  suppliesCount: number;
  totalSupplies: number;
  totalPaid: number;
  remaining: number;
};
export type EmgSupply = {
  id: number;
  supplierId: number;
  supplierName: string;
  projectId: number;
  projectName: string;
  supplyType: string;
  itemName: string;
  unit: string;
  quantity: number;
  unitPrice: number;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  supplyDate: string;
  invoiceNumber: string;
  notes: string;
  status: string;
};
export type EmgSupplyCorrection = Pick<EmgSupply, 'supplyType' | 'itemName' | 'unit' | 'quantity' | 'unitPrice' | 'invoiceNumber'>;
export type EmgSupplierInvoice = {
  id: number;
  supplierId: number;
  projectId: number;
  siteMovementId: number | null;
  invoiceNumber: string;
  currency: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  lines: Array<{ id: number; supplyId: number; description: string; unit: string; quantity: number; unitPrice: number; totalAmount: number }>;
};
export type EmgExcavation = {
  id: number;
  projectId: number;
  projectName: string;
  supplierId: number | null;
  supplierName: string;
  calculationMethod: string;
  length: number | null;
  width: number | null;
  depth: number | null;
  calculatedQuantity: number;
  unit: string;
  unitPrice: number;
  totalAmount: number;
  soilType: string;
  description: string;
  excavationDate: string;
  status: string;
  notes: string;
};
export type EmgConcrete = {
  id: number;
  projectId: number;
  projectName: string;
  supplierId: number | null;
  supplierName: string;
  concreteType: string;
  elementType: string;
  mixGrade: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalAmount: number;
  pourDate: string;
  deliveryNoteNumber: string;
  description: string;
  status: string;
  notes: string;
};
export type EmgFinishing = {
  id: number;
  projectId: number;
  projectName: string;
  supplierId: number | null;
  supplierName: string;
  workType: string;
  areaName: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalAmount: number;
  workDate: string;
  description: string;
  status: string;
  notes: string;
};
export type EmgSteel = {
  id: number;
  projectId: number;
  projectName: string;
  supplierId: number | null;
  supplierName: string;
  operationType: string;
  steelType: string;
  barDiameter: string;
  grade: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  totalAmount: number;
  operationDate: string;
  deliveryNoteNumber: string;
  description: string;
  status: string;
  notes: string;
};
export type EmgSupplierPayment = {
  id: number;
  supplierId: number;
  projectId: number | null;
  projectName: string;
  supplyId: number | null;
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  reference: string;
  notes: string;
  status: string;
  paymentSource: string;
};
export type EmgAdvance = {
  id: number;
  projectId: number;
  projectName: string;
  clientName: string;
  payerName: string;
  amount: number;
  allocatedAmount: number;
  remainingAmount: number;
  unallocatedAmount: number;
  dueFromPayer: number;
  balanceStatus: string;
  paymentDate: string | null;
  paymentMethod: string;
  reference: string;
  status: string;
  notes: string;
};
export type EmgAdvanceAllocation = {
  id: number;
  advancePaymentId: number;
  siteMovementId: number | null;
  supplierPaymentId: number | null;
  accountCategory: string;
  amount: number;
  allocationDate: string;
  notes: string;
};
export type EmgAttendance = {
  id: number;
  userId: number;
  userName: string;
  attendanceDate: string;
  checkInAtUtc: string;
  checkOutAtUtc: string | null;
  status: string;
  notes: string;
};
export type EmgNotification = {
  id: number;
  type: string;
  title: string;
  message: string;
  entityType: string;
  entityId: number | null;
  isRead: boolean;
  createdAtUtc: string;
};
export type EmgSearchResult = {
  entityType: string;
  entityId: number;
  title: string;
  subtitle: string;
  projectId: number | null;
  status: string;
};
export type EmgProfile = {
  id: number;
  userName: string;
  email: string;
  displayName: string;
  profileImagePath: string | null;
  mustChangePassword: boolean;
};
export type EmgUser = {
  id: number;
  userName: string;
  email: string;
  displayName: string;
  isActive: boolean;
  mustChangePassword: boolean;
  roles: string[];
  projectIds: number[];
};
export type EmgPermission = {
  id: number;
  code: string;
  displayName: string;
  scope?: 'all' | 'assigned' | 'own';
  amountLimit?: number | null;
};
export type EmgRole = { id: number; name: string; displayName: string; permissions: EmgPermission[] };
export type EmgSecurityAuditLog = {
  id: number;
  action: string;
  entityType: string;
  entityId: number | null;
  details: string;
  createdAtUtc: string;
  userName: string;
};

export async function getEmgProjects(): Promise<EmgProject[]> {
  const response = await axios.get<EmgProject[]>('/api/projects');
  return response.data;
}

export async function getEmgConsolidatedStatementPreview(): Promise<EmgConsolidatedStatementPreview> {
  return (await axios.get<EmgConsolidatedStatementPreview>('/api/reports/consolidated/master-statement/preview')).data;
}

export async function runEmgReportBuilder(query: EmgReportBuilderQuery): Promise<EmgReportBuilderPreview> {
  return (await axios.post<EmgReportBuilderPreview>('/api/reports/builder/preview', query)).data;
}

export async function downloadEmgReportBuilderWorkbook(query: EmgReportBuilderQuery, presentation: EmgReportBuilderPresentation): Promise<void> {
  let workbook: Blob;
  try {
    const response = await axios.post<Blob>('/api/reports/builder/export.xlsx', { query, presentation }, { responseType: 'blob' });
    workbook = response.data;
  } catch (reason) {
    let message = 'تعذّر إنشاء ملف Excel. تحقق من الفلاتر وصلاحية التصدير.';
    if (reason instanceof Blob && reason.type.includes('json')) {
      const error = await reason.text().then(text => JSON.parse(text)).catch(() => null);
      if (typeof error?.message === 'string') message = error.message;
    }
    throw new Error(message);
  }
  if (workbook.type !== 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' || workbook.size === 0) {
    throw new Error('استجابة التصدير ليست ملف Excel صالحًا.');
  }
  const url = URL.createObjectURL(workbook);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = 'emg-financial-report.xlsx';
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function previewEmgWorkbook(file: File): Promise<EmgWorkbookPreview> {
  const body = new FormData();
  body.append('file', file);
  return (await axios.post<EmgWorkbookPreview>('/api/imports/workbook-preview', body)).data;
}

export async function buildEmgNewProjectPlan(file: File): Promise<EmgWorkbookMigrationPlan> {
  const body = new FormData();
  body.append('file', file);
  return (await axios.post<EmgWorkbookMigrationPlan>('/api/imports/new-project-plan', body)).data;
}

export async function buildEmgExistingProjectPlan(file: File, projectId: number): Promise<EmgWorkbookMigrationPlan> {
  const body = new FormData();
  body.append('file', file);
  body.append('projectId', String(projectId));
  return (await axios.post<EmgWorkbookMigrationPlan>('/api/imports/existing-project-plan', body)).data;
}

export async function commitEmgNewProject(file: File, values: { name?: string; code?: string; clientName?: string; location?: string; engineerName?: string }) {
  const body = new FormData();
  body.append('file', file);
  if (values.name) body.append('projectName', values.name);
  if (values.code) body.append('projectCode', values.code);
  if (values.clientName) body.append('clientName', values.clientName);
  if (values.location) body.append('location', values.location);
  if (values.engineerName) body.append('engineerName', values.engineerName);
  return (await axios.post<{ projectId: number; name: string; code: string; sourceFileName: string; attachmentId: number; status: string; siteMovementsImported: number; excavationRecordsImported: number; concreteRecordsImported: number; steelRecordsImported: number; finishingRecordsImported: number; supplierSuppliesImported: number; supplierPaymentsImported: number; advancePaymentsImported: number; advanceAllocationsImported: number }>('/api/imports/commit-new-project', body)).data;
}

export async function commitEmgExistingProject(file: File, projectId: number) {
  const body = new FormData();
  body.append('file', file);
  body.append('projectId', String(projectId));
  return (await axios.post<{ projectId: number; name: string; code: string; sourceFileName: string; attachmentId: number; status: string; siteMovementsImported: number; excavationRecordsImported: number; concreteRecordsImported: number; steelRecordsImported: number; finishingRecordsImported: number; supplierSuppliesImported: number; supplierPaymentsImported: number; advancePaymentsImported: number; advanceAllocationsImported: number }>('/api/imports/existing-project-update', body)).data;
}

export type EmgApprovedHistoryResult = {
  projectId: number;
  name: string;
  code: string;
  sourceFileName: string;
  attachmentId: number;
  siteMovementsImported: number;
  supplierSuppliesImported: number;
  supplierPaymentsImported: number;
  advancePaymentsImported: number;
};

export async function commitEmgApprovedHistory(file: File, projectId: number | null, closedThrough: string, confirmedApproved: boolean) {
  const form = new FormData();
  form.append('file', file);
  if (projectId !== null) form.append('projectId', String(projectId));
  form.append('closedThrough', closedThrough);
  form.append('confirmedApproved', String(confirmedApproved));
  return (await axios.post<EmgApprovedHistoryResult>('/api/imports/approved-history', form)).data;
}

export async function createEmgProject(payload: Record<string, unknown>): Promise<EmgProject> {
  const response = await axios.post<EmgProject>('/api/projects', payload);
  return response.data;
}

export async function getEmgProject(id: number): Promise<EmgProject> {
  const response = await axios.get<EmgProject>(`/api/projects/${id}`);
  return response.data;
}

export async function approveEmgProjectImport(id: number, notes?: string): Promise<EmgProject> {
  return (await axios.post<EmgProject>(`/api/projects/${id}/import-approval`, { notes })).data;
}

export async function rejectEmgProjectImport(id: number, notes: string): Promise<EmgProject> {
  return (await axios.post<EmgProject>(`/api/projects/${id}/import-rejection`, { notes })).data;
}

export async function getEmgProjectReport(id: number): Promise<EmgProjectReport> {
  const response = await axios.get<EmgProjectReport>(`/api/reports/projects/${id}`);
  return response.data;
}

export async function getEmgHistoricalProjectReport(id: number): Promise<EmgHistoricalProjectReport> {
  return (await axios.get<EmgHistoricalProjectReport>(`/api/reports/projects/${id}/historical`)).data;
}

export async function getEmgProjectAudit(id: number): Promise<EmgOperationalAudit> {
  const response = await axios.get<EmgOperationalAudit>(`/api/reports/projects/${id}/audit`);
  return response.data;
}

export async function getEmgRoleAudit(): Promise<EmgSecurityAuditLog[]> {
  const response = await axios.get<EmgSecurityAuditLog[]>('/api/roles/audit');
  return response.data;
}

export async function downloadEmgProjectWorkbook(id: number): Promise<void> {
  const response = await axios.get(`/api/reports/projects/${id}.xlsx`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `project-${id}-report.xlsx`;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(url);
    anchor.remove();
  }, 1000);
}
export async function downloadEmgProjectMovementsCsv(id: number): Promise<void> {
  const response = await axios.get(`/api/reports/projects/${id}/movements.csv`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `project-${id}-movements.csv`;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(url);
    anchor.remove();
  }, 1000);
}

export async function getEmgSiteMovements(projectId: number): Promise<EmgSiteMovement[]> {
  const response = await axios.get<EmgSiteMovement[]>('/api/site-movements', { params: { projectId } });
  return response.data;
}
export async function getEmgSiteMovement(id: number): Promise<EmgSiteMovement> {
  return (await axios.get<EmgSiteMovement>(`/api/site-movements/${id}`)).data;
}
export async function updateEmgSiteMovement(id: number, payload: Record<string, unknown>): Promise<EmgSiteMovement> {
  return (await axios.put<EmgSiteMovement>(`/api/site-movements/${id}`, payload)).data;
}
export async function deleteEmgSiteMovement(id: number): Promise<void> {
  await axios.delete(`/api/site-movements/${id}`);
}
export async function correctEmgTransaction(entityType: string, entityId: number, values: Record<string, unknown>, reason: string): Promise<void> {
  await axios.put(`/api/transactions/${encodeURIComponent(entityType)}/${entityId}`, { values, reason });
}
export async function deleteEmgTransaction(entityType: string, entityId: number): Promise<void> {
  await axios.delete(`/api/transactions/${encodeURIComponent(entityType)}/${entityId}`);
}
export async function cancelEmgTransaction(entityType: string, entityId: number, reason: string): Promise<void> {
  void reason;
  await deleteEmgTransaction(entityType, entityId);
}
export async function getEmgAttachments(entityType: string, entityId: number): Promise<EmgAttachment[]> {
  return (await axios.get<EmgAttachment[]>(`/api/attachments/${entityType}/${entityId}`)).data;
}
export async function getEmgWorkflowHistory(entityType: string, entityId: number): Promise<EmgWorkflowResult[]> {
  return (await axios.get<EmgWorkflowResult[]>(`/api/workflow/${entityType}/${entityId}/history`)).data;
}
export async function getEmgWorkflowAudit(entityType: string, entityId: number): Promise<EmgWorkflowAudit> {
  return (await axios.get<EmgWorkflowAudit>(`/api/workflow/${entityType}/${entityId}/audit`)).data;
}
export async function getEmgWorkflowReviewQueue(): Promise<EmgWorkflowReviewItem[]> {
  return (await axios.get<EmgWorkflowReviewItem[]>('/api/workflow/review-queue')).data;
}
export async function downloadEmgAttachment(id: number, fileName: string): Promise<void> {
  const response = await axios.get(`/api/attachments/${id}/download`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.style.display = 'none';
  document.body.appendChild(anchor);
  anchor.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(url);
    anchor.remove();
  }, 1000);
}
export async function getEmgAttachmentPreviewUrl(id: number): Promise<string> {
  const response = await axios.get(`/api/attachments/${id}/download`, { responseType: 'blob' });
  return URL.createObjectURL(response.data);
}

export async function createEmgSiteMovement(payload: Record<string, unknown>): Promise<EmgSiteMovement> {
  const response = await axios.post<EmgSiteMovement>('/api/site-movements', payload);
  return response.data;
}
export async function createEmgSiteMovementBatch(payload: { movements: Record<string, unknown>[] }): Promise<EmgSiteMovement[]> {
  const response = await axios.post<EmgSiteMovement[]>('/api/site-movements/batch', payload);
  return response.data;
}
export async function createEmgSupplierInvoice(payload: Record<string, unknown>): Promise<EmgSupplierInvoice> {
  const response = await axios.post<EmgSupplierInvoice>('/api/suppliers/invoices', payload);
  return response.data;
}
export async function deleteEmgSupplierInvoice(id: number): Promise<void> {
  await axios.delete(`/api/suppliers/invoices/${id}`);
}
export async function suggestEmgEntry(payload: Record<string, unknown>): Promise<EmgEntrySuggestion> {
  const response = await axios.post<EmgEntrySuggestion>('/api/intelligence/entry/suggest', payload);
  return response.data;
}
export async function uploadEmgAttachment(entityType: string, entityId: number, file: File): Promise<void> {
  const body = new FormData();
  body.append('file', file);
  await axios.post(`/api/attachments/${entityType}/${entityId}`, body, {
    timeout: 60_000,
    headers: { Accept: 'application/json' }
  });
}
export async function executeEmgWorkflow(entityType: string, entityId: number, action: string, notes?: string): Promise<void> {
  await axios.post(`/api/workflow/${entityType}/${entityId}/${encodeURIComponent(action)}`, { notes: notes || null });
}

export async function correctEmgSupply(id: number, correction: EmgSupplyCorrection): Promise<void> {
  await axios.post(`/api/workflow/supplier-supply/${id}/correction`, correction);
}

export async function getEmgSuppliers(): Promise<EmgSupplier[]> {
  const response = await axios.get<EmgSupplier[]>('/api/suppliers');
  return response.data;
}

export async function createEmgSupplier(payload: Record<string, unknown>): Promise<EmgSupplier> {
  const response = await axios.post<EmgSupplier>('/api/suppliers', payload);
  return response.data;
}

export async function getEmgSupplier(id: number): Promise<EmgSupplier> {
  return (await axios.get<EmgSupplier>(`/api/suppliers/${id}`)).data;
}
export async function getEmgSupplierSupplies(id: number): Promise<EmgSupply[]> {
  return (await axios.get<EmgSupply[]>(`/api/suppliers/${id}/supplies`)).data;
}
export async function getEmgSupplierPayments(id: number): Promise<EmgSupplierPayment[]> {
  return (await axios.get<EmgSupplierPayment[]>(`/api/suppliers/${id}/payments`)).data;
}
export async function createEmgSupply(id: number, payload: Record<string, unknown>): Promise<EmgSupply> {
  return (await axios.post<EmgSupply>(`/api/suppliers/${id}/supplies`, payload)).data;
}
export async function createEmgSupplierPayment(id: number, payload: Record<string, unknown>): Promise<EmgSupplierPayment> {
  return (await axios.post<EmgSupplierPayment>(`/api/suppliers/${id}/payments`, payload)).data;
}
export async function getEmgContractors(): Promise<EmgContractor[]> { return (await axios.get<EmgContractor[]>('/api/contractors')).data; }
export type EmgWorkCategory = { id: number; projectId: number | null; name: string; classification: string; isActive: boolean };
export async function getEmgWorkCategories(projectId?: number, classification?: string): Promise<EmgWorkCategory[]> {
  return (await axios.get<EmgWorkCategory[]>('/api/work-categories', { params: { projectId, classification } })).data;
}
export async function createEmgWorkCategory(payload: Record<string, unknown>): Promise<EmgWorkCategory> {
  return (await axios.post<EmgWorkCategory>('/api/work-categories', payload)).data;
}
export async function createEmgContractor(payload: Record<string, unknown>): Promise<EmgContractor> { return (await axios.post<EmgContractor>('/api/contractors', payload)).data; }
export async function getEmgContractorContracts(projectId?: number, contractorId?: number): Promise<EmgContract[]> { return (await axios.get<EmgContract[]>('/api/contractors/contracts', { params: { projectId, contractorId } })).data; }
export async function createEmgContract(contractorId: number, payload: Record<string, unknown>): Promise<EmgContract> { return (await axios.post<EmgContract>(`/api/contractors/${contractorId}/contracts`, payload)).data; }
export async function getEmgContractItems(contractId: number): Promise<EmgContractItem[]> { return (await axios.get<EmgContractItem[]>(`/api/contractors/contracts/${contractId}/items`)).data; }
export async function getEmgEligibleContractorWork(contractId: number): Promise<EmgEligibleContractorWork[]> { return (await axios.get<EmgEligibleContractorWork[]>(`/api/contractors/contracts/${contractId}/eligible-work`)).data; }
export async function createEmgContractItem(contractId: number, payload: Record<string, unknown>): Promise<EmgContractItem> { return (await axios.post<EmgContractItem>(`/api/contractors/contracts/${contractId}/items`, payload)).data; }
export async function getEmgContractorStatements(projectId?: number): Promise<EmgContractorStatement[]> { return (await axios.get<EmgContractorStatement[]>('/api/contractors/statements', { params: { projectId } })).data; }
export async function createEmgContractorDraft(payload: Record<string, unknown>): Promise<EmgContractorStatement> { return (await axios.post<EmgContractorStatement>('/api/contractors/statements/draft', payload)).data; }
export async function createEmgContractorPayment(contractorId: number, payload: Record<string, unknown>): Promise<EmgContractorPayment> { return (await axios.post<EmgContractorPayment>(`/api/contractors/${contractorId}/payments`, payload)).data; }
export async function downloadEmgContractorDraft(id: number): Promise<Blob> { return (await axios.get(`/api/contractors/statements/${id}.xlsx`, { responseType: 'blob' })).data; }
export async function getEmgExcavation(projectId: number): Promise<EmgExcavation[]> {
  return (await axios.get<EmgExcavation[]>('/api/excavation', { params: { projectId } })).data;
}
export async function createEmgExcavation(payload: Record<string, unknown>): Promise<EmgExcavation> {
  return (await axios.post<EmgExcavation>('/api/excavation', payload)).data;
}
export async function getEmgConcrete(projectId: number): Promise<EmgConcrete[]> {
  return (await axios.get<EmgConcrete[]>('/api/concrete', { params: { projectId } })).data;
}
export async function createEmgConcrete(payload: Record<string, unknown>): Promise<EmgConcrete> {
  return (await axios.post<EmgConcrete>('/api/concrete', payload)).data;
}
export async function getEmgFinishing(projectId: number): Promise<EmgFinishing[]> {
  return (await axios.get<EmgFinishing[]>('/api/finishing', { params: { projectId } })).data;
}
export async function createEmgFinishing(payload: Record<string, unknown>): Promise<EmgFinishing> {
  return (await axios.post<EmgFinishing>('/api/finishing', payload)).data;
}
export async function getEmgSteel(projectId: number): Promise<EmgSteel[]> {
  return (await axios.get<EmgSteel[]>('/api/steel', { params: { projectId } })).data;
}
export async function createEmgSteel(payload: Record<string, unknown>): Promise<EmgSteel> {
  return (await axios.post<EmgSteel>('/api/steel', payload)).data;
}
export async function getEmgAdvances(projectId?: number): Promise<EmgAdvance[]> {
  return (await axios.get<EmgAdvance[]>('/api/advances', { params: projectId ? { projectId } : undefined })).data;
}
export async function createEmgAdvance(payload: Record<string, unknown>): Promise<EmgAdvance> {
  return (await axios.post<EmgAdvance>('/api/advances', payload)).data;
}
export async function getEmgAdvance(id: number): Promise<EmgAdvance> {
  return (await axios.get<EmgAdvance[]>('/api/advances')).data.find((item) => item.id === id) as EmgAdvance;
}
export async function getEmgAdvanceAllocations(id: number): Promise<EmgAdvanceAllocation[]> {
  return (await axios.get<EmgAdvanceAllocation[]>(`/api/advances/${id}/allocations`)).data;
}
export async function allocateEmgAdvance(id: number, payload: Record<string, unknown>): Promise<EmgAdvanceAllocation> {
  return (await axios.post<EmgAdvanceAllocation>(`/api/advances/${id}/allocations`, payload)).data;
}
export async function getTodayAttendance(): Promise<EmgAttendance | null> {
  const response = await axios.get<EmgAttendance | null>('/api/attendance/today', {
    validateStatus: (status) => status === 200 || status === 204
  });
  return response.status === 204 ? null : response.data;
}
export async function checkInAttendance(notes?: string): Promise<EmgAttendance> {
  return (await axios.post<EmgAttendance>('/api/attendance/check-in', { notes })).data;
}
export async function checkOutAttendance(notes?: string): Promise<EmgAttendance> {
  return (await axios.post<EmgAttendance>('/api/attendance/check-out', { notes })).data;
}
export async function getEmgAttendance(date?: string, userId?: number): Promise<EmgAttendance[]> {
  return (await axios.get<EmgAttendance[]>('/api/attendance', { params: { date, userId } })).data;
}
export async function getEmgNotifications(unreadOnly = false): Promise<EmgNotification[]> {
  return (await axios.get<EmgNotification[]>('/api/notifications', { params: { unreadOnly } })).data;
}
export async function markEmgNotificationRead(id: number): Promise<void> {
  await axios.post(`/api/notifications/${id}/read`);
}
export async function markAllEmgNotificationsRead(): Promise<void> {
  await axios.post('/api/notifications/read-all');
}
export async function searchEmg(query: string): Promise<EmgSearchResult[]> {
  return (await axios.get<EmgSearchResult[]>('/api/search', { params: { q: query } })).data;
}
export async function getEmgProfile(): Promise<EmgProfile> {
  return (await axios.get<EmgProfile>('/api/account/profile')).data;
}
export async function updateEmgProfile(displayName: string): Promise<EmgProfile> {
  return (await axios.put<EmgProfile>('/api/account/profile', { displayName })).data;
}
export async function changeEmgPassword(currentPassword: string, newPassword: string): Promise<void> {
  await axios.post('/api/account/change-password', { currentPassword, newPassword });
}
export async function uploadEmgProfileImage(file: File): Promise<EmgProfile> {
  const body = new FormData();
  body.append('file', file);
  return (await axios.post<EmgProfile>('/api/account/profile-image', body)).data;
}
export async function getEmgUsers(): Promise<EmgUser[]> {
  return (await axios.get<EmgUser[]>('/api/users')).data;
}
export async function deleteUnusedEmgUser(userId: number): Promise<void> {
  await axios.delete(`/api/users/${userId}`);
}
export async function createEmgUser(payload: Record<string, unknown>): Promise<EmgUser> {
  return (await axios.post<EmgUser>('/api/users', payload)).data;
}
export async function updateEmgUser(id: number, payload: Record<string, unknown>): Promise<EmgUser> {
  return (await axios.put<EmgUser>(`/api/users/${id}`, payload)).data;
}
export async function assignEmgUserProject(userId: number, projectId: number): Promise<void> {
  await axios.post(`/api/users/${userId}/projects/${projectId}`);
}
export async function unassignEmgUserProject(userId: number, projectId: number): Promise<void> {
  await axios.delete(`/api/users/${userId}/projects/${projectId}`);
}
export async function resetEmgUserPassword(userId: number, password?: string): Promise<void> {
  await axios.post(`/api/users/${userId}/reset-password`, { password });
}
export async function getEmgRoles(): Promise<EmgRole[]> {
  return (await axios.get<EmgRole[]>('/api/roles')).data;
}
export async function getEmgPermissions(): Promise<EmgPermission[]> {
  return (await axios.get<EmgPermission[]>('/api/roles/permissions')).data;
}
export async function updateEmgRolePermissions(
  roleId: number,
  permissionIds: number[],
  grants?: Array<{ permissionId: number; scope: string; amountLimit?: number | null }>
): Promise<void> {
  await axios.put(`/api/roles/${roleId}/permissions`, { permissionIds, grants });
}
