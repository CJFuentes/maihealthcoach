import { apiFetch } from './client';

/**
 * A single exercise activity from the catalog
 * (`GET /api/v1/exercises?q=&category=`).
 *
 * `met` is the Metabolic Equivalent of Task value the backend uses (together
 * with the user's body weight and the logged duration) to estimate calories
 * burned; `category` groups the activity (e.g. "Cardio", "Strength").
 */
export interface Exercise {
  id: string;
  name: string;
  category: string;
  met: number;
}

/**
 * A page of exercise-catalog matches from `GET /api/v1/exercises?q=`.
 *
 * The backend may respond with a bare array or this `{ items }` envelope; the
 * API module normalises both shapes to this interface so callers only consume
 * `items`.
 */
export interface ExerciseSearchResponse {
  items: Exercise[];
}

/**
 * A single logged exercise entry.
 *
 * `caloriesBurned` is the backend-computed energy expenditure (derived from the
 * activity MET, the user's body weight, and `durationMinutes`); `loggedAt` is
 * the ISO 8601 timestamp the backend recorded the entry at, and `date` is the
 * local `YYYY-MM-DD` the entry belongs to.
 */
export interface ExerciseEntry {
  id: string;
  activityId: string;
  activityName: string;
  category: string;
  durationMinutes: number;
  caloriesBurned: number;
  loggedAt: string;
  date: string;
}

/**
 * The exercise log for a single day, returned by
 * `GET /api/v1/me/exercise?date=YYYY-MM-DD`.
 *
 * `totalCaloriesBurned` is optional so the page can fall back to client-side
 * computation when the backend omits it (mirrors the best-effort summary
 * fallback in the water and diary features).
 */
export interface ExerciseDayResponse {
  date: string;
  entries: ExerciseEntry[];
  totalCaloriesBurned?: number;
}

/**
 * Body for `POST /api/v1/me/exercise` to log an exercise entry.
 */
export interface LogExerciseRequest {
  activityId: string;
  durationMinutes: number;
  date: string;
}

/**
 * Body for `PUT /api/v1/me/exercise/:id` to edit an entry's duration.
 */
export interface UpdateExerciseEntryRequest {
  durationMinutes: number;
}

/**
 * Searches the exercise catalog by free-text query (and optional category).
 *
 * The backend may return either a bare array or an `{ items }` envelope; both
 * are normalised to {@link ExerciseSearchResponse} so callers only deal with
 * `items`. The query (and category, when given) are URL-encoded.
 *
 * Throws {@link ApiError} on a non-2xx status so the caller can surface an
 * error state.
 */
export async function searchExercises(
  q: string,
  category?: string,
): Promise<ExerciseSearchResponse> {
  const raw = await apiFetch<Exercise[] | ExerciseSearchResponse>(
    `/api/v1/exercises?q=${encodeURIComponent(q)}${category ? `&category=${encodeURIComponent(category)}` : ''}`,
  );
  const extracted = Array.isArray(raw) ? raw : raw?.items;
  const items = Array.isArray(extracted) ? extracted : [];
  return { items };
}

/**
 * Fetches the exercise log for the given day (ISO `YYYY-MM-DD`): entries plus
 * the day's total calories burned.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getExerciseDay(date: string): Promise<ExerciseDayResponse> {
  return apiFetch<ExerciseDayResponse>(
    `/api/v1/me/exercise?date=${encodeURIComponent(date)}`,
  );
}

/**
 * Logs a new exercise entry.
 *
 * Throws {@link ApiError} on a non-2xx response. A 422 (carrying ProblemDetails)
 * indicates the user has no body weight set, so the caller can prompt them to
 * complete their profile; a 400 carries field-level validation messages via
 * `err.problem?.errors`.
 */
export async function logExercise(req: LogExerciseRequest): Promise<ExerciseEntry> {
  return apiFetch<ExerciseEntry>('/api/v1/me/exercise', {
    method: 'POST',
    body: JSON.stringify(req),
  });
}

/**
 * Updates an existing exercise entry's duration.
 *
 * Throws {@link ApiError} (carrying ProblemDetails on a 400) so the caller can
 * surface field-level validation messages via `err.problem?.errors`.
 */
export async function updateExerciseEntry(
  id: string,
  req: UpdateExerciseEntryRequest,
): Promise<ExerciseEntry> {
  return apiFetch<ExerciseEntry>(`/api/v1/me/exercise/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}

/**
 * Deletes an exercise entry. Resolves on the backend's 204 No Content.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function deleteExerciseEntry(id: string): Promise<void> {
  return apiFetch<void>(`/api/v1/me/exercise/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}
