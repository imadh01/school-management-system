import axios from "axios";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7100/api",
  headers: {
    "Content-Type": "application/json",
  },
});

// Attach the JWT to every outgoing request, if we have one.
apiClient.interceptors.request.use((config) => {
  const stored = localStorage.getItem("auth_session");
  if (stored) {
    const { token } = JSON.parse(stored);
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// A 401 means the token is missing/expired/invalid — clear the stale
// session and send the user back to login. No silent-refresh here yet;
// that needs backend refresh-token support we haven't built.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("auth_session");
      window.location.href = "/login";
    }
    return Promise.reject(error);
  },
);
