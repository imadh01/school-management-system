import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import type { AuthResult } from "../features/authentication/types/auth.types";

interface AuthContextValue {
  user: AuthResult | null;
  isAuthenticated: boolean;
  login: (result: AuthResult) => void;
  logout: () => void;
}

const STORAGE_KEY = "auth_session";

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthResult | null>(null);

  // Restore session on page refresh
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      setUser(JSON.parse(stored) as AuthResult);
    }
  }, []);

  const login = (result: AuthResult) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(result));
    setUser(result);
  };

  const logout = () => {
    localStorage.removeItem(STORAGE_KEY);
    setUser(null);
  };

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: !!user, login, logout }}
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
