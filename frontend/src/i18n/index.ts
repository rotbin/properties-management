import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import he from './locales/he/translation.json';
import en from './locales/en/translation.json';

const resources = {
  he: { translation: he },
  en: { translation: en },
};

/**
 * Robust language detection with the following priority:
 *  1. Stored preference in localStorage (key: "lang")
 *  2. Browser/system language (navigator.language)
 *  3. Fallback: if detected language is Hebrew → "he", otherwise → "en"
 */
function detectLanguage(): string {
  // Priority 1: stored preference
  try {
    const stored = localStorage.getItem('lang');
    if (stored === 'he' || stored === 'en') return stored;
  } catch {
    // localStorage may be unavailable (private browsing, etc.)
  }

  // Priority 2: browser/system language
  const browserLang = navigator.language || (navigator as any).userLanguage || '';
  const primary = browserLang.split('-')[0].toLowerCase();

  // Priority 3: Hebrew → "he", anything else → "en"
  return primary === 'he' ? 'he' : 'en';
}

const detectedLng = detectLanguage();

i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: detectedLng,
    fallbackLng: 'en',
    supportedLngs: ['he', 'en'],
    interpolation: {
      escapeValue: false, // React already escapes
    },
  });

// Persist language choice to localStorage on every change
i18n.on('languageChanged', (lng: string) => {
  try {
    localStorage.setItem('lang', lng);
  } catch {
    // ignore
  }
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
