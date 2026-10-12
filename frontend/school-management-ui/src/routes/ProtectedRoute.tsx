import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../store/AuthContext";

export const CHANGE_PASSWORD_PATH = "/change-password";

function Loading() {
  return (
    <div role="status" aria-live="polite" style={{ padding: "2rem" }}>
      Loading…
    </div>
  );
}

/**
 * Signed-in pages. In order:
 *   1. wait while the session is restored (D2) and the permissions load (F1);
 *   2. not signed in → /login;
 *   3. a password change is required → the change-password page, before anything else
 *      (the API refuses every other call until then anyway). Others may open that page too.
 * Per-page permission checks are RequirePermission's job.
 */
export function ProtectedRoute() {
  const { status, user, permissions } = useAuth();
  const location = useLocation();

  if (status === "loading") return <Loading />;
  if (status !== "authenticated" || !user)
    return <Navigate to="/login" replace />;
  if (permissions === null) return <Loading />;

  const onChangePassword = location.pathname === CHANGE_PASSWORD_PATH;
  if (user.mustChangePassword && !onChangePassword) {
    return <Navigate to={CHANGE_PASSWORD_PATH} replace />;
  }

  return <Outlet />;
}
