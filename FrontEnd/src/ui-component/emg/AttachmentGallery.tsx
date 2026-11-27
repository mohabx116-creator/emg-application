import { ChangeEvent, useEffect, useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  Stack,
  Typography
} from '@mui/material';
import { IconDownload, IconEye, IconFile } from '@tabler/icons-react';
import { downloadEmgAttachment, EmgAttachment, getEmgAttachmentPreviewUrl, getEmgAttachments, uploadEmgAttachment } from 'api/emg';

type Props = {
  entityType: string;
  entityId: number;
  title?: string;
  onMessage?: (message: { severity: 'success' | 'error'; text: string }) => void;
};

const accepted = '.pdf,.jpg,.jpeg,.png,.webp,.xlsx,.xls,.doc,.docx';

export default function AttachmentGallery({ entityType, entityId, title = 'المرفقات', onMessage }: Props) {
  const [files, setFiles] = useState<EmgAttachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [previewFile, setPreviewFile] = useState<EmgAttachment | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    try {
      setFiles(await getEmgAttachments(entityType, entityId));
    } catch {
      setFiles([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [entityType, entityId]);
  useEffect(
    () => () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    },
    [previewUrl]
  );

  const closePreview = () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(null);
    setPreviewFile(null);
  };

  const openPreview = async (file: EmgAttachment) => {
    closePreview();
    setPreviewFile(file);
    setPreviewLoading(true);
    try {
      setPreviewUrl(await getEmgAttachmentPreviewUrl(file.id));
    } catch {
      setPreviewFile(null);
      onMessage?.({ severity: 'error', text: 'تعذر عرض المرفق.' });
    } finally {
      setPreviewLoading(false);
    }
  };

  const upload = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    setUploading(true);
    try {
      await uploadEmgAttachment(entityType, entityId, file);
      await load();
      onMessage?.({ severity: 'success', text: 'تم رفع المرفق بنجاح.' });
    } catch (error: any) {
      onMessage?.({ severity: 'error', text: error?.message || 'تعذر رفع المرفق.' });
    } finally {
      setUploading(false);
    }
  };

  const isImage = previewFile?.contentType.startsWith('image/') ?? false;
  const isPdf = previewFile?.contentType === 'application/pdf' || previewFile?.originalFileName.toLowerCase().endsWith('.pdf');

  return (
    <>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
        <Typography variant="h4">{title}</Typography>
        <Stack direction="row" spacing={1} alignItems="center">
          <Chip size="small" label={`${files.length} ملف`} variant="outlined" />
          <Button component="label" size="small" variant="contained" disabled={uploading}>
            {uploading ? 'جاري الرفع...' : 'إضافة مرفق'}
            <input hidden type="file" accept={accepted} onChange={upload} />
          </Button>
        </Stack>
      </Stack>
      {loading ? (
        <Box sx={{ display: 'grid', placeItems: 'center', py: 3 }}>
          <CircularProgress size={24} />
        </Box>
      ) : (
        <Stack spacing={1.25}>
          {files.map((file) => (
            <Card key={file.id} sx={{ border: '1px solid', borderColor: 'divider', boxShadow: 1 }}>
              <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  justifyContent="space-between"
                  alignItems={{ xs: 'stretch', sm: 'center' }}
                  spacing={1.5}
                >
                  <Stack direction="row" spacing={1} alignItems="center" minWidth={0}>
                    <IconFile size={21} />
                    <Box minWidth={0}>
                      <Typography fontWeight={700} noWrap>
                        {file.originalFileName}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {Math.ceil(file.sizeInBytes / 1024)} ك.ب · {new Date(file.uploadedAtUtc).toLocaleString('ar-EG-u-nu-latn')}
                      </Typography>
                    </Box>
                  </Stack>
                  <Stack direction="row" spacing={1} justifyContent="flex-end">
                    <Button size="small" variant="outlined" onClick={() => openPreview(file)} startIcon={<IconEye size={15} />}>
                      عرض
                    </Button>
                    <Button
                      size="small"
                      onClick={() => downloadEmgAttachment(file.id, file.originalFileName)}
                      startIcon={<IconDownload size={15} />}
                    >
                      تحميل
                    </Button>
                  </Stack>
                </Stack>
              </CardContent>
            </Card>
          ))}
          {!files.length && <Typography color="text.secondary">لا توجد مرفقات لهذه العملية.</Typography>}
        </Stack>
      )}
      <Dialog open={Boolean(previewFile)} onClose={closePreview} fullWidth maxWidth="lg">
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2 }}>
          {previewFile?.originalFileName}
          <Button onClick={closePreview}>إغلاق</Button>
        </DialogTitle>
        <DialogContent dividers sx={{ minHeight: { xs: 320, md: 560 }, display: 'grid', placeItems: 'center', bgcolor: 'grey.100' }}>
          {previewLoading && <CircularProgress />}
          {!previewLoading && previewUrl && isImage && (
            <Box
              component="img"
              src={previewUrl}
              alt={previewFile?.originalFileName}
              sx={{ maxWidth: '100%', maxHeight: '70vh', objectFit: 'contain', borderRadius: 1, boxShadow: 3 }}
            />
          )}
          {!previewLoading && previewUrl && isPdf && (
            <Box
              component="iframe"
              src={previewUrl}
              title={previewFile?.originalFileName}
              sx={{ width: '100%', height: '70vh', border: 0, bgcolor: 'background.paper' }}
            />
          )}
          {!previewLoading && previewUrl && !isImage && !isPdf && (
            <Stack alignItems="center" spacing={2}>
              <IconFile size={52} />
              <Typography>هذا النوع من الملفات لا يدعم المعاينة المباشرة.</Typography>
              <Button
                variant="contained"
                onClick={() => previewFile && downloadEmgAttachment(previewFile.id, previewFile.originalFileName)}
                startIcon={<IconDownload size={17} />}
              >
                تحميل الملف
              </Button>
            </Stack>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
