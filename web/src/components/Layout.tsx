import { UserButton } from '@clerk/clerk-react';
import { NavLink, Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import RouteAnnouncer from './RouteAnnouncer';
import LanguageSwitcher from './LanguageSwitcher';

export default function Layout() {
  const { t } = useTranslation('nav');
  const { t: tCommon } = useTranslation('common');

  return (
    <>
      {/* Skip link: first focusable element, lets keyboard users jump straight
          past the nav to the page content. Visually hidden until focused. */}
      <a href="#main-content" className="skip-link">
        {tCommon('skipToContent')}
      </a>
      <header>
        <nav aria-label={t('primaryNavLabel')}>
          <NavLink to="/" end>
            {t('home')}
          </NavLink>
          <NavLink to="/about">{t('about')}</NavLink>
          <NavLink to="/profile">{t('profile')}</NavLink>
          <NavLink to="/goals">{t('goals')}</NavLink>
          <NavLink to="/diary">{t('diary')}</NavLink>
          <NavLink to="/scan">{t('scan')}</NavLink>
          <NavLink to="/coach">{t('coach')}</NavLink>
        </nav>
        {/* Clerk account menu: profile + sign-out. Renders nothing when signed out.
            Sign-out redirect is configured once on <ClerkProvider> (afterSignOutUrl). */}
        <UserButton />
        <LanguageSwitcher />
      </header>
      <main id="main-content" tabIndex={-1}>
        <RouteAnnouncer />
        <Outlet />
      </main>
    </>
  );
}
