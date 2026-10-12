import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type {
  AuthResult,
  CurrentUser,
  LoginRequest,
} from "../features/authentication/types/auth.types";
import { authService } from "../features/authentication/services/authService";
import {
  restoreSession,
  subscribeToSession,
} from "../features/authentication/services/session";
import { subscribeToForbidden } from "../services/accessEvents";

/** "loading" only while the app checks the refresh cookie on start-up. */
export type AuthStatus = "loading" | "authenticated" | "anonymous";

interface AuthContextValue {
  user: CurrentUser | null;
  status: AuthStatus;
  isAuthenticated: boolean;
  /**
   * F1: the signed-in user's permissions from /auth/me, in memory only.
   * null while they are being loaded (show a loading state, not "no access").
   */
  permissions: ReadonlySet<string> | null;
  /** True when the user holds at least one of the names. Cosmetic: the API decides. */
  hasAnyPermission: (names: readonly string[]) => boolean;
  login: (request: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
}

interface AuthState {
  status: AuthStatus;
  user: CurrentUser | null;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/** A 403 reloads permissions, but not more often than this (a legitimate 403 must not cause a loop). */
const FORBIDDEN_RELOAD_GAP_MS = 5_000;

function toState(session: AuthResult | null): AuthState {
  if (!session) return { status: "anonymous", user: null };

  // Keep the access token out of React state: it lives only in session.ts.
  const { token: _token, expiresAtUtc: _expiresAtUtc, ...user } = session;
  return { status: "authenticated", user };
}

// D2: nothing is stored in localStorage any more. On start-up the session is
// restored from the httpOnly refresh cookie; until that answer arrives the
// status is "loading", so ProtectedRoute waits instead of redirecting to /login.
//
// F1: after every login or session restore the permissions are loaded from
// /auth/me (the token carries none). They are reloaded when the API answers 403,
// because an admin may have changed this user's role in the meantime.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    status: "loading",
    user: null,
  });
  const [permissions, setPermissions] = useState<ReadonlySet<string> | null>(
    null,
  );

  const loadedForUserId = useRef<number | null>(null);
  const lastLoadAt = useRef(0);
  const inFlight = useRef<{ userId: number; done: Promise<void> } | null>(
    null,
  );

  const loadPermissions = useCallback((userId: number): Promise<void> => {
    // One request at a time per user; a different user always gets their own.
    if (inFlight.current?.userId === userId) return inFlight.current.done;

    const done = authService
      .me()
      .then((me) => {
        if (loadedForUserId.current !== userId) return; // signed out/in meanwhile
        setPermissions(new Set(me.permissions));
      })
      .catch(() => {
        // Could not load them: show "no access" rather than a page that will fail.
        // The API still decides; the next 403 or sign-in tries again.
        if (loadedForUserId.current === userId) setPermissions(new Set());
      })
      .finally(() => {
        lastLoadAt.current = Date.now();
        if (inFlight.current?.done === done) inFlight.current = null;
      });
    inFlight.current = { userId, done };
    return done;
  }, []);

  useEffect(() => {
    // Every login, refresh, logout (in this tab or another) lands here.
    const unsubscribe = subscribeToSession((session) => {
      const next = toState(session);
      setState(next);

      const userId = next.user?.userId ?? null;
      if (userId === null) {
        loadedForUserId.current = null;
        setPermissions(null);
      } else if (userId !== loadedForUserId.current) {
        // A different person (or the first restore): their permissions, not the last user's.
        loadedForUserId.current = userId;
        setPermissions(null);
        void loadPermissions(userId);
      }
      // Same user, new access token (the 15-minute refresh): keep what is loaded.
    });

    const unsubscribeForbidden = subscribeToForbidden(() => {
      const userId = loadedForUserId.current;
      if (
        userId !== null &&
        Date.now() - lastLoadAt.current > FORBIDDEN_RELOAD_GAP_MS
      ) {
        void loadPermissions(userId);
      }
    });

    // If the server could not be reached, stop "loading" and show the login page.
    void restoreSession().then((session) => {
      if (!session) {
        setState((current) =>
          current.status === "loading" ? toState(null) : current,
        );
      }
    });

    return () => {
      unsubscribe();
      unsubscribeForbidden();
    };
  }, [loadPermissions]);

  const hasAnyPermission = useCallback(
    (names: readonly string[]) =>
      permissions !== null && names.some((name) => permissions.has(name)),
    [permissions],
  );

  const login = async (request: LoginRequest) => {
    await authService.login(request);
  };

  const logout = async () => {
    await authService.logout();
  };

  return (
    <AuthContext.Provider
      value={{
        user: state.user,
        status: state.status,
        isAuthenticated: state.status === "authenticated",
        permissions,
        hasAnyPermission,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
}
