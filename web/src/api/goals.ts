import { apiFetch } from './client';

/**
 * A single nutrition/hydration target.
 *
 * `value` is the effective target (the override when set, otherwise the
 * computed value); `computed` is always the system-derived value; `isOverridden`
 * indicates the user has manually set this target.
 */
export interface GoalValue {
  value: number;
  computed: number;
  isOverridden: boolean;
}

/**
 * The full set of daily targets returned by `GET /api/v1/me/goals`.
 *
 * `bmr`/`tdee` are informational energy figures; `lastOverriddenAt` is null
 * until the user overrides at least one target.
 */
export interface GoalsResponse {
  calories: GoalValue;
  proteinGrams: GoalValue;
  carbohydrateGrams: GoalValue;
  fatGrams: GoalValue;
  waterMl: GoalValue;
  bmr: number;
  tdee: number;
  lastOverriddenAt: string | null;
}

/**
 * Body for `PUT /api/v1/me/goals/overrides`.
 *
 * Every field is optional: a `null` or omitted value clears that override
 * (reverting to the computed value); an empty object `{}` clears all overrides.
 */
export interface SetGoalOverridesRequest {
  caloriesKcal?: number | null;
  proteinGrams?: number | null;
  carbohydrateGrams?: number | null;
  fatGrams?: number | null;
  waterMl?: number | null;
}

/**
 * Fetches the current user's daily targets.
 *
 * Throws {@link ApiError} with status 404 (no profile) or 409 (profile missing
 * required biometrics) so the caller can prompt the user to complete their
 * profile.
 */
export async function getGoals(): Promise<GoalsResponse> {
  return apiFetch<GoalsResponse>('/api/v1/me/goals');
}

/**
 * Sets or clears the user's goal overrides.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages.
 */
export async function setGoalOverrides(req: SetGoalOverridesRequest): Promise<GoalsResponse> {
  return apiFetch<GoalsResponse>('/api/v1/me/goals/overrides', {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}
