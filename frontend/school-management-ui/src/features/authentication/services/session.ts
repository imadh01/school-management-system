import axios from "axios";
import { authHttp } from "../../../services/apiConfig";
import type { AuthResult } from "../types/auth.types";

/*
 * The browser side of the session (D2).
 *
 * - The ACCESS token (15 min) lives only in this module's memory. It is never
 *   written to localStorage/sessionStorage, where any injected script could read it.
 * - The REFRESH token is an httpOnly cookie that JavaScript cannot see at all.
 *   The browser sends it to /api/auth/* by itself.
 * - After a page reload the memory is empty, so the app calls /auth/refresh once
 *   (restoreSession) to get a new access token from the cookie.
 *
 * The server revokes a whole session if the same refresh token is ever used
 * twice (reuse detection), so refreshes are serialised:
 *   - within a tab: concurrent callers share one in-flight request;
 *   - across tabs: the Web Locks API lets only one tab refresh at a time.
 */

let accessToken: string | null = null;

/** The current access token, or null when signed out. */
export function getAccessToken(): string | null {
  return accessToken;
}

// ---------------------------------------------------------------- listeners

type SessionListener = (session: AuthResult | null) => void;
const listeners = new Set<SessionListener>();

/** Called with the new session after every login/refresh, and with null when the session ends. */
export function subscribeToSession(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function publish(session: AuthResult | null): void {
  accessToken = session?.token ?? null;
  listeners.forEach((listener) => listener(session));
}

// ---------------------------------------------------------------- other tabs

type TabMessage = "login" | "logout";

const channel =
  typeof BroadcastChannel !== "undefined"
    ? new BroadcastChannel("eschool-auth")
    : null;

channel?.addEventListener("message", (event: MessageEvent<TabMessage>) => {
  if (event.data === "logout") {
    publish(null); // another tab signed out: the shared cookie is gone
  } else if (event.data === "login") {
    void refreshSession().catch(() => undefined); // another tab signed in: pick up the session
  }
});

/** A new session was started in this tab (login, password change). */
export function startSession(session: AuthResult): void {
  publish(session);
  channel?.postMessage("login" satisfies TabMessage);
}

/** The session ended in this tab (logout, logout-all). Other tabs follow. */
export function endSession(): void {
  publish(null);
  channel?.postMessage("logout" satisfies TabMessage);
}

// ---------------------------------------------------------------- refresh

const REFRESH_LOCK = "eschool-auth-refresh";

/**
 * Runs `work` while holding a lock shared by every tab of this site, so two
 * tabs never send the same refresh-token cookie at the same time.
 */
export function withAuthLock<T>(work: () => Promise<T>): Promise<T> {
  if (typeof navigator !== "undefined" && navigator.locks) {
    return navigator.locks.request(REFRESH_LOCK, work);
  }
  return work(); // very old browsers: no cross-tab protection, still correct within a tab
}

let inFlight: Promise<AuthResult | null> | null = null;

/**
 * Gets a new access token using the refresh-token cookie.
 * Resolves to the new session, or null when the session has ended (the user
 * must sign in again). Rejects only when the server could not be reached.
 */
export function refreshSession(): Promise<AuthResult | null> {
  inFlight ??= withAuthLock(requestRefresh).finally(() => {
    inFlight = null;
  });
  return inFlight;
}

async function requestRefresh(): Promise<AuthResult | null> {
  try {
    const { data } = await authHttp.post<AuthResult>("/auth/refresh");
    publish(data);
    return data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response) {
      publish(null); // 401: no cookie, expired, revoked or reused
      return null;
    }
    throw error; // network failure: keep the current state, let the caller fail
  }
}

let restoring: Promise<AuthResult | null> | null = null;

/**
 * Called once when the app starts: turns the refresh-token cookie (if any)
 * into an in-memory access token, so a page reload keeps the user signed in.
 */
export function restoreSession(): Promise<AuthResult | null> {
  restoring ??= refreshSession().catch(() => null);
  return restoring;
}
