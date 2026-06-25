import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  addWaterEntry,
  deleteWaterEntry,
  getWaterDay,
  updateWaterEntry,
  type WaterDayResponse,
  type WaterEntry,
} from './water';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleEntry: WaterEntry = {
  id: 'water-1',
  amountMl: 250,
  loggedAt: '2026-06-24T08:30:00.000Z',
};

const sampleDay: WaterDayResponse = {
  date: '2026-06-24',
  entries: [sampleEntry],
  totalMl: 250,
  goalMl: 2000,
  remainingMl: 1750,
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getWaterDay', () => {
  it('returns the day log on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleDay));

    const result = await getWaterDay('2026-06-24');

    expect(result).toEqual(sampleDay);
  });

  it('throws an ApiError on a 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(getWaterDay('2026-06-24')).rejects.toBeInstanceOf(ApiError);
    await expect(getWaterDay('2026-06-24')).rejects.toMatchObject({ status: 404 });
  });
});

describe('addWaterEntry', () => {
  it('POSTs the request body to the water endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleEntry));

    const req = { amountMl: 250 };
    const result = await addWaterEntry(req);

    expect(result).toEqual(sampleEntry);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/water',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(req) }),
    );
  });
});

describe('updateWaterEntry', () => {
  it('PUTs the request body to the entry endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse({ ...sampleEntry, amountMl: 500 }));

    const req = { amountMl: 500 };
    await updateWaterEntry('water-1', req);

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/water/water-1',
      expect.objectContaining({ method: 'PUT', body: JSON.stringify(req) }),
    );
  });
});

describe('deleteWaterEntry', () => {
  it('sends a DELETE and resolves on a 204 with no body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => {
        throw new SyntaxError('no body');
      },
    } as unknown as Response);

    await expect(deleteWaterEntry('water-1')).resolves.toBeUndefined();
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/water/water-1',
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});
