import { apiClient } from "../../../services/apiClient";
import { authHttp } from "../../../services/apiConfig";
import { endSession, startSession, withAuthLock } from "./session";
import type { LoginRequest, AuthResult } from "../types/auth.types";

export const authService = {
  /** Signs in; the API also sets the refresh-token cookie. */
  login: async (request: LoginRequest): Promise<AuthResult> => {
    const { data } = await authHttp.post<AuthResult>("/auth/login", request);
    startSession(data);
    return data;
  },

  /**
   * Ends this device's session. Runs under the same lock as refresh, so it never
   * sends a cookie another tab is rotating at that moment (which the server
   * would treat as token reuse). The local session ends even if the server
   * cannot be reached.
   */
  logout: async (): Promise<void> => {
    try {
      await withAuthLock(() => authHttp.post("/auth/logout"));
    } finally {
      endSession();
    }
  },

  /** "Sign out everywhere": ends every session of this user on every device. */
  logoutAll: async (): Promise<void> => {
    try {
      await apiClient.post("/auth/logout-all");
    } finally {
      endSession();
    }
  },
};
