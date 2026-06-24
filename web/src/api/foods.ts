import { apiFetch, ApiError } from './client';

/**
 * Macro- and micro-nutrient values for a food, expressed per 100 g.
 *
 * `energyKcal` and the three macros are always present; additional micros
 * (fibre, sugars, sodium, …) are optional and keyed by their backend name.
 */
export interface NutritionPer100g {
  energyKcal: number;
  proteinG: number;
  carbohydrateG: number;
  fatG: number;
  fiberG?: number;
  sugarsG?: number;
  saturatedFatG?: number;
  sodiumMg?: number;
  saltG?: number;
}

/** A named serving size and its weight in grams (e.g. "1 can" = 330 g). */
export interface ServingSize {
  label: string;
  grams: number;
}

/**
 * A food as returned by the backend food endpoints
 * (`GET /api/v1/foods/barcode/{code}`, `GET /api/v1/foods?q=` and
 * `GET /api/v1/foods/{id}`).
 *
 * `source` indicates the origin of the record (e.g. "OpenFoodFacts" or
 * "Custom"). `barcode` is null for foods that were not matched by code.
 */
export interface FoodDto {
  id: string;
  name: string;
  brand: string | null;
  barcode: string | null;
  source: string;
  nutritionPer100g: NutritionPer100g;
  servingSizes: ServingSize[];
}

/**
 * A single page of food-search results from `GET /api/v1/foods?q=`.
 *
 * `items` is the page of matches; the optional paging fields are populated when
 * the backend returns them (the diary UI only consumes `items` today).
 */
export interface FoodSearchResponse {
  items: FoodDto[];
  total?: number;
  page?: number;
  pageSize?: number;
}

/**
 * Thrown by {@link lookupBarcode} when the upstream Open Food Facts service is
 * unavailable (HTTP 503). Distinct from a generic {@link ApiError} so the UI can
 * render a dedicated "service unavailable, try again later" state rather than a
 * generic failure.
 */
export class FoodServiceUnavailableError extends Error {
  constructor(message = 'The food lookup service is temporarily unavailable.') {
    super(message);
    this.name = 'FoodServiceUnavailableError';
  }
}

/**
 * Normalises a raw barcode string for the lookup request.
 *
 * Strips whitespace and any non-digit characters (scanners and manual entry can
 * include spaces or stray separators). EAN/UPC barcodes are purely numeric.
 */
export function normalizeBarcode(raw: string): string {
  return raw.replace(/\D/g, '');
}

/**
 * Looks up a packaged food by its EAN/UPC barcode.
 *
 * - Returns the parsed {@link FoodDto} on 200.
 * - Returns `null` when the backend responds 404 (no product matched the
 *   barcode) so the UI can offer to create a custom food.
 * - Throws {@link FoodServiceUnavailableError} on 503 (Open Food Facts upstream
 *   unavailable) so the UI can show a distinct retry state.
 * - Re-throws {@link ApiError} for any other non-2xx status.
 *
 * @param code - The barcode digits (already-decoded EAN/UPC). Non-digit
 *   characters are stripped before the request is made.
 */
export async function lookupBarcode(code: string): Promise<FoodDto | null> {
  const normalized = normalizeBarcode(code);

  try {
    return await apiFetch<FoodDto>(`/api/v1/foods/barcode/${encodeURIComponent(normalized)}`);
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.status === 404) {
        return null;
      }
      if (error.status === 503) {
        throw new FoodServiceUnavailableError();
      }
    }
    throw error;
  }
}

/**
 * Searches the food database by free-text query (`GET /api/v1/foods?q=`).
 *
 * Returns a {@link FoodSearchResponse}; throws {@link ApiError} on a non-2xx
 * status so the caller can surface an error state. The query is URL-encoded.
 */
export async function searchFoods(query: string, page = 1): Promise<FoodSearchResponse> {
  return apiFetch<FoodSearchResponse>(
    `/api/v1/foods?q=${encodeURIComponent(query)}&page=${page}`,
  );
}

/**
 * Fetches a single food by id (`GET /api/v1/foods/{id}`).
 *
 * Used by the diary edit flow to re-hydrate the full food (and its serving
 * sizes) for an existing entry. Throws {@link ApiError} on a non-2xx status.
 */
export async function getFood(id: string): Promise<FoodDto> {
  return apiFetch<FoodDto>(`/api/v1/foods/${encodeURIComponent(id)}`);
}
