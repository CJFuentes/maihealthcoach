import { useEffect } from 'react';
import { useAuth } from '@clerk/clerk-react';
import { setTokenProvider } from '../api/client';

/**
 * Registers Clerk's session token as the provider for outgoing API requests.
 *
 * Must be rendered inside `<ClerkProvider>`. On mount it calls
 * {@link setTokenProvider} with a function that delegates to Clerk's
 * `getToken()`, so every subsequent `apiFetch()` attaches a fresh Clerk JWT as
 * `Authorization: Bearer <token>` (the contract the backend validates).
 *
 * Clerk's `getToken()` throws when there is no active session, so the provider
 * catches and returns `null` — the API client then omits the Authorization
 * header rather than failing the request.
 *
 * Renders nothing — this is a side-effect-only component.
 */
export default function ClerkTokenBridge(): null {
  const { getToken } = useAuth();

  useEffect(() => {
    setTokenProvider(async () => {
      try {
        return await getToken();
      } catch {
        // No active session (or token refresh failed) — send an unauthenticated
        // request and let the caller handle the resulting 401.
        return null;
      }
    });
  }, [getToken]);

  return null;
}
