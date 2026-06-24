import { vi } from 'vitest';
import type { useAuth } from '@clerk/clerk-react';

/**
 * Test helpers for mocking `@clerk/clerk-react`.
 *
 * There are no real Clerk credentials in CI, and `ClerkProvider` would attempt
 * to contact Clerk's servers, so every test that touches Clerk replaces the
 * module with a lightweight stub. Because `vi.mock` is hoisted above imports,
 * the mock *factory* must be inlined in each test file (it cannot reference an
 * imported helper). This module therefore only provides {@link makeAuthState},
 * which is called at runtime (not inside the hoisted factory) to build the
 * `useAuth()` return value for a given test case:
 *
 *   vi.mock('@clerk/clerk-react', () => ({ useAuth: vi.fn(), ... }));
 *   vi.mocked(useAuth).mockReturnValue(makeAuthState({ isSignedIn: true }));
 *
 * Kept inside `src/` (not a root `__mocks__/` dir) so it is typechecked by
 * `tsc` and linted by ESLint like the rest of the app.
 */

/** Shape of the auth state our components read off `useAuth()`. */
interface AuthStateOverrides {
  isLoaded?: boolean;
  isSignedIn?: boolean;
  getToken?: () => Promise<string | null>;
}

/**
 * Builds a `useAuth()` return value for a test case.
 *
 * Clerk's real `UseAuthReturn` is a wide discriminated union; we only need the
 * three fields our components consume, so we cast through `unknown` to the real
 * return type. This keeps tests resilient to Clerk adding union members.
 */
export function makeAuthState(overrides: AuthStateOverrides = {}): ReturnType<typeof useAuth> {
  const { isLoaded = true, isSignedIn = false, getToken } = overrides;
  return {
    isLoaded,
    isSignedIn,
    getToken: getToken ?? vi.fn<() => Promise<string | null>>().mockResolvedValue(null),
    signOut: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    userId: isSignedIn ? 'user_test' : null,
    sessionId: isSignedIn ? 'sess_test' : null,
  } as unknown as ReturnType<typeof useAuth>;
}
