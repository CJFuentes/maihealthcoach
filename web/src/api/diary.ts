import { apiFetch } from './client';
import type { FoodNutrition } from './foods';

/** Which meal a diary entry belongs to. */
export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';

/**
 * Nutrition for a logged diary entry — identical in shape to
 * {@link FoodNutrition} (calories required, the rest optional).
 */
export type EntryNutrition = FoodNutrition;

/**
 * A single logged food in the diary.
 *
 * `nutrition` is the scaled nutrition for the chosen quantity/serving and may
 * be absent when the backend has not computed it; `quantity` is the multiplier
 * applied to the serving identified by `servingSizeId`.
 */
export interface DiaryEntry {
  id: string;
  foodId: string;
  foodName: string;
  brand?: string;
  mealType: MealType;
  date: string;
  quantity: number;
  servingSizeId?: string;
  servingSizeLabel?: string;
  nutrition?: EntryNutrition;
  loggedAt?: string;
}

/**
 * The diary for a single day, grouped by meal.
 *
 * `meals` is partial because a meal with no entries is simply omitted.
 */
export interface DiaryResponse {
  date: string;
  meals: Partial<Record<MealType, DiaryEntry[]>>;
}

/** Body for `POST /api/v1/me/diary` to log a new entry. */
export interface AddDiaryEntryRequest {
  foodId: string;
  mealType: MealType;
  date: string;
  quantity: number;
  servingSizeId?: string;
}

/**
 * Body for `PUT /api/v1/me/diary/:id`.
 *
 * Every field is optional so callers can patch only what changed.
 */
export interface UpdateDiaryEntryRequest {
  mealType?: MealType;
  quantity?: number;
  servingSizeId?: string;
}

/**
 * Aggregated nutrition for a day, with optional goal targets for comparison.
 */
export interface DailySummary {
  date: string;
  totals: EntryNutrition;
  goals?: {
    calories?: number;
    proteinGrams?: number;
    carbohydrateGrams?: number;
    fatGrams?: number;
  };
}

/**
 * Fetches the diary for the given day (ISO `YYYY-MM-DD`), grouped by meal.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getDiary(date: string): Promise<DiaryResponse> {
  return apiFetch<DiaryResponse>(`/api/v1/me/diary?date=${encodeURIComponent(date)}`);
}

/**
 * Logs a new food entry.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages.
 */
export async function addDiaryEntry(req: AddDiaryEntryRequest): Promise<DiaryEntry> {
  return apiFetch<DiaryEntry>('/api/v1/me/diary', {
    method: 'POST',
    body: JSON.stringify(req),
  });
}

/**
 * Updates an existing diary entry.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages.
 */
export async function updateDiaryEntry(
  id: string,
  req: UpdateDiaryEntryRequest,
): Promise<DiaryEntry> {
  return apiFetch<DiaryEntry>(`/api/v1/me/diary/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}

/**
 * Deletes a diary entry. Resolves on the backend's 204 No Content.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function deleteDiaryEntry(id: string): Promise<void> {
  return apiFetch<void>(`/api/v1/me/diary/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}

/**
 * Fetches the aggregated daily summary (totals + goals) for the given day.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getDailySummary(date: string): Promise<DailySummary> {
  return apiFetch<DailySummary>(`/api/v1/me/summary?date=${encodeURIComponent(date)}`);
}

/**
 * Scales a food's base nutrition by a quantity multiplier.
 *
 * Calories are rounded to the nearest integer; every other present field is
 * rounded to one decimal place. Fields absent from `base` are omitted from the
 * result (no spurious zeros). Calories are always present, defaulting to 0.
 *
 * @param base - Per-serving nutrition to scale.
 * @param quantity - Multiplier (e.g. 1.5 servings).
 */
export function scaleNutrition(base: FoodNutrition, quantity: number): EntryNutrition {
  const round1 = (value: number): number => Math.round(value * 10) / 10;

  const scaled: EntryNutrition = {
    calories: Math.round((base.calories ?? 0) * quantity),
  };

  if (base.proteinGrams !== undefined) scaled.proteinGrams = round1(base.proteinGrams * quantity);
  if (base.carbohydrateGrams !== undefined)
    scaled.carbohydrateGrams = round1(base.carbohydrateGrams * quantity);
  if (base.fatGrams !== undefined) scaled.fatGrams = round1(base.fatGrams * quantity);
  if (base.fibreGrams !== undefined) scaled.fibreGrams = round1(base.fibreGrams * quantity);
  if (base.sugarGrams !== undefined) scaled.sugarGrams = round1(base.sugarGrams * quantity);
  if (base.sodiumMg !== undefined) scaled.sodiumMg = round1(base.sodiumMg * quantity);

  return scaled;
}
