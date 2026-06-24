import { afterEach, describe, expect, it, vi } from 'vitest';
import { getGoals, setGoalOverrides, type GoalsResponse } from './goals';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleGoals: GoalsResponse = {
  calories: { value: 2200, computed: 2200, isOverridden: false },
  proteinGrams: { value: 165, computed: 165, isOverridden: false },
  carbohydrateGrams: { value: 248, computed: 248, isOverridden: false },
  fatGrams: { value: 73, computed: 73, isOverridden: false },
  waterMl: { value: 3000, computed: 3000, isOverridden: false },
  bmr: 1750,
  tdee: 2200,
  lastOverriddenAt: null,
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getGoals', () => {
  it('returns the parsed goals on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleGoals));

    const result = await getGoals();

    expect(result).toEqual(sampleGoals);
  });

  it('throws an ApiError with status 409 for an incomplete profile', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      mockJsonResponse({ title: 'Incomplete profile.' }, false, 409),
    );

    await expect(getGoals()).rejects.toBeInstanceOf(ApiError);
    await expect(getGoals()).rejects.toMatchObject({ status: 409 });
  });
});

describe('setGoalOverrides', () => {
  it('PUTs the override body and returns the updated goals', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleGoals));

    const result = await setGoalOverrides({ caloriesKcal: 2000 });

    expect(result).toEqual(sampleGoals);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/goals/overrides',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ caloriesKcal: 2000 }),
      }),
    );
  });

  it('clears all overrides with an empty object', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleGoals));

    await setGoalOverrides({});

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/goals/overrides',
      expect.objectContaining({ method: 'PUT', body: '{}' }),
    );
  });
});
