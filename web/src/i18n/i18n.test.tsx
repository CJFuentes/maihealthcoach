import { describe, expect, it } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { useTranslation } from 'react-i18next';
import i18n from './index';

describe('i18n baseline', () => {
  it('resolves keys across namespaces to their English values', () => {
    expect(i18n.t('common:skipToContent')).toBe('Skip to main content');
    expect(i18n.t('nav:home')).toBe('Home');
    expect(i18n.t('profile:saveButton')).toBe('Save profile');
    expect(i18n.t('goals:title')).toBe('Goals');
    expect(i18n.t('scan:title')).toBe('Scan a barcode');
  });

  it('falls back to the key when a translation is missing', () => {
    // No such key exists; i18next returns the key itself as the fallback. The
    // typed t() intentionally rejects unknown keys at compile time, so we go
    // through an untyped reference to exercise the runtime fallback behaviour.
    const translate = i18n.t as (key: string) => string;
    expect(translate('common:doesNotExist')).toBe('doesNotExist');
  });

  it('switching to an unsupported language falls back to English without throwing', async () => {
    function Probe() {
      const { t } = useTranslation('nav');
      return <span data-testid="home-label">{t('home')}</span>;
    }

    render(<Probe />);

    expect(screen.getByTestId('home-label')).toHaveTextContent('Home');

    // Switching to a locale with no resources must not throw; English remains
    // the rendered fallback. Wrapped in act() because changeLanguage triggers a
    // re-render of subscribed components.
    await act(async () => {
      await i18n.changeLanguage('fr');
    });

    expect(screen.getByTestId('home-label')).toHaveTextContent('Home');

    // Restore the default language so other tests are unaffected.
    await act(async () => {
      await i18n.changeLanguage('en');
    });
  });
});
