import { afterEach, describe, expect, it, vi } from 'vitest';
import { getDashboard, type DashboardResponse } from './dashboard';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleDashboard: DashboardResponse = {
  date: '2026-06-24',
  goalsAvailable: true,
  calories: {
    calories: { consumed: 1200, target: 2000, remaining: 800, percentOfTarget: 60 },
    proteinG: { consumed: 80, target: 120, remaining: 40, percentOfTarget: 67 },
    carbohydrateG: { consumed: 150, target: 250, remaining: 100, percentOfTarget: 60 },
    fatG: { consumed: 40, target: 70, remaining: 30, percentOfTarget: 57 },
    entryCount: 3,
  },
  water: { goalsAvailable: true, consumedMl: 750, goalMl: 2000, remainingMl: 1250 },
  exercise: { totalCaloriesBurned: 320, entryCount: 1 },
  netCalories: 880,
  streak: {
    currentStreak: 5,
    longestStreak: 12,
    caloriesAdherence7d: 71,
    waterAdherence7d: 86,
  },
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getDashboard', () => {
  it('returns the dashboard snapshot on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleDashboard));

    const result = await getDashboard('2026-06-24');

    expect(result).toEqual(sampleDashboard);
  });

  it('requests the dashboard endpoint with the URL-encoded date', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleDashboard));

    await getDashboard('2026-06-24');

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/dashboard?date=2026-06-24',
      expect.objectContaining({ headers: expect.any(Object) }),
    );
  });

  it('throws an ApiError on a 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(getDashboard('2026-06-24')).rejects.toBeInstanceOf(ApiError);
    await expect(getDashboard('2026-06-24')).rejects.toMatchObject({ status: 404 });
  });
});
