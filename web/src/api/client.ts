import { API_BASE_URL } from '../env';

/**
 * Error thrown when the API responds with a non-2xx status.
 *
 * Carries the HTTP `status` so callers can branch on it (e.g. 401 → sign out,
 * 404 → not found) without parsing the message string.
 */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

/**
 * Supplies the current auth token (or null when unauthenticated).
 *
 * May be async so the auth layer can refresh an expired token before the
 * request is made.
 */
export type TokenProvider = () => string | null | Promise<string | null>;

let tokenProvider: TokenProvider = () => null;

/**
 * Registers the function used to obtain the auth token for outgoing requests.
 *
 * Called once by the auth layer at startup. Until then, requests are sent
 * without an Authorization header.
 */
export function setTokenProvider(provider: TokenProvider): void {
  tokenProvider = provider;
}

/**
 * Performs a JSON fetch against the backend API.
 *
 * - Resolves the auth token via the registered {@link TokenProvider} and adds
 *   an `Authorization: Bearer <token>` header when present.
 * - Prefixes `path` with {@link API_BASE_URL} (empty in dev, so the Vite proxy
 *   handles `/api/*`).
 * - Throws {@link ApiError} on a non-2xx response.
 *
 * @typeParam T - The expected shape of the parsed JSON response.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = await tokenProvider();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init.headers as Record<string, string> | undefined),
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${API_BASE_URL}${path}`;
  const response = await fetch(url, { ...init, headers });

  if (!response.ok) {
    throw new ApiError(response.status, `Request to ${path} failed with status ${response.status}`);
  }

  return (await response.json()) as T;
}
