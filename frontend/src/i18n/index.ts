import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import he from './locales/he/translation.json';
import en from './locales/en/translation.json';

const resources = {
  he: { translation: he },
  en: { translation: en },
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'he',
    supportedLngs: ['he', 'en'],
    interpolation: {
      escapeValue: false, // React already escapes
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'lang',
      caches: ['localStorage'],
    },
  });

export default i18n;

/** Get the locale string for Intl APIs based on current language */
export function getIntlLocale(): string {
  return i18n.language === 'he' ? 'he-IL' : 'en-US';
}

/** Check if current language is RTL */
export function isRtl(): boolean {
  return i18n.language === 'he';
}
