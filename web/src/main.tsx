import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import { BrowserRouter } from 'react-router-dom';
import { ClerkProvider } from '@clerk/clerk-react';
import { I18nextProvider } from 'react-i18next';
import './i18n';
import i18n from './i18n';
import { CLERK_PUBLISHABLE_KEY } from './env';
import ClerkTokenBridge from './auth/ClerkTokenBridge';
import App from './App';

const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('Root element #root not found in index.html');
}

// `BrowserRouter` wraps `ClerkProvider` so Clerk's embedded <SignIn>/<SignUp>
// flows (OAuth callback, email/MFA verification) drive SPA navigation through
// react-router instead of triggering full-page reloads. `ClerkTokenBridge`
// mounts inside the provider to wire Clerk's session token into the API client
// before any route component runs.
createRoot(rootElement).render(
  <StrictMode>
    <BrowserRouter>
      <I18nextProvider i18n={i18n}>
        <ClerkProvider publishableKey={CLERK_PUBLISHABLE_KEY} afterSignOutUrl="/sign-in">
          <ClerkTokenBridge />
          <App />
        </ClerkProvider>
      </I18nextProvider>
    </BrowserRouter>
  </StrictMode>,
);
