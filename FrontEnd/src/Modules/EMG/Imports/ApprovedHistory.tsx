import { useEffect, useMemo, useState } from 'react';
import {
  Alert, Box, Button, Checkbox, FormControlLabel, MenuItem, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TextField, Typography
} from '@mui/material';
import { Link } from 'react-router-dom';
import { getEmgProjects } from 'api/emg';
import type { EmgProject } from 'api/emg';
import {
  advanceReviewedWorkbook, getReviewedWorkbookBatch, listReviewedWorkbookBatches, previewReviewedWorkbook, reviseReviewedWorkbook, stageReviewedWorkbook
} from 'api/reviewedworkbook';
import type { ReviewedWorkbookBatch, ReviewedWorkbookPreview, WorkbookControl } from 'api/reviewedworkbook';
import MainCard from 'ui-component/cards/MainCard';

const targets: Record<string, string> = {
  SiteMovement: 'أعمال الموقع', ExcavationRecord: 'الحفر', ConcreteRecord: 'الخرسانة', SteelRecord: 'الحديد',
  FinishingRecord: 'التشطيبات', SupplierSupply: 'توريد مورد', AdvancePayment: 'تمويل المالك'
};
const types = ['حركة', 'تمويل', 'دفعة', 'مصروف', 'التزام', 'توزيع', 'رصيد افتتاحي', 'تسوية', 'قيمة مشتقة', 'إجمالي', 'غير محسوم'];
const statuses: Record<string, string> = {
  PREVIEW_ONLY: 'معاينة محفوظة', REVIEW_REQUIRED: 'تحتاج مراجعة', READY_FOR_APPROVAL: 'بانتظار الاعتماد',
  APPROVED_FOR_POSTING: 'معتمد للترحيل', POSTED: 'مرحّل', REJECTED: 'مرجعي أو مستبعد',
  MATCHED: 'مطابق', DIFFERENCE: 'فرق', NO_VERIFIED_SOURCE_CONTROL: 'لا يوجد إجمالي مصدر موثّق',
  NO_CACHED_SOURCE_CONTROL: 'الإجمالي بلا قيمة محفوظة في Excel'
};
const streams: Record<string, string> = {
  allocation_ledger: 'التوزيعات', main_payment_ledger: 'الدفعات الرئيسية',
  unallocated_multicurrency_ledger: 'الدفعات غير الموزعة', workbook_stream: 'حركات القسم'
};
const value = (amount: number | null) => amount === null ? '—' : amount.toLocaleString('en-US', { maximumFractionDigits: 8 });
const targetLabel = (target: string | null | undefined) => target ? targets[target] ?? target : '—';

function Reconciliation({ controls }: { controls: WorkbookControl[] }) {
  const [page, setPage] = useState(0);
  if (!controls.length) return null;
  return (
    <Box>
      <Typography variant="h4" gutterBottom>مطابقة إجماليات المصدر</Typography>
      <TableContainer>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>المسار</TableCell><TableCell>خلية التحكم</TableCell><TableCell>العملة</TableCell>
            <TableCell>Excel</TableCell><TableCell>المعاينة</TableCell><TableCell>الفرق</TableCell><TableCell>الحالة</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {controls.slice(page * 8, page * 8 + 8).map((row) => (
              <TableRow key={`${row.sectionId}-${row.currency}`}>
                <TableCell>{streams[row.streamKey] ?? row.streamKey}</TableCell><TableCell>{row.evidenceCoordinate || '—'}</TableCell>
                <TableCell>{row.currency}</TableCell><TableCell>{value(row.sourceSummaryAmount)}</TableCell>
                <TableCell>{value(row.previewAmount)}</TableCell><TableCell>{value(row.difference)}</TableCell>
                <TableCell>{statuses[row.status] ?? row.status}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination component="div" count={controls.length} page={page} rowsPerPage={8} rowsPerPageOptions={[]}
        onPageChange={(_, next) => setPage(next)} labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`} />
    </Box>
  );
}

export default function ApprovedHistoryImport() {
  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [projectId, setProjectId] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ReviewedWorkbookPreview | null>(null);
  const [batch, setBatch] = useState<ReviewedWorkbookBatch | null>(null);
  const [batches, setBatches] = useState<number[]>([]);
  const [confirmed, setConfirmed] = useState<string[]>([]);
  const [confirmedProject, setConfirmedProject] = useState(false);
  const [reference, setReference] = useState('');
  const [mappingEvidence, setMappingEvidence] = useState('');
  const [filter, setFilter] = useState('all');
  const [page, setPage] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    getEmgProjects().then((items) => { if (active) setProjects(items); })
      .catch(() => { if (active) setError('تعذر تحميل المشروعات.'); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    if (projectId) listReviewedWorkbookBatches(Number(projectId)).then((ids) => { if (active) setBatches(ids); })
      .catch(() => { if (active) setError('تعذر تحميل دفعات المشروع.'); });
    return () => { active = false; };
  }, [projectId]);

  const duplicateIds = useMemo(() => new Set(preview?.workbook.duplicateCandidates.flatMap((item) => item.recordIds)), [preview]);
  const sectionIndex = useMemo(() => new Map(preview?.workbook.sections.map((section) => [section.sectionId, section])), [preview]);
  const eligible = useMemo(() => preview?.workbook.records.filter((row) => row.eligibleForPosting && !duplicateIds.has(row.recordId)) ?? [],
    [preview, duplicateIds]);
  const canConfirm = useMemo(() => new Set(preview?.workbook.records.filter((row) =>
    row.currency === 'EGP' && !duplicateIds.has(row.recordId) &&
    (row.eligibleForPosting || row.operationalEvidence?.requiresManualMapping)).map((row) => row.recordId)), [preview, duplicateIds]);
  const confirmedSet = useMemo(() => new Set(confirmed), [confirmed]);
  const hasManualSelection = preview?.workbook.records.some((row) => confirmedSet.has(row.recordId) && row.operationalEvidence?.requiresManualMapping);
  const previewRows = preview?.workbook.records.filter((row) => filter === 'all' || row.sectionId === filter) ?? [];
  const stageRows = batch?.records ?? [];
  const selection = batch ? stageRows : previewRows;
  const confirmedCount = stageRows.filter((row) => ['READY_FOR_APPROVAL', 'APPROVED_FOR_POSTING', 'POSTED'].includes(row.status)).length;
  const pendingCount = stageRows.filter((row) => row.status === 'REVIEW_REQUIRED').length;

  const resetReview = () => {
    setPreview(null); setBatch(null); setConfirmed([]); setConfirmedProject(false); setReference(''); setMappingEvidence('');
    setPage(0); setFilter('all');
  };

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError('');
    try { await action(); }
    catch (failure) {
      const response = failure as { response?: { data?: { message?: string } }; message?: string };
      setError(response.response?.data?.message ?? response.message ?? 'تعذر تنفيذ الطلب.');
    } finally { setBusy(false); }
  };

  const showBatch = (result: ReviewedWorkbookBatch) => {
    setBatch(result); setPage(0);
    setBatches((ids) => [result.id, ...ids.filter((id) => id !== result.id)]);
  };

  return (
    <MainCard title="مراجعة واستيراد أرشيف Excel">
      <Stack spacing={2.5}>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'center' }}>
          <TextField select label="المشروع المستهدف" value={projectId} disabled={busy} sx={{ minWidth: 280 }}
            onChange={(event) => { resetReview(); setBatches([]); setProjectId(event.target.value); }}>
            <MenuItem value="" disabled>اختر مشروعاً</MenuItem>
            {projects.map((project) => <MenuItem key={project.id} value={String(project.id)}>{project.name} — {project.code}</MenuItem>)}
          </TextField>
          <Button component={Link} to="/emg/projects">إدارة المشروعات</Button>
          <Button component="label" variant="outlined" disabled={busy}>
            اختيار ملف Excel
            <input type="file" hidden accept=".xlsx" aria-label="ملف الأرشيف المعتمد"
              onChange={(event) => { resetReview(); setFile(event.target.files?.[0] ?? null); }} />
          </Button>
          <Button variant="contained" disabled={busy || !projectId || !file} onClick={() => void run(async () => {
            resetReview(); setPreview(await previewReviewedWorkbook(Number(projectId), file!));
          })}>{busy ? 'جارٍ التنفيذ…' : 'معاينة الملف'}</Button>
        </Stack>
        {file && <Typography>{file.name}</Typography>}
        {batches.length > 0 && (
          <TextField select label="الدفعات المحفوظة" value={batch?.id ?? ''} disabled={busy} sx={{ maxWidth: 280 }}
            onChange={(event) => void run(async () => { resetReview(); showBatch(await getReviewedWorkbookBatch(Number(event.target.value))); })}>
            <MenuItem value="" disabled>اختر دفعة</MenuItem>
            {batches.map((id) => <MenuItem key={id} value={id}>دفعة رقم {id}</MenuItem>)}
          </TextField>
        )}
        {error && <Alert severity="error">{error}</Alert>}
        {preview && !batch && (
          <>
            <Typography variant="h4">{preview.projectName} — {preview.projectCode}</Typography>
            <Typography variant="body2">
              {preview.workbook.fileName} · رقم القطعة في المصدر {preview.workbook.sourceProjectCode ?? 'غير مثبت'} ·
              {' '}{preview.workbook.records.length} سجل · {eligible.length} مطابقة مقترحة · {duplicateIds.size} تكرار محتمل
            </Typography>
            <Reconciliation key={preview.snapshotId} controls={preview.workbook.reconciliations} />
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'center' }}>
              <TextField select label="القسم" value={filter} sx={{ minWidth: 275 }} onChange={(event) => { setFilter(event.target.value); setPage(0); }}>
                <MenuItem value="all">كل الأقسام</MenuItem>
                {preview.workbook.sections.map((section) => <MenuItem key={section.sectionId} value={section.sectionId}>
                  {section.sheetName} · {section.sourceRange} · {section.requiresMapping ? 'يحتاج مراجعة' : section.mappingFamily}
                </MenuItem>)}
              </TextField>
              <Button disabled={!previewRows.some((row) => row.eligibleForPosting && canConfirm.has(row.recordId)) || busy}
                onClick={() => setConfirmed([...new Set([...confirmed, ...previewRows.filter((row) =>
                  row.eligibleForPosting && canConfirm.has(row.recordId)).map((row) => row.recordId)])])}>
                تأكيد المقترحات في القسم المعروض
              </Button>
              <Button disabled={!confirmed.length || busy} onClick={() => setConfirmed([])}>مسح التحديد</Button>
              <Typography>{confirmed.length} سجل مؤكد</Typography>
            </Stack>
          </>
        )}
        {batch && (
          <>
            <Typography variant="h4">دفعة {batch.id} · {statuses[batch.status] ?? batch.status}</Typography>
            <Typography>{batch.sourceFileName} · {batch.originalRecordCount} سجل · {confirmedCount} مؤكد · {pendingCount} قيد المراجعة</Typography>
            {batch.totals?.length > 0 && (
              <TableContainer>
                <Table size="small" aria-label="إجماليات الدفعة">
                  <TableHead><TableRow>
                    <TableCell>وجهة EMG</TableCell><TableCell>المؤكد للمراجعة</TableCell><TableCell>المرحّل فعليًا</TableCell>
                  </TableRow></TableHead>
                  <TableBody>{batch.totals.map((row) => (
                    <TableRow key={`${row.target}-${row.currency}`}>
                      <TableCell>{targetLabel(row.target)}</TableCell>
                      <TableCell>{value(row.selectedAmount)} {row.currency} · {row.selectedCount} سجل</TableCell>
                      <TableCell>{value(row.postedAmount)} {row.currency} · {row.postedCount} سجل</TableCell>
                    </TableRow>
                  ))}</TableBody>
                </Table>
              </TableContainer>
            )}
            <Reconciliation key={batch.id} controls={batch.reconciliations} />
            <Stack direction="row" flexWrap="wrap" gap={1}>
              {batch.canApprove && <Button variant="contained" disabled={busy} onClick={() => void run(async () => {
                showBatch(await advanceReviewedWorkbook(batch.id, 'approve', batch.version));
              })}>اعتماد السجلات المؤكدة</Button>}
              {batch.canPost && <Button variant="contained" disabled={busy} onClick={() => void run(async () => {
                showBatch(await advanceReviewedWorkbook(batch.id, 'post', batch.version));
              })}>ترحيل الدفعة التجريبية</Button>}
              <Button component={Link} to={`/emg/reports/${batch.projectId}`}>تقرير المشروع وملف Excel</Button>
            </Stack>
            {batch.canRevise && <TextField label="دليل تعديل المطابقة اليدوية" value={mappingEvidence} disabled={busy}
              onChange={(event) => setMappingEvidence(event.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />}
            {!batch.canPost && batch.status === 'APPROVED_FOR_POSTING' && <Alert severity="info">الترحيل المالي التاريخي مغلق.</Alert>}
          </>
        )}
        {(preview || batch) && (
          <Box>
            <TableContainer>
              <Table size="small" aria-label="سجلات الأرشيف">
                <TableHead><TableRow>
                  {!batch && <TableCell>تأكيد</TableCell>}
                  <TableCell>المصدر</TableCell><TableCell>نوع الحركة</TableCell><TableCell>القيمة</TableCell>
                  <TableCell>وجهة EMG</TableCell><TableCell>حالة المطابقة</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {batch ? stageRows.slice(page * 25, page * 25 + 25).map((row) => (
                    <TableRow key={row.id}>
                      <TableCell>{row.sourceCoordinate}</TableCell><TableCell>{types[recordTypeNames.indexOf(row.recordType)] ?? row.recordType}</TableCell>
                      <TableCell>{value(row.amount)} {row.currency}</TableCell><TableCell>{targetLabel(row.target)} {row.targetId && `#${row.targetId}`}</TableCell>
                      <TableCell>{statuses[row.status] ?? row.status}{row.reason && <Typography variant="caption" display="block">{row.reason}</Typography>}
                        {batch.canRevise && row.canSelect && (
                          <Button size="small" disabled={busy || (row.needsMappingEvidence && row.status !== 'READY_FOR_APPROVAL' && mappingEvidence.trim().length < 8)}
                            onClick={() => void run(async () => {
                              setBatch(await reviseReviewedWorkbook(batch.id, batch.version, row.id, row.status !== 'READY_FOR_APPROVAL', mappingEvidence));
                              setMappingEvidence('');
                            })}>{row.status === 'READY_FOR_APPROVAL' ? 'أعد للمراجعة' : 'أكد المطابقة'}</Button>
                        )}
                      </TableCell>
                    </TableRow>
                  )) : previewRows.slice(page * 25, page * 25 + 25).map((row) => (
                    <TableRow key={row.recordId}>
                      <TableCell><Checkbox checked={confirmedSet.has(row.recordId)} disabled={busy || !canConfirm.has(row.recordId)}
                        slotProps={{ input: { 'aria-label': `تأكيد ${row.sourceCoordinate}` } }}
                        onChange={(_, checked) => setConfirmed(checked ? [...confirmed, row.recordId] : confirmed.filter((id) => id !== row.recordId))} /></TableCell>
                      <TableCell>{row.sourceCoordinate}<Typography variant="caption" display="block">
                        {sectionIndex.get(row.sectionId)?.mappingFamily} · {row.sourceCells.join('، ')}
                      </Typography></TableCell>
                      <TableCell>{types[row.recordType]}<Typography variant="caption" display="block">{row.operationalEvidence?.description}</Typography></TableCell>
                      <TableCell>{value(row.amount)} {row.currency}</TableCell><TableCell>{targetLabel(row.operationalEvidence?.target)}</TableCell>
                      <TableCell>{duplicateIds.has(row.recordId) ? 'تكرار محتمل' : row.isSummary ? 'إجمالي مرجعي' :
                        row.currency !== 'EGP' ? 'عملة المصدر محفوظة للمراجعة' : row.operationalEvidence?.requiresManualMapping ?
                          'تأكيد يدوي لوجهة مصروف الموقع' : canConfirm.has(row.recordId) ? 'مطابقة مقترحة' : 'يحتاج دليلاً أو ربطاً'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
            <TablePagination component="div" count={selection.length} page={page} rowsPerPage={25} rowsPerPageOptions={[]}
              onPageChange={(_, next) => setPage(next)} labelDisplayedRows={({ from, to, count }) => `${from}–${to} من ${count}`} />
          </Box>
        )}
        {preview && !batch && (
          <Stack spacing={1.5} sx={{ maxWidth: 580 }}>
            <TextField label="مرجع مراجعة المصدر" value={reference} disabled={busy} onChange={(event) => setReference(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 500 } }} />
            {hasManualSelection && <TextField label="دليل المطابقة اليدوية" value={mappingEvidence} disabled={busy}
              onChange={(event) => setMappingEvidence(event.target.value)} slotProps={{ htmlInput: { minLength: 8, maxLength: 500 } }} />}
            <FormControlLabel control={<Checkbox checked={confirmedProject} disabled={busy}
              onChange={(_, checked) => setConfirmedProject(checked)} />} label="أؤكد أن المصدر يخص المشروع المحدد." />
            <Button variant="contained" disabled={busy || !reference.trim() || !confirmedProject ||
              (hasManualSelection && mappingEvidence.trim().length < 8)} sx={{ alignSelf: 'flex-start' }}
              onClick={() => void run(async () => showBatch(await stageReviewedWorkbook(preview.snapshotId, reference, confirmedProject, confirmed, mappingEvidence)))}>
              حفظ الدفعة للمراجعة
            </Button>
            <Typography variant="body2">تُحفظ السجلات غير المؤكدة للمراجعة مع خلاياها وقيمها الأصلية.</Typography>
          </Stack>
        )}
      </Stack>
    </MainCard>
  );
}

const recordTypeNames = ['Transaction', 'Funding', 'Payment', 'Expense', 'Liability', 'Allocation', 'OpeningBalance', 'Adjustment', 'DerivedValue', 'Summary', 'Unresolved'];
