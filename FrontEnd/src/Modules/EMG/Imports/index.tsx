import { ChangeEvent, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Grid,
  Stack,
  Step,
  StepLabel,
  Stepper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from '@mui/material';
import { IconAlertTriangle, IconCheck, IconFileUpload, IconRefresh, IconShieldCheck } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { Link, useLocation } from 'react-router-dom';
import axios from 'utils/axios';

type UniversalPreviewIssue = {
  code: string;
  sheetName: string;
  sourceCoordinate: string;
  recordId?: string | null;
  message: string;
};

type UniversalPreviewResponse = {
  snapshotId: string;
  fileName?: string;
  sourceProjectCode?: string | null;
  projectMetadata?: {
    clientName?: string | null;
    ownerName?: string | null;
    payerName?: string | null;
    location?: string | null;
  } | null;
  sections?: Array<{ sectionId: string; sheetName: string; sourceRange: string; mappingFamily: string; requiresMapping: boolean }>;
  records?: Array<{
    recordId: string;
    sheetName: string;
    sourceCoordinate: string;
    amount: number | null;
    currency: string;
    isSummary: boolean;
    eligibleForPosting: boolean;
    operationalEvidence?: { requiresManualMapping: boolean } | null;
  }>;
  reconciliations?: Array<{
    sectionId: string;
    streamKey: string;
    currency: string;
    previewAmount: number;
    sourceSummaryAmount: number | null;
    difference: number | null;
    status: string;
  }>;
  duplicateCandidates?: Array<{ recordIds: string[] }>;
  issues?: UniversalPreviewIssue[];
};

type ImportValidation = {
  postingEnabled: boolean;
  requiresExclusionConfirmation: boolean;
  records: Array<{ recordId: string; sourceCoordinate: string; target: string | null; amount: number | null; currency: string; canImport: boolean; reason: string | null }>;
};

const STEPS = ['رفع ملف Excel', 'التحليل ومراجعة البيانات', 'معالجة الملاحظات وإعادة التحقق', 'تأكيد الاستيراد'];

export default function EmgImports() {
  const { user } = useAuth();
  const location = useLocation();
  const isUpdateMode = location.pathname.endsWith('/update');
  const inputRef = useRef<HTMLInputElement>(null);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [universalPreview, setUniversalPreview] = useState<UniversalPreviewResponse | null>(null);
  const [validation, setValidation] = useState<{ result: ImportValidation; input: string } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [revalidatedAt, setRevalidatedAt] = useState<string | null>(null);

  // Editable project metadata (retained even if upload returns HTTP 400 per Codex contract)
  const [meta, setMeta] = useState({
    name: '',
    code: '',
    clientName: '',
    ownerName: '',
    payerName: '',
    location: '',
    engineerName: ''
  });

  // Inline issue correction notes keyed by sheet+coordinate
  const [issueCorrections, setIssueCorrections] = useState<Record<string, string>>({});

  const [committedProject, setCommittedProject] = useState<{
    projectId: number;
    name: string;
    code: string;
    counts: string;
  } | null>(null);

  if (user?.permissions && !user.permissions.includes('data.migrate')) {
    return <Alert severity="error">ليس لديك صلاحية استيراد البيانات.</Alert>;
  }

  if (isUpdateMode) {
    return <Alert severity="info" action={<Button component={Link} to="/emg/imports/approved-history">فتح المراجعة</Button>}>
      استخدم مراجعة الاستيراد التاريخي للمشروعات القائمة.
    </Alert>;
  }

  const request = { ...meta, snapshotId: universalPreview?.snapshotId, mappingEvidence: Object.fromEntries(
    Object.entries(issueCorrections).filter(([, evidence]) => evidence.trim())
  ) };
  const requestKey = JSON.stringify(request);
  const reviewed = validation?.input === requestKey ? validation.result : null;

  const analyzeWorkbook = async (file: File, isRevalidate = false) => {
    setLoading(true);
    setError(null);
    if (!isRevalidate) {
      setCommittedProject(null);
      setUniversalPreview(null);
      setIssueCorrections({});
      setRevalidatedAt(null);
    }
    setValidation(null);
    try {
      if (isRevalidate) {
        const result = await axios.post<ImportValidation>('/api/universal-import/revalidate', request);
        setValidation({ result: result.data, input: requestKey });
        setRevalidatedAt(new Date().toLocaleTimeString('ar-EG'));
        return;
      }
      const formData = new FormData();
      formData.append('file', file);
      const { data: uData } = await axios.post<UniversalPreviewResponse>('/api/universal-import/preview', formData);
      setUniversalPreview(uData);
      setMeta({
        name: file.name.replace(/\.xlsx$/i, ''),
        code: uData.sourceProjectCode ?? '',
        clientName: uData.projectMetadata?.clientName ?? '',
        ownerName: uData.projectMetadata?.ownerName ?? '',
        payerName: uData.projectMetadata?.payerName ?? '',
        location: uData.projectMetadata?.location ?? '',
        engineerName: ''
      });
    } catch (caughtError: any) {
      setError(caughtError?.response?.data?.message || caughtError?.response?.data?.title || 'تعذر التحقق من ملف Excel.');
    } finally {
      setLoading(false);
    }
  };

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setSelectedFile(file);
    await analyzeWorkbook(file, false);
  };

  const handleCommitNewProject = async () => {
    if (!reviewed?.postingEnabled) return;
    if (!meta.name.trim()) {
      setError('يرجى إدخال اسم المشروع قبل تأكيد الاستيراد.');
      return;
    }
    if (!window.confirm(reviewed.requiresExclusionConfirmation
      ? 'سيتم استيراد الحركات المؤهلة وحفظ السجلات المستبعدة للمراجعة. هل تريد المتابعة؟'
      : 'سيتم إنشاء المشروع واستيراد الحركات المؤهلة. هل تريد المتابعة؟')) return;
    setLoading(true);
    setError(null);
    try {
      const { data: result } = await axios.post<{
        projectId: number; name: string; code: string; counts: Record<string, number>; retainedRecords: number;
      }>('/api/universal-import/commit', { ...request, confirmExcluded: true });
      setCommittedProject({
        projectId: result.projectId,
        name: result.name,
        code: result.code,
        counts: `تم استيراد ${Object.values(result.counts).reduce((sum, count) => sum + count, 0)} سجل، وحفظ ${result.retainedRecords} للمراجعة والمطابقة.`
      });
    } catch (caughtError: any) {
      const serverMessage = caughtError?.response?.data?.message || caughtError?.response?.data?.title;
      setError(serverMessage || caughtError?.message || 'تعذر اعتماد إنشاء المشروع. لم يتم تنفيذ الاستيراد.');
    } finally {
      setLoading(false);
    }
  };

  const issues = universalPreview?.issues || [];
  const sections = universalPreview?.sections || [];
  const records = universalPreview?.records || [];
  const correctableRecordIds = new Set(records.filter((record) => record.operationalEvidence?.requiresManualMapping)
    .map((record) => record.recordId));
  const validRecordsCount = reviewed ? reviewed.records.filter((r) => r.canImport).length :
    records.filter((r) => !r.isSummary && r.eligibleForPosting).length;
  const activeStep = committedProject ? 3 : universalPreview ? (issues.length > 0 ? 2 : 1) : 0;

  return (
    <Box sx={{ pb: 4, direction: 'rtl' }}>
      <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', md: 'center' }} spacing={2} sx={{ mb: 2.5 }}>
        <Box>
          <Typography variant="h2" sx={{ fontWeight: 850 }}>
            استيراد مشروع جديد من Excel
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.35 }}>
            تحليل تلقائي للشيتات والصفوف، توضيح الأخطاء بالخلية والسبب، ثم إنشاء المشروع والاستيراد مباشرة
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          {selectedFile && universalPreview && !committedProject && (
            <Button variant="outlined" startIcon={<IconRefresh size={17} />} onClick={() => void analyzeWorkbook(selectedFile, true)} disabled={loading} sx={{ borderRadius: 2, fontWeight: 700 }}>
              إعادة التحقق
            </Button>
          )}
          <Button variant="contained" startIcon={<IconFileUpload size={18} />} onClick={() => inputRef.current?.click()} disabled={loading} sx={{ borderRadius: 2, fontWeight: 700 }}>
            {loading ? 'جاري التحليل...' : selectedFile ? 'اختيار ملف آخر' : 'رفع ملف Excel (.xlsx)'}
          </Button>
          <input ref={inputRef} hidden type="file" accept=".xlsx" onChange={handleFile} />
        </Stack>
      </Stack>

      <MainCard sx={{ mb: 2.5, borderRadius: 3 }}>
        <Stepper activeStep={activeStep} alternativeLabel>
          {STEPS.map((label) => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>
      </MainCard>

      {error && (
        <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2, borderRadius: 2 }}>
          {error}
        </Alert>
      )}
      {revalidatedAt && reviewed && (
        <Alert severity="info" sx={{ mb: 2, borderRadius: 2 }}>
          تمت إعادة التحقق من الملف بنجاح الساعة {revalidatedAt}.
        </Alert>
      )}

      {!universalPreview && !loading && (
        <MainCard sx={{ borderRadius: 3 }}>
          <Stack alignItems="center" spacing={1.5} sx={{ py: 6, textAlign: 'center' }}>
            <IconShieldCheck size={44} color="#1565c0" />
            <Typography variant="h3" sx={{ fontWeight: 800 }}>
              ارفع ملف المشروع الأصلي للتحليل التلقائي
            </Typography>
            <Typography color="text.secondary" sx={{ maxWidth: 480 }}>
              لا حاجة لإنشاء المشروع يدوياً مسبقاً. سيتم قراءة الشيتات، كشف الصفوف الصالحة، وتحديد أي ملاحظات بالصف والعمود قبل الحفظ.
            </Typography>
            <Button variant="contained" size="large" startIcon={<IconFileUpload size={19} />} onClick={() => inputRef.current?.click()} sx={{ mt: 1, borderRadius: 2.5, fontWeight: 700 }}>
              اختيار ملف Excel
            </Button>
          </Stack>
        </MainCard>
      )}

      {loading && (
        <MainCard sx={{ borderRadius: 3 }}>
          <Stack alignItems="center" spacing={1.5} sx={{ py: 6 }}>
            <IconRefresh size={34} />
            <Typography fontWeight={700}>جاري تحليل بنية الملف والصفوف والمطابقات المالية...</Typography>
          </Stack>
        </MainCard>
      )}

      {universalPreview && !loading && (
        <Stack spacing={2.5}>
          {/* Summary KPIs */}
          <Grid container spacing={2}>
            <Grid size={{ xs: 6, md: 3 }}>
              <Card variant="outlined" sx={{ borderRadius: 3 }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight={700}>
                    الشيتات المكتشفة
                  </Typography>
                  <Typography variant="h2" fontWeight={900}>
                    {sections.length}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Card variant="outlined" sx={{ borderRadius: 3 }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight={700}>
                    صفوف صالحة للاستيراد
                  </Typography>
                  <Typography variant="h2" color="success.main" fontWeight={900}>
                    {validRecordsCount}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Card variant="outlined" sx={{ borderRadius: 3 }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight={700}>
                    ملاحظات وأخطاء مكتشفة
                  </Typography>
                  <Typography variant="h2" color={issues.length > 0 ? 'warning.dark' : 'success.main'} fontWeight={900}>
                    {issues.length}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <Card variant="outlined" sx={{ borderRadius: 3 }}>
                <CardContent>
                  <Typography variant="caption" color="text.secondary" fontWeight={700}>
                    تكرارات محتملة
                  </Typography>
                  <Typography variant="h2" fontWeight={900}>
                    {universalPreview.duplicateCandidates?.length || 0}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Editable Project Metadata */}
          {!isUpdateMode && (
            <MainCard title="بيانات المشروع المستخرجة (قابلة للتعديل قبل الإنشاء)" sx={{ borderRadius: 3 }}>
              <Grid container spacing={1.75}>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" required label="اسم المشروع" value={meta.name} onChange={(e) => setMeta({ ...meta, name: e.target.value })} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" label="كود المشروع / رقم القطعة" value={meta.code} onChange={(e) => setMeta({ ...meta, code: e.target.value })} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" label="اسم العميل" value={meta.clientName} onChange={(e) => setMeta({ ...meta, clientName: e.target.value })} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" label="اسم المالك" value={meta.ownerName} onChange={(e) => setMeta({ ...meta, ownerName: e.target.value })} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" label="الدافع" value={meta.payerName} onChange={(e) => setMeta({ ...meta, payerName: e.target.value })} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 4 }}>
                  <TextField fullWidth size="small" label="الموقع" value={meta.location} onChange={(e) => setMeta({ ...meta, location: e.target.value })} />
                </Grid>
              </Grid>
            </MainCard>
          )}

          {/* Identified Issues Table (Sheet, Coordinate, Code, Reason + Inline Correction Note) */}
          <MainCard
            title={
              <Stack direction="row" spacing={1} alignItems="center">
                {issues.length > 0 ? <IconAlertTriangle size={20} color="#d97706" /> : <IconCheck size={20} color="#16a34a" />}
                <Typography variant="h4" fontWeight={800}>
                  الملاحظات والأخطاء المكتشفة ({issues.length})
                </Typography>
              </Stack>
            }
            sx={{ borderRadius: 3 }}
          >
            {issues.length === 0 ? (
              <Alert severity="success" sx={{ borderRadius: 2 }}>
                لا توجد أخطاء بنيوية أو صفوف غير مصنفة في الملف.
              </Alert>
            ) : (
              <TableContainer sx={{ overflowX: 'auto' }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ fontWeight: 800 }}>الشيت</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>الصف / الخلية</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>الرمز</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>سبب الخطأ</TableCell>
                      <TableCell sx={{ fontWeight: 800 }}>تصحيح / ملاحظة الربط</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {issues.map((issue, idx) => {
                      const key = `${issue.sheetName}:${issue.sourceCoordinate}:${idx}`;
                      return (
                        <TableRow key={key} hover>
                          <TableCell sx={{ fontWeight: 700 }}>{issue.sheetName}</TableCell>
                          <TableCell sx={{ direction: 'ltr', textAlign: 'end', fontWeight: 700 }}>{issue.sourceCoordinate}</TableCell>
                          <TableCell>
                            <Chip size="small" color="warning" variant="outlined" label={issue.code} />
                          </TableCell>
                          <TableCell>{issue.message}</TableCell>
                          <TableCell sx={{ minWidth: 200 }}>
                            {issue.recordId && correctableRecordIds.has(issue.recordId) ? <TextField
                              fullWidth
                              size="small"
                              placeholder="أدخل دليل المطابقة من المصدر"
                              value={issueCorrections[issue.recordId] || ''}
                              onChange={(e) => setIssueCorrections((prev) => ({ ...prev, [issue.recordId!]: e.target.value }))}
                            /> : 'محفوظ للمراجعة'}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </MainCard>

          {/* Detected Sheets / Sections */}
          <MainCard title="الشيتات المكتشفة" sx={{ borderRadius: 3 }}>
            <TableContainer sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 800 }}>الشيت</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>النطاق</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>التصنيف المكتشف</TableCell>
                    <TableCell sx={{ fontWeight: 800 }}>الحالة</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {sections.map((sec) => (
                    <TableRow key={sec.sectionId} hover>
                      <TableCell sx={{ fontWeight: 700 }}>{sec.sheetName}</TableCell>
                      <TableCell sx={{ direction: 'ltr', textAlign: 'end' }}>{sec.sourceRange}</TableCell>
                      <TableCell>{sec.mappingFamily}</TableCell>
                      <TableCell>
                        <Chip size="small" label={sec.requiresMapping ? 'يحتاج مطابقة' : 'جاهز'} color={sec.requiresMapping ? 'warning' : 'success'} />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </MainCard>

          <Divider />
          {reviewed && (
            <MainCard title="نتيجة التحقق على الخادم">
              {!reviewed.postingEnabled && <Alert severity="info">الترحيل المالي غير مفعّل لهذه البيئة أو لهذا الحساب.</Alert>}
              <TableContainer sx={{ maxHeight: 420 }}>
                <Table size="small" stickyHeader>
                  <TableHead><TableRow><TableCell>المصدر</TableCell><TableCell>القيمة</TableCell><TableCell>النتيجة</TableCell></TableRow></TableHead>
                  <TableBody>{reviewed.records.map((row) => (
                    <TableRow key={row.recordId}>
                      <TableCell>{row.sourceCoordinate}</TableCell>
                      <TableCell>{row.amount ?? '—'} {row.currency}</TableCell>
                      <TableCell>{row.canImport ? row.target : row.reason}</TableCell>
                    </TableRow>
                  ))}</TableBody>
                </Table>
              </TableContainer>
            </MainCard>
          )}

          {/* Confirm Action */}
          {!committedProject ? (
            <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} spacing={1.5}>
              <Typography variant="body2" color="text.secondary">
                راجع بيانات المشروع ونتيجة التحقق قبل التأكيد.
              </Typography>
              <Stack direction="row" spacing={1.25}>
                <Button variant="outlined" onClick={() => selectedFile && void analyzeWorkbook(selectedFile, true)} disabled={loading}>
                  إعادة التحقق
                </Button>
                  <Button variant="contained" color="primary" disabled={loading || !reviewed?.postingEnabled || validRecordsCount === 0} onClick={() => void handleCommitNewProject()} sx={{ fontWeight: 700 }}>
                    تأكيد إنشاء المشروع والاستيراد
                  </Button>
              </Stack>
            </Stack>
          ) : (
            <Alert
              severity="success"
              sx={{ borderRadius: 2.5 }}
              action={
                <Button component={Link} to={`/emg/projects/${committedProject.projectId}`} variant="contained" size="small" color="success" sx={{ fontWeight: 700 }}>
                  فتح المشروع
                </Button>
              }
            >
              تم إنشاء المشروع «{committedProject.name}» ({committedProject.code}). {committedProject.counts}
            </Alert>
          )}
        </Stack>
      )}
    </Box>
  );
}
