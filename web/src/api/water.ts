import { apiFetch } from './client';

/**
 * A single logged water-intake entry.
 *
 * `amountMl` is the volume consumed in millilitres; `loggedAt` is the ISO 8601
 * timestamp the backend recorded the entry at.
 */
export interface WaterEntry {
  id: string;
  amountMl: number;
  loggedAt: string;
}

/**
 * The water log for a single day, returned by
 * `GET /api/v1/me/water?date=YYYY-MM-DD`.
 *
 * The aggregate fields (`totalMl`, `goalMl`, `remainingMl`) are optional so the
 * page can fall back to client-side computation when the backend omits them
 * (mirrors the best-effort summary fallback in the diary feature). `goalMl`
 * reflects the user's profile-derived daily hydration goal; `remainingMl` is
 * `goalMl - totalMl` clamped at zero.
 */
export interface WaterDayResponse {
  date: string;
  entries: WaterEntry[];
  totalMl?: number;
  goalMl?: number;
  remainingMl?: number;
}

/**
 * Body for `POST /api/v1/me/water` to quick-add a water entry.
 */
export interface AddWaterEntryRequest {
  amountMl: number;
}

/**
 * Body for `PUT /api/v1/me/water/:id` to edit an entry's amount.
 */
export interface UpdateWaterEntryRequest {
  amountMl: number;
}

/**
 * Fetches the water log for the given day (ISO `YYYY-MM-DD`): entries plus the
 * day's total consumed, daily goal, and remaining volume.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getWaterDay(date: string): Promise<WaterDayResponse> {
  return apiFetch<WaterDayResponse>(`/api/v1/me/water?date=${encodeURIComponent(date)}`);
}

/**
 * Logs a new water entry (quick-add).
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages via `err.problem?.errors`.
 */
export async function addWaterEntry(req: AddWaterEntryRequest): Promise<WaterEntry> {
  return apiFetch<WaterEntry>('/api/v1/me/water', {
    method: 'POST',
    body: JSON.stringify(req),
  });
}

/**
 * Updates an existing water entry's amount.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages via `err.problem?.errors`.
 */
export async function updateWaterEntry(
  id: string,
  req: UpdateWaterEntryRequest,
): Promise<WaterEntry> {
  return apiFetch<WaterEntry>(`/api/v1/me/water/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}

/**
 * Deletes a water entry. Resolves on the backend's 204 No Content.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function deleteWaterEntry(id: string): Promise<void> {
  return apiFetch<void>(`/api/v1/me/water/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}
