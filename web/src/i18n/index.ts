import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import common from './locales/en/common.json';
import nav from './locales/en/nav.json';
import home from './locales/en/home.json';
import about from './locales/en/about.json';
import profile from './locales/en/profile.json';
import goals from './locales/en/goals.json';
import scan from './locales/en/scan.json';
import auth from './locales/en/auth.json';
import coach from './locales/en/coach.json';
import water from './locales/en/water.json';

/**
 * Namespaces bundled into the app. Each maps to one JSON file under
 * `locales/<lang>/`. Keep this list in sync with the `resources` map below and
 * with the module augmentation in `types.d.ts`.
 */
const STORAGE_KEY = 'mai-lang';

/**
 * Reads the persisted language, guarding `localStorage` access so the module is
 * safe to import in environments where storage may be unavailable (it exists in
 * jsdom, so tests resolve to the persisted value or the 'en' default).
 */
function readStoredLang(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? 'en';
  } catch {
    return 'en';
  }
}

// Initialized synchronously at module top-level (side effect): `initImmediate:
// false` makes init complete before this module finishes evaluating, so any
// code importing this module — including the test setup — sees a ready
// instance with the English resources already loaded. No backend plugin: all
// resources are bundled inline.
void i18n.use(initReactI18next).init({
  lng: readStoredLang(),
  fallbackLng: 'en',
  defaultNS: 'common',
  ns: ['common', 'nav', 'home', 'about', 'profile', 'goals', 'scan', 'auth', 'coach', 'water'],
  resources: {
    en: { common, nav, home, about, profile, goals, scan, auth, coach, water },
  },
  interpolation: {
    // React already escapes interpolated values, so i18next must not double-escape.
    escapeValue: false,
  },
  initImmediate: false,
  react: {
    useSuspense: false,
  },
});

// Persist the active language whenever it changes so the choice survives reloads.
i18n.on('languageChanged', (lng) => {
  try {
    localStorage.setItem(STORAGE_KEY, lng);
  } catch {
    // Ignore storage failures (private mode, disabled storage, etc.).
  }
});

export default i18n;
