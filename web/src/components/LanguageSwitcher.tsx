import type { ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * Locales the app can switch between.
 *
 * To add a locale (e.g. French):
 *   1. Copy `src/i18n/locales/en/` to `src/i18n/locales/fr/` and translate the
 *      JSON values (leave the keys unchanged).
 *   2. Import the new namespace JSON in `src/i18n/index.ts` and add them under
 *      `resources.fr`.
 *   3. Add `{ code: 'fr', label: 'Français' }` to this array.
 *   4. Run `npm run typecheck && npm run test`.
 *
 * The switcher renders nothing while only one locale is supported, so it stays
 * invisible until a second locale is added — no dead UI in the meantime.
 */
const SUPPORTED_LOCALES = [{ code: 'en', label: 'English' }] as const;

export default function LanguageSwitcher() {
  const { i18n } = useTranslation();

  if (SUPPORTED_LOCALES.length <= 1) {
    return null;
  }

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    void i18n.changeLanguage(event.target.value);
  }

  return (
    <div className="language-switcher">
      <label htmlFor="language-select">Language</label>
      <select id="language-select" value={i18n.language} onChange={handleChange}>
        {SUPPORTED_LOCALES.map((locale) => (
          <option key={locale.code} value={locale.code}>
            {locale.label}
          </option>
        ))}
      </select>
    </div>
  );
}
