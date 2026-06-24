import { API_BASE_URL } from '../env';

/**
 * RFC 7807 ProblemDetails body returned by the backend on validation failures.
 *
 * `errors` maps each invalid field (camelCase, matching the request body key)
 * to its list of human-readable messages, so callers can surface them inline.
 */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/**
 * Error thrown when the API responds with a non-2xx status.
 *
 * Carries the HTTP `status` so callers can branch on it (e.g. 401 → sign out,
 * 404 → not found) without parsing the message string. When the response body
 * is a parseable JSON object it is exposed as {@link ProblemDetails} via
 * `problem`, so callers can read `err.problem?.errors` to render field-level
 * validation messages.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
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
 * - Throws {@link ApiError} on a non-2xx response, attaching any parsed
 *   {@link ProblemDetails} body so callers can read field-level validation
 *   errors via `err.problem?.errors`.
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
    // Some error responses have an empty or non-JSON body (e.g. 401/502), so
    // parsing is best-effort and must never mask the original HTTP error.
    let problem: ProblemDetails | undefined;
    try {
      const body: unknown = await response.json();
      if (body && typeof body === 'object') {
        problem = body as ProblemDetails;
      }
    } catch {
      problem = undefined;
    }

    throw new ApiError(
      response.status,
      `Request to ${path} failed with status ${response.status}`,
      problem,
    );
  }

  // 204 No Content / 205 Reset Content have no body to parse. DELETE endpoints
  // in particular return 204; calling response.json() on them would throw.
  if (response.status === 204 || response.status === 205) {
    return undefined as T;
  }

  return (await response.json()) as T;
}
