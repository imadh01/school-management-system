export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

/** Body of /auth/login, /auth/refresh and /auth/change-password. The refresh token is never in it (httpOnly cookie). */
export interface AuthResult {
  userId: number;
  username: string;
  email: string;
  roles: string[];
  token: string;
  expiresAtUtc: string;
  mustChangePassword: boolean;
}

/** The signed-in user as the UI sees it: everything except the access token itself. */
export type CurrentUser = Omit<AuthResult, "token" | "expiresAtUtc">;
