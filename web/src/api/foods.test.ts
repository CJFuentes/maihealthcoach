import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  lookupBarcode,
  normalizeBarcode,
  FoodServiceUnavailableError,
  type FoodDto,
} from './foods';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleFood: FoodDto = {
  id: 'food-1',
  name: 'Dark Chocolate 70%',
  brand: 'CocoaCo',
  barcode: '5000159484695',
  source: 'OpenFoodFacts',
  nutritionPer100g: {
    energyKcal: 598,
    proteinG: 7.8,
    carbohydrateG: 45.9,
    fatG: 42.6,
    fiberG: 11,
    sugarsG: 24,
  },
  servingSizes: [{ label: '1 square', grams: 10 }],
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('normalizeBarcode', () => {
  it('strips whitespace and non-digit characters', () => {
    expect(normalizeBarcode('  500 015-9484695 ')).toBe('5000159484695');
  });

  it('returns an empty string for input with no digits', () => {
    expect(normalizeBarcode('abc-xyz')).toBe('');
  });
});

describe('lookupBarcode', () => {
  it('returns the parsed food on 200', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleFood));

    const result = await lookupBarcode('5000159484695');

    expect(result).toEqual(sampleFood);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/foods/barcode/5000159484695',
      expect.objectContaining({}),
    );
  });

  it('normalizes the barcode before building the request URL', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleFood));

    await lookupBarcode('500 015-9484695');

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/foods/barcode/5000159484695',
      expect.objectContaining({}),
    );
  });

  it('returns null when the backend responds 404 (no product matched)', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    const result = await lookupBarcode('0000000000000');

    expect(result).toBeNull();
  });

  it('throws FoodServiceUnavailableError on 503 (upstream unavailable)', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 503));

    await expect(lookupBarcode('5000159484695')).rejects.toBeInstanceOf(
      FoodServiceUnavailableError,
    );
  });

  it('re-throws ApiError for other non-2xx failures', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 500));

    await expect(lookupBarcode('5000159484695')).rejects.toBeInstanceOf(ApiError);
    await expect(lookupBarcode('5000159484695')).rejects.toMatchObject({ status: 500 });
  });
});
