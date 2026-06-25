import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  deleteExerciseEntry,
  getExerciseDay,
  logExercise,
  searchExercises,
  updateExerciseEntry,
  type Exercise,
  type ExerciseDayResponse,
  type ExerciseEntry,
} from './exercise';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleExercise: Exercise = {
  id: 'ex-1',
  name: 'Running',
  category: 'Cardio',
  met: 9.8,
};

const sampleEntry: ExerciseEntry = {
  id: 'entry-1',
  activityId: 'ex-1',
  activityName: 'Running',
  category: 'Cardio',
  durationMinutes: 30,
  caloriesBurned: 280,
  loggedAt: '2026-06-24T08:30:00.000Z',
  date: '2026-06-24',
};

const sampleDay: ExerciseDayResponse = {
  date: '2026-06-24',
  entries: [sampleEntry],
  totalCaloriesBurned: 280,
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('searchExercises', () => {
  it('normalises an { items } envelope to a search response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      mockJsonResponse({ items: [sampleExercise] }),
    );

    const result = await searchExercises('run');

    expect(result).toEqual({ items: [sampleExercise] });
  });

  it('normalises a bare array to a search response', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse([sampleExercise]));

    const result = await searchExercises('run');

    expect(result).toEqual({ items: [sampleExercise] });
  });

  it('normalises a malformed body to an empty list', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse({}));

    const result = await searchExercises('run');

    expect(result).toEqual({ items: [] });
  });

  it('normalises a non-array items value to an empty list', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      mockJsonResponse({ items: 'oops' }),
    );

    const result = await searchExercises('run');

    expect(result).toEqual({ items: [] });
  });

  it('passes the category filter in the URL when given', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse({ items: [sampleExercise] }));

    await searchExercises('run', 'Cardio');

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/exercises?q=run&category=Cardio',
      expect.anything(),
    );
  });

  it('throws an ApiError on a 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(searchExercises('run')).rejects.toBeInstanceOf(ApiError);
    await expect(searchExercises('run')).rejects.toMatchObject({ status: 404 });
  });
});

describe('getExerciseDay', () => {
  it('returns the day log on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleDay));

    const result = await getExerciseDay('2026-06-24');

    expect(result).toEqual(sampleDay);
  });

  it('throws an ApiError on a 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    await expect(getExerciseDay('2026-06-24')).rejects.toBeInstanceOf(ApiError);
    await expect(getExerciseDay('2026-06-24')).rejects.toMatchObject({ status: 404 });
  });
});

describe('logExercise', () => {
  it('POSTs the request body to the exercise endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleEntry));

    const req = { activityId: 'ex-1', durationMinutes: 30, date: '2026-06-24' };
    const result = await logExercise(req);

    expect(result).toEqual(sampleEntry);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/exercise',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(req) }),
    );
  });

  it('rejects with an ApiError carrying status 422 when body weight is missing', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse({}, false, 422));

    const req = { activityId: 'ex-1', durationMinutes: 30, date: '2026-06-24' };
    await expect(logExercise(req)).rejects.toBeInstanceOf(ApiError);
    await expect(logExercise(req)).rejects.toMatchObject({ status: 422 });
  });
});

describe('updateExerciseEntry', () => {
  it('PUTs the request body to the entry endpoint', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse({ ...sampleEntry, durationMinutes: 45 }));

    const req = { durationMinutes: 45 };
    await updateExerciseEntry('entry-1', req);

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/exercise/entry-1',
      expect.objectContaining({ method: 'PUT', body: JSON.stringify(req) }),
    );
  });
});

describe('deleteExerciseEntry', () => {
  it('sends a DELETE and resolves on a 204 with no body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => {
        throw new SyntaxError('no body');
      },
    } as unknown as Response);

    await expect(deleteExerciseEntry('entry-1')).resolves.toBeUndefined();
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/exercise/entry-1',
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});
