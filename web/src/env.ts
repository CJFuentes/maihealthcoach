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
