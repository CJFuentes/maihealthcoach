import { apiFetch } from './client';

/**
 * Per-nutrient (or calorie) summary for the day.
 *
 * `target`/`remaining`/`percentOfTarget` are null when the user has no goals
 * set, so the UI can fall back to an indeterminate (no-goal) presentation.
 * `consumed` is always present (zero when nothing is logged); `percentOfTarget`
 * is the consumed amount as a percentage of the target (may exceed 100).
 */
export interface NutrientSummary {
  consumed: number;
  target: number | null;
  remaining: number | null;
  percentOfTarget: number | null;
}

/**
 * The calories-and-macros block of the dashboard.
 *
 * Each of `calories`, `proteinG`, `carbohydrateG`, and `fatG` is a
 * {@link NutrientSummary}; `entryCount` is the number of diary entries logged
 * for the day (zero indicates an empty day).
 */
export interface DashboardCalories {
  calories: NutrientSummary;
  proteinG: NutrientSummary;
  carbohydrateG: NutrientSummary;
  fatG: NutrientSummary;
  entryCount: number;
}

/**
 * The water block of the dashboard.
 *
 * `goalsAvailable` is false (and `goalMl`/`remainingMl` are null) when the user
 * has no hydration goal, so the UI renders an indeterminate water bar carrying
 * just the consumed amount.
 */
export interface DashboardWater {
  goalsAvailable: boolean;
  consumedMl: number;
  goalMl: number | null;
  remainingMl: number | null;
}

/**
 * The exercise block of the dashboard: the day's total calories burned across
 * `entryCount` logged activities.
 */
export interface DashboardExercise {
  totalCaloriesBurned: number;
  entryCount: number;
}

/**
 * The streak block of the dashboard.
 *
 * `currentStreak`/`longestStreak` are consecutive-day logging counts; the
 * `*Adherence7d` figures are the user's 7-day adherence percentages. They are
 * null only when the user has no complete profile (goals are unavailable); they
 * become non-null as soon as goals can be computed, even on day one.
 */
export interface DashboardStreak {
  currentStreak: number;
  longestStreak: number;
  caloriesAdherence7d: number | null;
  waterAdherence7d: number | null;
}

/**
 * The daily dashboard snapshot returned by
 * `GET /api/v1/me/dashboard?date=YYYY-MM-DD`.
 *
 * `goalsAvailable` reflects whether the user has nutrition goals set;
 * `netCalories` (consumed minus burned) is null only when the day has no diary
 * entries and no exercise entries (there is no meaningful net when both sides
 * are empty) — it may be non-null even when `goalsAvailable` is false. All
 * amounts are zeroed (not omitted) on an empty day, so the page can render the
 * snapshot without null-checking every figure.
 */
export interface DashboardResponse {
  date: string;
  goalsAvailable: boolean;
  calories: DashboardCalories;
  water: DashboardWater;
  exercise: DashboardExercise;
  netCalories: number | null;
  streak: DashboardStreak;
}

/**
 * Fetches the daily dashboard snapshot for the given day (ISO `YYYY-MM-DD`):
 * calorie/macro progress, water progress, the exercise summary, net calories,
 * and the logging streak.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getDashboard(date: string): Promise<DashboardResponse> {
  return apiFetch<DashboardResponse>(
    `/api/v1/me/dashboard?date=${encodeURIComponent(date)}`,
  );
}
