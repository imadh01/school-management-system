import { useNavigate } from "react-router-dom";
import { useAuth } from "@/store/AuthContext";

/**
 * Placeholder until Batch E2 ships the change-password form.
 *
 * The route guard already sends a user whose MustChangePassword flag is set
 * here before anything else (the API refuses every other call for them until
 * the password is changed). E2 replaces this body with the real form, which
 * calls POST /api/auth/change-password and then startSession() with the result.
 */
export function ChangePasswordRequiredPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  return (
    <div className="card" style={{ maxWidth: 520, margin: "64px auto", padding: 32 }}>
      <h1 style={{ fontSize: 19, margin: "0 0 8px" }}>Change your password</h1>
      <p style={{ margin: "0 0 12px" }}>
        {user?.username}, your password must be changed before you can continue.
      </p>
      <p style={{ color: "var(--muted)", margin: "0 0 20px" }}>
        The change-password screen is not available yet. Please contact an administrator.
      </p>
      <button type="button" className="btn btn--secondary" onClick={handleLogout}>
        Log out
      </button>
    </div>
  );
}
