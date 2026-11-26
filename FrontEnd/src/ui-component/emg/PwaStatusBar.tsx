import { useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Collapse from '@mui/material/Collapse';
import Stack from '@mui/material/Stack';
import { IconDownload, IconRefresh, IconWifiOff } from '@tabler/icons-react';

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

export default function PwaStatusBar() {
  const [offline, setOffline] = useState(!navigator.onLine);
  const [updateWaiting, setUpdateWaiting] = useState<ServiceWorker | null>(null);
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null);
  const [dismissedInstall, setDismissedInstall] = useState(false);

  useEffect(() => {
    const onOffline = () => setOffline(true);
    const onOnline = () => setOffline(false);
    const onInstallPrompt = (e: Event) => {
      e.preventDefault();
      setInstallEvent(e as BeforeInstallPromptEvent);
    };
    const onSwUpdate = (e: Event) => {
      const custom = e as CustomEvent<ServiceWorker>;
      if (custom.detail) setUpdateWaiting(custom.detail);
    };

    window.addEventListener('offline', onOffline);
    window.addEventListener('online', onOnline);
    window.addEventListener('beforeinstallprompt', onInstallPrompt);
    window.addEventListener('emg-sw-update', onSwUpdate);
    return () => {
      window.removeEventListener('offline', onOffline);
      window.removeEventListener('online', onOnline);
      window.removeEventListener('beforeinstallprompt', onInstallPrompt);
      window.removeEventListener('emg-sw-update', onSwUpdate);
    };
  }, []);

  const handleInstall = async () => {
    if (!installEvent) return;
    await installEvent.prompt();
    await installEvent.userChoice;
    setInstallEvent(null);
  };

  const handleApplyUpdate = () => {
    if (updateWaiting) {
      updateWaiting.postMessage({ type: 'SKIP_WAITING' });
    }
    window.location.reload();
  };

  return (
    <Stack spacing={1} sx={{ mb: offline || updateWaiting || (installEvent && !dismissedInstall) ? 1.5 : 0 }}>
      <Collapse in={offline}>
        <Alert severity="warning" icon={<IconWifiOff size={20} />} sx={{ borderRadius: 2 }}>
          انقطع الاتصال بالإنترنت. يرجى استعادة الاتصال قبل حفظ أو تعديل أي معاملات مالية.
        </Alert>
      </Collapse>

      <Collapse in={Boolean(updateWaiting)}>
        <Alert
          severity="info"
          action={
            <Button color="inherit" size="small" startIcon={<IconRefresh size={16} />} onClick={handleApplyUpdate}>
              تحديث الآن
            </Button>
          }
          sx={{ borderRadius: 2 }}
        >
          يتوفر إصدار جديد من منظومة EMG.
        </Alert>
      </Collapse>

      <Collapse in={Boolean(installEvent && !dismissedInstall && !offline)}>
        <Alert
          severity="info"
          icon={<IconDownload size={20} />}
          onClose={() => setDismissedInstall(true)}
          action={
            <Button color="primary" variant="contained" size="small" onClick={handleInstall}>
              تثبيت التطبيق
            </Button>
          }
          sx={{ borderRadius: 2 }}
        >
          يمكنك تثبيت تطبيق EMG على الشاشة الرئيسية للوصول السريع من الهاتف.
        </Alert>
      </Collapse>
    </Stack>
  );
}
