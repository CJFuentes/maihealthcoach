import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  addDiaryEntry,
  deleteDiaryEntry,
  getDailySummary,
  getDiary,
  scaleNutrition,
  updateDiaryEntry,
  type DailySummary,
  type DiaryEntry,
  type DiaryResponse,
} from './diary';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleEntry: DiaryEntry = {
  id: 'entry-1',
  foodId: 'food-1',
  foodName: 'Chicken Breast',
  mealType: 'Lunch',
  date: '2026-06-24',
  quantity: 1,
  servingLabel: '100 g',
  nutrition: { calories: 165, proteinGrams: 31 },
};

const sampleDiary: DiaryResponse = {
  date: '2026-06-24',
  meals: { Lunch: [sampleEntry] },
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getDiary', () => {
  it('returns the grouped meals on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleDiary));

    const result = await getDiary('2026-06-24');

    expect(result).toEqual(sampleDiary);
  });
});

describe('addDiaryEntry', () => {
  it('POSTs the request body to the diary endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleEntry));

    const req = {
      foodId: 'food-1',
      mealType: 'Lunch' as const,
      date: '2026-06-24',
      quantity: 1,
      servingLabel: '100 g',
    };
    const result = await addDiaryEntry(req);

    expect(result).toEqual(sampleEntry);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/diary',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(req) }),
    );
  });
});

describe('updateDiaryEntry', () => {
  it('PUTs the request body to the entry endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleEntry));

    const req = { quantity: 2 };
    await updateDiaryEntry('entry-1', req);

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/diary/entry-1',
      expect.objectContaining({ method: 'PUT', body: JSON.stringify(req) }),
    );
  });
});

describe('deleteDiaryEntry', () => {
  it('sends a DELETE and resolves on a 204 with no body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => {
        throw new SyntaxError('no body');
      },
    } as unknown as Response);

    await expect(deleteDiaryEntry('entry-1')).resolves.toBeUndefined();
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/diary/entry-1',
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});

describe('getDailySummary', () => {
  const sampleSummary: DailySummary = {
    date: '2026-06-24',
    totals: { calories: 1800, proteinGrams: 120 },
    goals: { calories: 2200 },
  };

  it('returns the summary on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleSummary));

    const result = await getDailySummary('2026-06-24');

    expect(result).toEqual(sampleSummary);
  });

  it('throws an ApiError on a 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(getDailySummary('2026-06-24')).rejects.toBeInstanceOf(ApiError);
    await expect(getDailySummary('2026-06-24')).rejects.toMatchObject({ status: 404 });
  });
});

describe('scaleNutrition', () => {
  it('scales per-100 g nutrition by serving grams and quantity', () => {
    // 100 g serving × 1.5 of a food at 110 kcal / 20.7 g protein per 100 g.
    const result = scaleNutrition(
      { energyKcal: 110, proteinG: 20.7, carbohydrateG: 0, fatG: 2.6 },
      100,
      1.5,
    );

    expect(result).toEqual({
      calories: 165,
      proteinGrams: 31.1,
      carbohydrateGrams: 0,
      fatGrams: 3.9,
    });
  });

  it('accounts for serving weight when it differs from 100 g', () => {
    // One 30 g serving of a 400 kcal / 100 g food = 120 kcal.
    const result = scaleNutrition(
      { energyKcal: 400, proteinG: 10, carbohydrateG: 60, fatG: 15 },
      30,
      1,
    );

    expect(result.calories).toBe(120);
  });
});
