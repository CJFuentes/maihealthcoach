import type { ReactElement } from 'react';
import { render, type RenderOptions } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import i18n from '../i18n';

/**
 * Renders `ui` wrapped in the real i18next instance (initialized in
 * `setupTests.ts`), so components calling `useTranslation()` resolve to the
 * bundled English strings. Use this when a test needs translations but does not
 * already get them through another provider.
 */
export function renderWithI18n(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
  return render(<I18nextProvider i18n={i18n}>{ui}</I18nextProvider>, options);
}
