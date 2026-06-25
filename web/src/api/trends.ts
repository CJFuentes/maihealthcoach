import { apiFetch } from './client';

/**
 * A single dense daily data point.
 *
 * The trends endpoint returns dense series: there is exactly one point for every
 * day in the requested `[from, to]` window, in chronological order, with `value`
 * zero-filled on days with no data. Because the series is dense, a point's array
 * index equals its day offset from `from`, so consumers may position by index.
 */
export interface DailyPoint {
  date: string;
  value: number;
}

/**
 * A single body-weight measurement.
 *
 * Unlike {@link DailyPoint}, the weight series is SPARSE: it contains only the
 * days the user actually weighed in, so the array index is meaningless. Position
 * each point by its `date` within the window — a missing day means "not
 * measured", which is NOT the same as zero, so callers must never zero-fill it.
 */
export interface WeightPoint {
  date: string;
  weightKg: number;
}

/**
 * The trends payload returned by
 * `GET /api/v1/me/trends?from=&to=` (or `?range=`).
 *
 * `from`/`to` are the resolved (inclusive) window bounds. The four calorie/water
 * series are dense {@link DailyPoint} arrays (one entry per day, zero-filled);
 * `weight` is a sparse {@link WeightPoint} array (measured days only).
 */
export interface TrendsResponse {
  from: string;
  to: string;
  caloriesConsumed: DailyPoint[];
  caloriesBurned: DailyPoint[];
  netCalories: DailyPoint[];
  waterMl: DailyPoint[];
  weight: WeightPoint[];
}

/** Supported preset window lengths, in days, for the `range` query parameter. */
export type TrendsRange = 7 | 30 | 90;

/**
 * Fetches the trends payload: dense daily calorie/water series plus the sparse
 * body-weight series over a window.
 *
 * The window may be specified three ways:
 * - a {@link TrendsRange} number → `?range=<n>` (last N days, server-resolved);
 * - an explicit `{ from, to }` object → `?from=<from>&to=<to>` (both inclusive,
 *   ISO `YYYY-MM-DD`);
 * - omitted → no query string, letting the server apply its default window.
 *
 * Throws {@link ApiError} on a non-2xx response.
 */
export async function getTrends(
  params?: TrendsRange | { from: string; to: string },
): Promise<TrendsResponse> {
  let qs = '';
  if (typeof params === 'number') {
    qs = `?range=${params}`;
  } else if (params) {
    qs = `?from=${encodeURIComponent(params.from)}&to=${encodeURIComponent(params.to)}`;
  }

  return apiFetch<TrendsResponse>(`/api/v1/me/trends${qs}`);
}
