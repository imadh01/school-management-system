import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { API_BASE_URL, CSRF_HEADERS } from "./apiConfig";
import {
  getAccessToken,
  refreshSession,
} from "../features/authentication/services/session";

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
    ...CSRF_HEADERS,
  },
});

// Attach the in-memory access token to every outgoing request, if we have one.
apiClient.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

type RetriableRequest = InternalAxiosRequestConfig & { _retried?: boolean };

// A 401 usually means the 15-minute access token expired. Get a new one with
// the refresh-token cookie and repeat the request ONCE. If the refresh fails,
// the session has ended: session.ts publishes that, AuthContext switches to
// signed-out, and ProtectedRoute sends the user to /login.
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const request = error.config as RetriableRequest | undefined;

    if (error.response?.status !== 401 || !request || request._retried) {
      return Promise.reject(error);
    }

    request._retried = true;
    const session = await refreshSession().catch(() => null);
    if (!session) {
      return Promise.reject(error);
    }

    return apiClient(request); // the request interceptor attaches the new token
  },
);
