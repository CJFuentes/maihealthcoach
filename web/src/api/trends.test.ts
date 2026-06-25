import { afterEach, describe, expect, it, vi } from 'vitest';
import { getTrends, type TrendsResponse } from './trends';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleTrends: TrendsResponse = {
  from: '2026-06-19',
  to: '2026-06-25',
  caloriesConsumed: [
    { date: '2026-06-19', value: 1800 },
    { date: '2026-06-20', value: 2100 },
    { date: '2026-06-21', value: 0 },
    { date: '2026-06-22', value: 1950 },
    { date: '2026-06-23', value: 2200 },
    { date: '2026-06-24', value: 1700 },
    { date: '2026-06-25', value: 1850 },
  ],
  caloriesBurned: [
    { date: '2026-06-19', value: 300 },
    { date: '2026-06-20', value: 0 },
    { date: '2026-06-21', value: 450 },
    { date: '2026-06-22', value: 200 },
    { date: '2026-06-23', value: 380 },
    { date: '2026-06-24', value: 0 },
    { date: '2026-06-25', value: 410 },
  ],
  netCalories: [
    { date: '2026-06-19', value: 1500 },
    { date: '2026-06-20', value: 2100 },
    { date: '2026-06-21', value: -450 },
    { date: '2026-06-22', value: 1750 },
    { date: '2026-06-23', value: 1820 },
    { date: '2026-06-24', value: 1700 },
    { date: '2026-06-25', value: 1440 },
  ],
  waterMl: [
    { date: '2026-06-19', value: 1500 },
    { date: '2026-06-20', value: 2000 },
    { date: '2026-06-21', value: 0 },
    { date: '2026-06-22', value: 1800 },
    { date: '2026-06-23', value: 2200 },
    { date: '2026-06-24', value: 1600 },
    { date: '2026-06-25', value: 1900 },
  ],
  weight: [
    { date: '2026-06-20', weightKg: 80.5 },
    { date: '2026-06-24', weightKg: 79.8 },
  ],
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getTrends', () => {
  it('returns the trends payload on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleTrends));

    const result = await getTrends(7);

    expect(result).toEqual(sampleTrends);
  });

  it('requests the trends endpoint with a range query when given a number', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleTrends));

    await getTrends(30);

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('?range=30'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );
  });

  it('requests the trends endpoint with from/to when given an explicit window', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleTrends));

    await getTrends({ from: '2026-06-01', to: '2026-06-07' });

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining('from=2026-06-01&to=2026-06-07'),
      expect.objectContaining({ headers: expect.any(Object) }),
    );
  });

  it('requests the bare endpoint with no query when called without arguments', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleTrends));

    await getTrends();

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/trends',
      expect.objectContaining({ headers: expect.any(Object) }),
    );
  });

  it('throws an ApiError carrying the status on a 401', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 401));

    await expect(getTrends(7)).rejects.toBeInstanceOf(ApiError);
    await expect(getTrends(7)).rejects.toMatchObject({ status: 401 });
  });
});
