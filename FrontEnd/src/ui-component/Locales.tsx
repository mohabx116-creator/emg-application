import React, { useState, useEffect, useMemo } from 'react';

// third party
import { IntlProvider, MessageFormatElement } from 'react-intl';
import useConfig from 'hooks/useConfig';

// types
import { I18n } from 'types/config';

// load locales files
function loadLocaleData(i18n: I18n) {
  switch (i18n) {
    case 'ar':
      return import('utils/locales/ar.json');
    case 'fr':
      return import('utils/locales/fr.json');
    case 'ro':
      return import('utils/locales/ro.json');
    case 'zh':
      return import('utils/locales/zh.json');
    default:
      return import('utils/locales/en.json');
  }
}

// ==============================|| LOCALIZATION ||============================== //

interface LocalsProps {
  children: React.ReactNode;
}

export default function Locales({ children }: LocalsProps) {
  const {
    state: { i18n }
  } = useConfig();
  const [messages, setMessages] = useState<Record<string, string> | Record<string, MessageFormatElement[]>>({});

  const localeDataPromise = useMemo(() => loadLocaleData(i18n), [i18n]);
  useEffect(() => {
    let active = true;
    localeDataPromise.then((d: { default: Record<string, string> | Record<string, MessageFormatElement[]> }) => {
      if (active) setMessages(d.default || {});
    });
    return () => {
      active = false;
    };
  }, [localeDataPromise]);

  return (
    <IntlProvider locale={i18n} defaultLocale="ar" messages={messages} onError={() => undefined}>
      {children}
    </IntlProvider>
  );
}
