/**
 * Build-time environment configuration.
 *
 * Vite only exposes variables prefixed with `VITE_` to the browser bundle.
 * `VITE_API_BASE_URL` is the absolute base URL of the backend API.
 *
 * In development it is left empty so that requests to `/api/*` are handled by
 * the Vite dev proxy (see vite.config.ts). In production it should be set to
 * the absolute backend origin (e.g. https://api.maihealthcoach.com).
 */
export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '';

/**
 * Clerk publishable key.
 *
 * In production / staging, set `VITE_CLERK_PUBLISHABLE_KEY` to the real key from
 * the Clerk dashboard (https://dashboard.clerk.com).
 *
 * The fallback is a syntactically-valid dummy key whose base64 payload decodes
 * to `dummy-clerk.clerk.accounts.dev$`. Clerk validates key *format* (not
 * validity) when `ClerkProvider` mounts, so this dummy lets the app build, run,
 * and pass tests/CI without a real Clerk account or any network call.
 * Authentication will not actually succeed with this key — it is build/test
 * scaffolding only. A real key must be supplied via env at runtime.
 *
 * `||` (not `??`) is intentional: an empty string (`VITE_CLERK_PUBLISHABLE_KEY=`
 * in `.env`) is injected by Vite as `''`, which `??` would NOT replace and Clerk
 * would then reject. `||` falls back on both `undefined` and `''`.
 */
export const CLERK_PUBLISHABLE_KEY: string =
  import.meta.env.VITE_CLERK_PUBLISHABLE_KEY ||
  'pk_test_ZHVtbXktY2xlcmsuY2xlcmsuYWNjb3VudHMuZGV2JA';
