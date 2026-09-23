import { apiClient } from "../services/apiClient";
import type {
  LoginRequest,
  AuthResult,
} from "../features/authentication/types/auth.types";

export const authService = {
  login: (request: LoginRequest) =>
    apiClient.post<AuthResult>("/auth/login", request).then((res) => res.data),
};
