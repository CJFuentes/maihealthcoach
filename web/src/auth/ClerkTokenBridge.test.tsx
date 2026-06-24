import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, waitFor } from '@testing-library/react';
import { useAuth } from '@clerk/clerk-react';
import { apiFetch, setTokenProvider } from '../api/client';
import { makeAuthState } from '../test/clerkTestUtils';
import ClerkTokenBridge from './ClerkTokenBridge';

// Factory is inlined (not an imported helper) because vi.mock is hoisted above
// imports — referencing an imported binding here throws at load time.
vi.mock('@clerk/clerk-react', () => ({ useAuth: vi.fn() }));

function mockJsonResponse(body: unknown): Response {
  return { ok: true, status: 200, json: async () => body } as Response;
}

afterEach(() => {
  // Restore the API client's default no-op provider so other test files are
  // unaffected (mirrors api/health.test.ts).
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('ClerkTokenBridge', () => {
  it('renders nothing', () => {
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isSignedIn: true }));

    const { container } = render(<ClerkTokenBridge />);

    expect(container).toBeEmptyDOMElement();
  });

  it('registers a provider so apiFetch attaches the Clerk token', async () => {
    const getToken = vi.fn<() => Promise<string | null>>().mockResolvedValue('clerk-jwt-xyz');
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isSignedIn: true, getToken }));

    render(<ClerkTokenBridge />);

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse({}));

    await waitFor(async () => {
      await apiFetch('/api/v1/ping');
      expect(fetchSpy).toHaveBeenCalledWith(
        '/api/v1/ping',
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: 'Bearer clerk-jwt-xyz' }),
        }),
      );
    });
    expect(getToken).toHaveBeenCalled();
  });

  it('omits the Authorization header when getToken returns null (signed out)', async () => {
    const getToken = vi.fn<() => Promise<string | null>>().mockResolvedValue(null);
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isSignedIn: false, getToken }));

    render(<ClerkTokenBridge />);

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse({}));

    await waitFor(async () => {
      await apiFetch('/api/v1/ping');
      expect(fetchSpy).toHaveBeenCalledWith(
        '/api/v1/ping',
        expect.objectContaining({
          headers: expect.not.objectContaining({ Authorization: expect.anything() }),
        }),
      );
    });
  });

  it('swallows a thrown getToken and sends an unauthenticated request', async () => {
    const getToken = vi
      .fn<() => Promise<string | null>>()
      .mockRejectedValue(new Error('no active session'));
    vi.mocked(useAuth).mockReturnValue(makeAuthState({ isSignedIn: false, getToken }));

    render(<ClerkTokenBridge />);

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse({}));

    await waitFor(async () => {
      await expect(apiFetch('/api/v1/ping')).resolves.toBeDefined();
      expect(fetchSpy).toHaveBeenCalledWith(
        '/api/v1/ping',
        expect.objectContaining({
          headers: expect.not.objectContaining({ Authorization: expect.anything() }),
        }),
      );
    });
  });
});
