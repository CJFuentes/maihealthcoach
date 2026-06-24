import { apiFetch } from './client';

/**
 * Shape of the backend health-check response returned by `/api/v1/ping`.
 */
export interface PingResponse {
  service: string;
  version: string;
  timestamp: string;
}

/**
 * Calls the backend health-check endpoint.
 *
 * Used by the home page to confirm the API is reachable.
 */
export async function ping(): Promise<PingResponse> {
  return apiFetch<PingResponse>('/api/v1/ping');
}
