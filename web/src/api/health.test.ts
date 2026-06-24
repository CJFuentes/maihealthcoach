import { afterEach, describe, expect, it, vi } from 'vitest';
import { ping } from './health';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('ping', () => {
  it('returns the parsed JSON response', async () => {
    const payload = { service: 'mai-api', version: '1.0.0', timestamp: '2026-06-24T00:00:00Z' };
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(payload));

    const result = await ping();

    expect(result).toEqual(payload);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/ping',
      expect.objectContaining({
        headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
      }),
    );
  });

  it('injects an Authorization header when a token is provided', async () => {
    setTokenProvider(() => 'test-token-abc');
    const payload = { service: 'mai-api', version: '1.0.0', timestamp: '2026-06-24T00:00:00Z' };
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(payload));

    await ping();

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/ping',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer test-token-abc' }),
      }),
    );
  });

  it('throws an ApiError on a non-2xx response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(ping()).rejects.toMatchObject({
      name: 'ApiError',
      status: 404,
    });
    await expect(ping()).rejects.toBeInstanceOf(ApiError);
  });
});
