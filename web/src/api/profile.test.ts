import { afterEach, describe, expect, it, vi } from 'vitest';
import { getProfile, updateProfile, type ProfileResponse } from './profile';
import { ApiError, setTokenProvider } from './client';

function mockJsonResponse(body: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => body,
  } as Response;
}

const sampleProfile: ProfileResponse = {
  id: 'profile-1',
  userId: 'user-1',
  heightCm: 180,
  dateOfBirth: '1990-01-01',
  biologicalSex: 'Male',
  activityLevel: 'ModeratelyActive',
  primaryGoal: 'Maintain',
  units: 'Metric',
  dietType: 'None',
  allergies: null,
  latestWeightKg: 80,
  weightHistory: [{ weightKg: 80, measuredAt: '2026-06-24T00:00:00Z' }],
  createdAt: '2026-06-24T00:00:00Z',
  updatedAt: '2026-06-24T00:00:00Z',
};

afterEach(() => {
  setTokenProvider(() => null);
  vi.restoreAllMocks();
});

describe('getProfile', () => {
  it('returns the parsed profile on 200', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(sampleProfile));

    const result = await getProfile();

    expect(result).toEqual(sampleProfile);
  });

  it('returns null when the backend responds 404', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 404));

    const result = await getProfile();

    expect(result).toBeNull();
  });

  it('re-throws ApiError for non-404 failures', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(null, false, 500));

    await expect(getProfile()).rejects.toBeInstanceOf(ApiError);
    await expect(getProfile()).rejects.toMatchObject({ status: 500 });
  });
});

describe('updateProfile', () => {
  it('PUTs the request body and returns the updated profile', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(mockJsonResponse(sampleProfile));

    const result = await updateProfile({ heightCm: 180, units: 'Metric' });

    expect(result).toEqual(sampleProfile);
    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/me/profile',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ heightCm: 180, units: 'Metric' }),
      }),
    );
  });

  it('throws an ApiError carrying ProblemDetails on a 400', async () => {
    const problem = {
      title: 'Validation failed',
      errors: { heightCm: ['Height must be between 50 and 272 cm.'] },
    };
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(mockJsonResponse(problem, false, 400));

    await expect(updateProfile({ heightCm: 5, units: 'Metric' })).rejects.toMatchObject({
      status: 400,
      problem: { errors: { heightCm: ['Height must be between 50 and 272 cm.'] } },
    });
  });
});
