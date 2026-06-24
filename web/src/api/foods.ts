import { apiFetch } from './client';

/**
 * Nutrition figures for a food or serving.
 *
 * `calories` is always present; macro and micronutrient fields are optional
 * because the underlying food database does not provide every value for every
 * item.
 */
export interface FoodNutrition {
  calories: number;
  proteinGrams?: number;
  carbohydrateGrams?: number;
  fatGrams?: number;
  fibreGrams?: number;
  sugarGrams?: number;
  sodiumMg?: number;
}

/**
 * A selectable portion of a food (e.g. "1 cup", "100 g").
 *
 * `weightGrams` is the portion's mass when known; `nutrition` is the food's
 * nutrition for one of this serving.
 */
export interface ServingSize {
  id: string;
  label: string;
  weightGrams?: number;
  nutrition: FoodNutrition;
}

/**
 * A food in the database, with its base nutrition and available serving sizes.
 *
 * `nutrition` is the per-default-serving baseline; `servingSizes` lists every
 * portion the user can pick. `defaultServingSizeId` points at the preferred
 * entry in `servingSizes` when set.
 */
export interface Food {
  id: string;
  name: string;
  brand?: string;
  barcode?: string;
  nutrition: FoodNutrition;
  servingSizes: ServingSize[];
  defaultServingSizeId?: string;
  isFavourite?: boolean;
  isCustom?: boolean;
  createdAt?: string;
}

/**
 * Paged result of `GET /api/v1/foods`.
 *
 * `items` is the current page of matches; `total`/`page`/`pageSize` are
 * pagination metadata when the backend supplies them.
 */
export interface FoodSearchResponse {
  items: Food[];
  total?: number;
  page?: number;
  pageSize?: number;
}

/**
 * Searches the food database for the given query.
 *
 * Throws {@link ApiError} on a non-2xx response so the caller can surface the
 * failure to the user.
 *
 * @param query - The free-text search term.
 * @param page - 1-based page number (defaults to the first page).
 */
export async function searchFoods(query: string, page = 1): Promise<FoodSearchResponse> {
  return apiFetch<FoodSearchResponse>(
    `/api/v1/foods?q=${encodeURIComponent(query)}&page=${page}`,
  );
}

/**
 * Fetches a single food by id, including its serving sizes.
 *
 * Throws {@link ApiError} with status 404 when the food no longer exists.
 */
export async function getFood(id: string): Promise<Food> {
  return apiFetch<Food>(`/api/v1/foods/${encodeURIComponent(id)}`);
}
