import { afterEach, describe, expect, it, vi } from 'vitest';
import { getFood, searchFoods, type Food, type FoodSearchResponse } from './foods';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleFood: Food = {
  id: 'food-1',
  name: 'Chicken Breast',
  nutrition: { calories: 165, proteinGrams: 31, carbohydrateGrams: 0, fatGrams: 3.6 },
  servingSizes: [
    {
      id: 'serving-1',
      label: '100 g',
      weightGrams: 100,
      nutrition: { calories: 165, proteinGrams: 31, carbohydrateGrams: 0, fatGrams: 3.6 },
    },
  ],
  defaultServingSizeId: 'serving-1',
};

const sampleResponse: FoodSearchResponse = {
  items: [sampleFood],
  total: 1,
  page: 1,
  pageSize: 20,
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('searchFoods', () => {
  it('returns the parsed response on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleResponse));

    const result = await searchFoods('chicken');

    expect(result).toEqual(sampleResponse);
  });

  it('URL-encodes the query', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleResponse));

    await searchFoods('chicken breast');

    expect(fetchSpy).toHaveBeenCalledWith(
      expect.stringContaining(encodeURIComponent('chicken breast')),
      expect.anything(),
    );
  });

  it('throws an ApiError on a 500', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 500));

    await expect(searchFoods('chicken')).rejects.toBeInstanceOf(ApiError);
    await expect(searchFoods('chicken')).rejects.toMatchObject({ status: 500 });
  });
});

describe('getFood', () => {
  it('returns the parsed food on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleFood));

    const result = await getFood('food-1');

    expect(result).toEqual(sampleFood);
  });

  it('throws an ApiError with status 404 when the food is missing', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(getFood('missing')).rejects.toBeInstanceOf(ApiError);
    await expect(getFood('missing')).rejects.toMatchObject({ status: 404 });
  });
});
