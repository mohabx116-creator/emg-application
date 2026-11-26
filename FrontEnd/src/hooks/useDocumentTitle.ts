import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

const ROUTE_TITLES: Array<{ match: (path: string) => boolean; title: string }> = [
  { match: (p) => p === '/', title: 'EMG & JUZOR APPLICATION' },
  { match: (p) => p.startsWith('/login'), title: 'EMG – تسجيل الدخول' },
  { match: (p) => p.startsWith('/account/password-change'), title: 'EMG – تغيير كلمة المرور' },
  { match: (p) => p === '/emg' || p === '/emg/dashboard', title: 'EMG – لوحة التحكم' },
  { match: (p) => p.includes('/movements') || p.startsWith('/emg/site-movements'), title: 'EMG – حركات الموقع' },
  { match: (p) => p.includes('/excavation'), title: 'EMG – أعمال الحفر' },
  { match: (p) => p.includes('/concrete'), title: 'EMG – أعمال الخرسانة' },
  { match: (p) => p.includes('/steel'), title: 'EMG – أعمال الحديد' },
  { match: (p) => p.includes('/finishing'), title: 'EMG – أعمال التشطيبات' },
  { match: (p) => p.startsWith('/emg/projects'), title: 'EMG – المشروعات' },
  { match: (p) => p.startsWith('/emg/suppliers'), title: 'EMG – الموردون' },
  { match: (p) => p.startsWith('/emg/contractor-statements'), title: 'EMG – كشوف المقاولين' },
  { match: (p) => p.startsWith('/emg/advances'), title: 'EMG – الدفعات تحت الحساب' },
  { match: (p) => p.startsWith('/emg/reports/consolidated'), title: 'EMG – كشف الحساب المجمع' },
  { match: (p) => p.startsWith('/emg/reports'), title: 'EMG – التقارير المالية' },
  { match: (p) => p.startsWith('/emg/attendance'), title: 'EMG – الحضور والانصراف' },
  { match: (p) => p.startsWith('/emg/imports'), title: 'EMG – استيراد البيانات' },
  { match: (p) => p.startsWith('/emg/users'), title: 'EMG – المستخدمون' },
  { match: (p) => p.startsWith('/emg/roles'), title: 'EMG – الأدوار والصلاحيات' },
  { match: (p) => p.startsWith('/emg/notifications'), title: 'EMG – الإشعارات' },
  { match: (p) => p.startsWith('/emg/profile'), title: 'EMG – الإعدادات' }
];

export function resolveEmgTitle(pathname: string): string {
  const found = ROUTE_TITLES.find((entry) => entry.match(pathname));
  return found ? found.title : 'EMG Construction ERP';
}

export function useDocumentTitle(customTitle?: string) {
  const { pathname } = useLocation();
  useEffect(() => {
    document.title = customTitle || resolveEmgTitle(pathname);
  }, [pathname, customTitle]);
}
