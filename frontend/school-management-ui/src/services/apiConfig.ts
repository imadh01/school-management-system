import axios from "axios";

/**
 * Same-origin by default: in development the Vite dev server proxies /api to the
 * ASP.NET Core API (see vite.config.ts); in production the API is served under
 * /api on the same host. Same origin is what lets the refresh-token cookie be
 * SameSite=Strict and lets the browser skip CORS entirely.
 */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "/api";

/**
 * Sent on every request. The API requires it on the cookie-authenticated
 * endpoints (/auth/refresh, /auth/logout) as CSRF protection: another site
 * cannot make the browser add a custom header.
 */
export const CSRF_HEADERS = { "X-Requested-With": "eschool" } as const;

/**
 * Bare client for the auth endpoints that work with the refresh-token cookie
 * (login, refresh, logout). It has no interceptors on purpose: a failing
 * refresh must never trigger another refresh.
 */
export const authHttp = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    "Content-Type": "application/json",
    ...CSRF_HEADERS,
  },
});
