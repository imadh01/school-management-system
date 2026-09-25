export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface AuthResult {
  userId: number;
  username: string;
  email: string;
  roles: string[];
  token: string;
  expiresAtUtc: string;
}
