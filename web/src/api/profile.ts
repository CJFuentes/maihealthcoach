import { apiFetch, ApiError } from './client';

/** Biological sex options accepted by the profile. */
export type BiologicalSex = 'Male' | 'Female';

/** Self-reported activity level, used to derive TDEE. */
export type ActivityLevel =
  | 'Sedentary'
  | 'LightlyActive'
  | 'ModeratelyActive'
  | 'VeryActive'
  | 'ExtraActive';

/** The user's primary weight goal. */
export type PrimaryGoal = 'Lose' | 'Maintain' | 'Gain';

/** Measurement system the user prefers to view/enter values in. */
export type UnitsPreference = 'Metric' | 'Imperial';

/** Dietary pattern, used to tailor coaching and goals. */
export type DietType = 'None' | 'Vegetarian' | 'Vegan' | 'Pescatarian' | 'Keto' | 'Paleo';

/** A single recorded body-weight measurement. */
export interface WeightHistoryEntry {
  weightKg: number;
  measuredAt: string;
}

/**
 * The full profile as returned by `GET /api/v1/me/profile`.
 *
 * All biometric fields are nullable because a freshly created account has no
 * profile yet; `units` always has a value (defaults to "Metric").
 */
export interface ProfileResponse {
  id: string;
  userId: string;
  heightCm: number | null;
  dateOfBirth: string | null;
  biologicalSex: BiologicalSex | null;
  activityLevel: ActivityLevel | null;
  primaryGoal: PrimaryGoal | null;
  units: UnitsPreference;
  dietType: DietType | null;
  allergies: string | null;
  latestWeightKg: number | null;
  weightHistory: WeightHistoryEntry[];
  createdAt: string;
  updatedAt: string;
}

/**
 * Body for `PUT /api/v1/me/profile`.
 *
 * Every field is optional: omitted fields are left unchanged on the server.
 * Weight is supplied via `weightKg` (a new measurement), not the read-only
 * `latestWeightKg`.
 */
export interface UpdateProfileRequest {
  heightCm?: number;
  dateOfBirth?: string;
  biologicalSex?: BiologicalSex;
  activityLevel?: ActivityLevel;
  primaryGoal?: PrimaryGoal;
  units?: UnitsPreference;
  dietType?: DietType;
  allergies?: string;
  weightKg?: number;
}

/**
 * Fetches the current user's profile.
 *
 * Returns `null` when the backend responds 404 (no profile created yet) so the
 * UI can render a blank form rather than an error. Any other error propagates.
 */
export async function getProfile(): Promise<ProfileResponse | null> {
  try {
    return await apiFetch<ProfileResponse>('/api/v1/me/profile');
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }
    throw error;
  }
}

/**
 * Creates or updates the current user's profile.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages.
 */
export async function updateProfile(req: UpdateProfileRequest): Promise<ProfileResponse> {
  return apiFetch<ProfileResponse>('/api/v1/me/profile', {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}
