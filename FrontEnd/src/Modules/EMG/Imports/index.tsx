import { ChangeEvent, useEffect, useRef, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Chip, Divider, FormControl, Grid, InputLabel, MenuItem, Select, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { IconAlertTriangle, IconCheck, IconFileUpload, IconRefresh, IconShieldCheck } from '@tabler/icons-react';
import MainCard from 'ui-component/cards/MainCard';
import useAuth from 'hooks/useAuth';
import { Link, useLocation } from 'react-router-dom';
import { buildEmgExistingProjectPlan, buildEmgNewProjectPlan, commitEmgExistingProject, commitEmgNewProject, EmgProject, EmgWorkbookMigrationPlan, EmgWorkbookPreview, getEmgProjects, previewEmgWorkbook } from 'api/emg';

const formatBytes = (value: number) => `${(value / 1024).toFixed(1)} ك.ب`;

export default function EmgImports() {
  const { user } = useAuth();
  const location = useLocation();
  const isUpdateMode = location.pathname.endsWith('/update');
  const isNewMode = location.pathname.endsWith('/new');
  const inputRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<EmgWorkbookPreview | null>(null);
  const [migrationPlan, setMigrationPlan] = useState<EmgWorkbookMigrationPlan | null>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [committedProject, setCommittedProject] = useState<{ projectId: number; name: string; code: string; attachmentId: number; counts: string } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [projects, setProjects] = useState<EmgProject[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<number | ''>('');
  const hasUpdatePlan = migrationPlan?.mode === 'update-existing-project';

  useEffect(() => { getEmgProjects().then(setProjects).catch(() => setProjects([])); }, []);

  if (user?.permissions && !user.permissions.includes('data.migrate')) {
    return <Alert severity="error">ليس لديك صلاحية ترحيل البيانات.</Alert>;
  }

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setLoading(true); setError(null); setPreview(null);
    setSelectedFile(file);
    setMigrationPlan(null);
    try { setPreview(await previewEmgWorkbook(file)); } catch { setError('تعذر قراءة ملف Excel. تأكد من أنه XLSX صالح.'); }
    finally { setLoading(false); }
  };

  const handleBuildPlan = async () => {
    if (!selectedFile) return;
    setLoading(true); setError(null);
    try { setMigrationPlan(await buildEmgNewProjectPlan(selectedFile)); } catch { setError('تعذر إنشاء خطة الترحيل.'); }
    finally { setLoading(false); }
  };

  const handleBuildExistingPlan = async () => {
    if (!selectedFile || selectedProjectId === '') return;
    setLoading(true); setError(null);
    try { setMigrationPlan(await buildEmgExistingProjectPlan(selectedFile, Number(selectedProjectId))); } catch { setError('تعذر إنشاء خطة تحديث المشروع.'); }
    finally { setLoading(false); }
  };

  const handleCommitNewProject = async () => {
    if (!selectedFile || !migrationPlan) return;
    if (migrationPlan.mode !== 'create-new-project') return;
    if (!window.confirm('سيتم إنشاء مشروع جديد وحفظ ملف Excel كمرفق. هل تريد المتابعة؟')) return;
    setLoading(true); setError(null);
    try {
      const result = await commitEmgNewProject(selectedFile, { name: migrationPlan.suggestedProjectName, code: migrationPlan.suggestedProjectCode, clientName: migrationPlan.suggestedClientName, location: migrationPlan.suggestedLocation });
      setCommittedProject({ projectId: result.projectId, name: result.name, code: result.code, attachmentId: result.attachmentId, counts: `حركة موقع ${result.siteMovementsImported}، حفر ${result.excavationRecordsImported}، خرسانات ${result.concreteRecordsImported}، حديد ${result.steelRecordsImported}، تشطيبات ${result.finishingRecordsImported}، توريدات ${result.supplierSuppliesImported}، مدفوعات موردين ${result.supplierPaymentsImported}، دفعات تحت الحساب ${result.advancePaymentsImported}، توزيعات ${result.advanceAllocationsImported}` });
    } catch (caughtError: any) {
      const serverMessage = caughtError?.response?.data?.message || caughtError?.response?.data?.title;
      setError(serverMessage || caughtError?.message || 'تعذر اعتماد إنشاء المشروع. لم يتم تنفيذ الترحيل.');
    }
    finally { setLoading(false); }
  };

  const handleCommitExistingProject = async () => {
    if (!selectedFile || !migrationPlan || selectedProjectId === '') return;
    if (!window.confirm('سيتم استيراد الحركات كمسودات إلى مشروع خالٍ من الحركات المالية. هل تريد المتابعة؟')) return;
    setLoading(true); setError(null);
    try {
      const result = await commitEmgExistingProject(selectedFile, Number(selectedProjectId));
      setCommittedProject({ projectId: result.projectId, name: result.name, code: result.code, attachmentId: result.attachmentId, counts: `حركة موقع ${result.siteMovementsImported}، حفر ${result.excavationRecordsImported}، خرسانات ${result.concreteRecordsImported}، حديد ${result.steelRecordsImported}، تشطيبات ${result.finishingRecordsImported}، توريدات ${result.supplierSuppliesImported}، مدفوعات موردين ${result.supplierPaymentsImported}، دفعات تحت الحساب ${result.advancePaymentsImported}، توزيعات ${result.advanceAllocationsImported}` });
    } catch (caughtError: any) { setError(caughtError?.response?.data?.message || 'تعذر تحديث المشروع. لم يتم اعتماد أي جزء من العملية.'); }
    finally { setLoading(false); }
  };

  return <Box sx={{ pb: 4, direction: 'rtl' }}>
    <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', md: 'center' }} spacing={2} sx={{ mb: 3 }}>
      <Box><Typography variant="h2" sx={{ fontWeight: 800 }}>معاينة استيراد البيانات</Typography><Typography color="text.secondary" sx={{ mt: 0.5 }}>تحليل ملف Excel قبل حفظ أي بيانات داخل النظام</Typography></Box>
      <Button variant="contained" startIcon={<IconFileUpload size={18} />} onClick={() => inputRef.current?.click()} disabled={loading} sx={{ gap: 0.75 }}>{loading ? 'جاري التحليل...' : 'اختيار ملف Excel'}</Button>
      <input ref={inputRef} hidden type="file" accept=".xlsx" onChange={handleFile} />
    </Stack>
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    {!preview && !loading && <MainCard><Stack alignItems="center" spacing={1.5} sx={{ py: 6 }}><IconShieldCheck size={42} color="#673ab7" /><Typography variant="h3">المعاينة آمنة</Typography><Typography color="text.secondary">لن يتم إنشاء أو تعديل أي مشروع قبل مراجعة النتيجة واعتمادها.</Typography></Stack></MainCard>}
    {loading && <MainCard><Stack alignItems="center" spacing={1.5} sx={{ py: 6 }}><IconRefresh size={34} className="emg-spin" /><Typography>جاري فحص بنية الملف...</Typography></Stack></MainCard>}
    {preview && <Stack spacing={2}>
      {!isUpdateMode && <MainCard title="بيانات المشروع الجديد">
        <Stack spacing={1.25}>
          <Alert severity="info">سيتم إنشاء مشروع جديد تلقائيًا من اسم الملف والبيانات التعريفية، مع حفظ ملف Excel الأصلي كمرفق. الصفحات غير المعنونة ستُحفظ داخل المصدر ولن تُرحّل كحركات بدون مطابقة.</Alert>
          {selectedFile && <Button variant="outlined" onClick={handleBuildPlan} disabled={loading || !preview?.isValid} sx={{ alignSelf: 'flex-start' }}>{loading ? 'جاري إعداد الخطة...' : 'إنشاء خطة مشروع جديد'}</Button>}
        </Stack>
      </MainCard>}
      {!isNewMode && <MainCard title="إعادة استيراد وتحديث مشروع موجود" sx={{ border: '1px solid', borderColor: 'warning.light', boxShadow: '0 8px 24px rgba(245, 158, 11, 0.10)' }}>
        <Stack spacing={2}>
          <Box sx={{ p: 2, borderRadius: 2, bgcolor: 'warning.lighter', border: '1px solid', borderColor: 'warning.light' }}>
            <Typography variant="h4" sx={{ mb: 0.5 }}>تحديث مشروع محفوظ</Typography>
            <Typography color="text.secondary">اختر مشروعاً خالياً من الحركات المالية. سيُحفظ ملف Excel وتُضاف الحركات كمسودات.</Typography>
          </Box>
          <FormControl fullWidth size="small">
            <InputLabel id="existing-project-label">المشروع المستهدف</InputLabel>
            <Select labelId="existing-project-label" label="المشروع المستهدف" value={selectedProjectId} onChange={(event) => setSelectedProjectId(event.target.value as number)}>
              <MenuItem value=""><em>اختر المشروع الذي سيتم تحديثه</em></MenuItem>
              {projects.map((project) => <MenuItem key={project.id} value={project.id}>{project.name} — {project.code}</MenuItem>)}
            </Select>
          </FormControl>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.25} alignItems={{ xs: 'stretch', sm: 'center' }}>
            <Button variant="outlined" onClick={handleBuildExistingPlan} disabled={loading || !selectedFile || selectedProjectId === ''}>معاينة خطة التحديث</Button>
            {hasUpdatePlan && <Button variant="contained" color="warning" onClick={handleCommitExistingProject} disabled={loading}>اعتماد تحديث المشروع</Button>}
            {hasUpdatePlan && <Chip color="warning" variant="outlined" label="خطة تحديث جاهزة للمراجعة" />}
          </Stack>
        </Stack>
      </MainCard>}
      <Alert severity={preview.requiresReview ? 'warning' : 'success'} icon={preview.requiresReview ? <IconAlertTriangle /> : <IconCheck />}>
        {preview.requiresReview ? 'الملف صالح للقراءة لكنه يحتاج مراجعة قبل الترحيل.' : 'الملف صالح للقراءة ولا توجد تحذيرات بنيوية.'}
      </Alert>
      <Grid container spacing={2}><Grid size={{ xs: 12, md: 4 }}><Card><CardContent><Typography color="text.secondary">الملف</Typography><Typography variant="h4" sx={{ mt: 0.5 }}>{preview.fileName}</Typography><Typography variant="body2" color="text.secondary">{formatBytes(preview.sizeBytes)}</Typography></CardContent></Card></Grid><Grid size={{ xs: 12, md: 4 }}><Card><CardContent><Typography color="text.secondary">عدد الصفحات</Typography><Typography variant="h2" sx={{ mt: 0.5 }}>{preview.sheets.length}</Typography></CardContent></Card></Grid><Grid size={{ xs: 12, md: 4 }}><Card><CardContent><Typography color="text.secondary">الحالة</Typography><Chip sx={{ mt: 1 }} label={preview.isValid ? 'قابل للمراجعة' : 'غير صالح'} color={preview.isValid ? 'success' : 'error'} /></CardContent></Card></Grid></Grid>
      {preview.warnings.length > 0 && <MainCard title="تحذيرات المراجعة"><Stack spacing={1}>{preview.warnings.map((warning) => <Alert key={warning} severity="warning">{warning}</Alert>)}</Stack></MainCard>}
      <MainCard title="تصنيف صفحات الملف"><Table size="small"><TableHead><TableRow><TableCell>الترتيب</TableCell><TableCell>اسم الصفحة الأصلي</TableCell><TableCell>القسم المعتمد</TableCell><TableCell>الحالة</TableCell></TableRow></TableHead><TableBody>{preview.sheets.map((sheet) => <TableRow key={`${sheet.order}-${sheet.originalName}`}><TableCell>{sheet.order}</TableCell><TableCell sx={{ fontWeight: 700 }}>{sheet.originalName}</TableCell><TableCell>{sheet.classification}</TableCell><TableCell><Chip size="small" label={sheet.recognized ? 'معروفة' : 'تحتاج مراجعة'} color={sheet.recognized ? 'success' : 'warning'} /></TableCell></TableRow>)}</TableBody></Table></MainCard>
      {migrationPlan && <MainCard title={hasUpdatePlan ? 'خطة تحديث المشروع' : 'خطة إنشاء المشروع الجديد'} sx={{ boxShadow: '0 10px 28px rgba(31, 41, 55, 0.10)' }}><Stack spacing={1.5}>
        <Grid container spacing={1.25}>
          {[
            ['اسم المشروع', migrationPlan.suggestedProjectName],
            ['الكود الداخلي / رقم القطعة', migrationPlan.suggestedProjectCode],
            ['اسم العميل', migrationPlan.suggestedClientName],
            ['اسم المالك', migrationPlan.suggestedOwnerName],
            ['الدافع', migrationPlan.suggestedPayerName],
            ['الموقع', migrationPlan.suggestedLocation]
          ].map(([label, value]) => <Grid size={{ xs: 12, sm: 6, md: 4 }} key={label}><Card variant="outlined" sx={{ height: '100%' }}><CardContent sx={{ py: 1.25, '&:last-child': { pb: 1.25 } }}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography sx={{ mt: 0.35, fontWeight: 700, color: value ? 'text.primary' : 'warning.main' }}>{value || 'غير مستخرج من الملف'}</Typography></CardContent></Card></Grid>)}
        </Grid>
        <Alert severity={hasUpdatePlan ? 'info' : 'warning'}>{hasUpdatePlan ? 'تُضاف الحركات كمسودات إلى المشروع المحدد إذا كان خالياً من الحركات المالية.' : 'سيتم إنشاء المشروع بحالة «تحت المراجعة» وحفظ الملف الأصلي كمرفق. الصفحات المعروفة فقط هي التي تُرحّل.'}</Alert><Table size="small"><TableHead><TableRow><TableCell>الصفحة</TableCell><TableCell>النمط المكتشف</TableCell><TableCell>صفوف مستخدمة</TableCell><TableCell>صفوف مرشحة</TableCell><TableCell>الثقة</TableCell><TableCell>الملاحظات</TableCell></TableRow></TableHead><TableBody>{migrationPlan.sheets.map((sheet) => <TableRow key={`plan-${sheet.order}`}><TableCell sx={{ fontWeight: 700 }}>{sheet.originalName}</TableCell><TableCell>{sheet.detectionProfile}</TableCell><TableCell>{sheet.sourceRows}</TableCell><TableCell>{sheet.candidateRows}</TableCell><TableCell><Chip size="small" label={!sheet.recognized ? 'محفوظة كمصدر' : sheet.confidence} color={!sheet.recognized ? 'default' : sheet.confidence.startsWith('عالية') ? 'success' : 'warning'} /></TableCell><TableCell>{sheet.diagnostics?.join('، ') || (!sheet.recognized ? 'صفحة غير معنونة؛ لن تُرحّل كحركات' : 'مطابقة مبدئية')}</TableCell></TableRow>)}</TableBody></Table>{!hasUpdatePlan && <Button variant="contained" color="primary" onClick={handleCommitNewProject} disabled={loading || migrationPlan.sheets.some((sheet) => sheet.recognized && sheet.willCreateData && sheet.confidence === 'تحتاج مراجعة')}>اعتماد إنشاء المشروع وحفظ المصدر</Button>}</Stack></MainCard>}
      {committedProject && <Stack spacing={1}><Alert severity="success">تم إنشاء المشروع «{committedProject.name}» بالكود {committedProject.code} وحفظ ملف المصدر كمرفق رقم {committedProject.attachmentId}. تم ترحيل: {committedProject.counts}. المشروع تحت المراجعة لحين الاعتماد.</Alert><Button component={Link} to={`/emg/projects/${committedProject.projectId}`} variant="contained" sx={{ alignSelf: 'flex-start' }}>فتح المشروع ومراجعة الترحيل</Button></Stack>}
      <Divider />
      <Alert severity="info">هذه شاشة المراجعة فقط. لن يتم حفظ أي بيانات حتى يتم اختيار المشروع واعتماد خطة الترحيل الفعلية بعد مطابقة الصفوف.</Alert>
    </Stack>}
  </Box>;
}
