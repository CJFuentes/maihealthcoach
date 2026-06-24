import { apiFetch } from './client';
import type { NutritionPer100g } from './foods';

/** Which meal a diary entry belongs to. */
export type MealType = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack';

/**
 * Nutrition for a logged diary entry (calories required, the rest optional).
 *
 * This is the diary/summary projection — distinct from a food's
 * {@link NutritionPer100g}, which is the raw per-100 g reference the diary
 * scales from. The macro fields use the diary backend's `…Grams` naming.
 */
export interface EntryNutrition {
  calories: number;
  proteinGrams?: number;
  carbohydrateGrams?: number;
  fatGrams?: number;
}

/**
 * A single logged food in the diary.
 *
 * `nutrition` is the scaled nutrition for the chosen quantity/serving and may
 * be absent when the backend has not computed it; `quantity` is the number of
 * servings of `servingLabel` (or of 100 g when no named serving was chosen).
 */
export interface DiaryEntry {
  id: string;
  foodId: string;
  foodName: string;
  brand?: string;
  mealType: MealType;
  date: string;
  quantity: number;
  servingLabel?: string;
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

/**
 * Body for `POST /api/v1/me/diary` to log a new entry.
 *
 * `servingLabel` is the label of the chosen {@link import('./foods').ServingSize}
 * (foods identify servings by label/grams, not by id); omit it to log a raw
 * 100 g quantity.
 */
export interface AddDiaryEntryRequest {
  foodId: string;
  mealType: MealType;
  date: string;
  quantity: number;
  servingLabel?: string;
}

/**
 * Body for `PUT /api/v1/me/diary/:id`.
 *
 * Every field is optional so callers can patch only what changed.
 */
export interface UpdateDiaryEntryRequest {
  mealType?: MealType;
  quantity?: number;
  servingLabel?: string;
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
 * Computes the {@link EntryNutrition} for a portion of a food.
 *
 * A food's {@link NutritionPer100g} is scaled by the total grams consumed —
 * `gramsPerServing × quantity` — so a 1.5 × "1 pot" (170 g) portion of a food
 * uses 255 g of its per-100 g figures. Calories are rounded to the nearest
 * integer; macros to one decimal place. Optional macros absent from `per100g`
 * are omitted (no spurious zeros).
 *
 * @param per100g - The food's reference nutrition per 100 g.
 * @param gramsPerServing - Weight in grams of one serving (use 100 for a raw
 *   "per 100 g" portion).
 * @param quantity - Number of servings (e.g. 1.5).
 */
export function scaleNutrition(
  per100g: NutritionPer100g,
  gramsPerServing: number,
  quantity: number,
): EntryNutrition {
  const round1 = (value: number): number => Math.round(value * 10) / 10;
  const factor = (gramsPerServing * quantity) / 100;

  const scaled: EntryNutrition = {
    calories: Math.round(per100g.energyKcal * factor),
    proteinGrams: round1(per100g.proteinG * factor),
    carbohydrateGrams: round1(per100g.carbohydrateG * factor),
    fatGrams: round1(per100g.fatG * factor),
  };

  return scaled;
}
