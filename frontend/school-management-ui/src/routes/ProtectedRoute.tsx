import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../store/AuthContext";

export function ProtectedRoute() {
  const { status } = useAuth();

  // D2: on a page reload the session is being restored from the refresh
  // cookie; wait for that instead of bouncing a signed-in user to /login.
  if (status === "loading") {
    return (
      <div role="status" aria-live="polite" style={{ padding: "2rem" }}>
        Loading…
      </div>
    );
  }

  return status === "authenticated" ? (
    <Outlet />
  ) : (
    <Navigate to="/login" replace />
  );
}
