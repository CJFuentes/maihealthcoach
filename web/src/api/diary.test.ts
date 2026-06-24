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
  servingSizeId: 'serving-1',
  servingSizeLabel: '100 g',
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
      servingSizeId: 'serving-1',
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
  it('scales present fields and omits absent ones', () => {
    const result = scaleNutrition({ calories: 165, proteinGrams: 31 }, 1.5);

    expect(result).toEqual({ calories: 248, proteinGrams: 46.5 });
  });
});
