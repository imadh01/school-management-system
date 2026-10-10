import {
  createContext,
  useContext,
  useEffect,
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

/** "loading" only while the app checks the refresh cookie on start-up. */
export type AuthStatus = "loading" | "authenticated" | "anonymous";

interface AuthContextValue {
  user: CurrentUser | null;
  status: AuthStatus;
  isAuthenticated: boolean;
  login: (request: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
}

interface AuthState {
  status: AuthStatus;
  user: CurrentUser | null;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function toState(session: AuthResult | null): AuthState {
  if (!session) return { status: "anonymous", user: null };

  // Keep the access token out of React state: it lives only in session.ts.
  const { token: _token, expiresAtUtc: _expiresAtUtc, ...user } = session;
  return { status: "authenticated", user };
}

// D2: nothing is stored in localStorage any more. On start-up the session is
// restored from the httpOnly refresh cookie; until that answer arrives the
// status is "loading", so ProtectedRoute waits instead of redirecting to /login.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    status: "loading",
    user: null,
  });

  useEffect(() => {
    // Every login, refresh, logout (in this tab or another) lands here.
    const unsubscribe = subscribeToSession((session) =>
      setState(toState(session)),
    );

    // If the server could not be reached, stop "loading" and show the login page.
    void restoreSession().then((session) => {
      if (!session) {
        setState((current) =>
          current.status === "loading" ? toState(null) : current,
        );
      }
    });

    return unsubscribe;
  }, []);

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
