import { Link } from "react-router-dom";

/** Shown when a signed-in user opens a page their role does not include (F1). */
export function NoAccessPage() {
  return (
    <div className="card" style={{ maxWidth: 520, margin: "48px auto", padding: 32, textAlign: "center" }}>
      <div style={{ fontSize: 40, marginBottom: 8 }} aria-hidden="true">
        🔒
      </div>
      <h1 style={{ fontSize: 19, margin: "0 0 8px" }}>You don’t have access to this page</h1>
      <p style={{ color: "var(--muted)", margin: "0 0 20px" }}>
        Your role does not include it. If you need it for your work, ask an administrator to
        update your role.
      </p>
      <Link className="btn btn--primary" to="/dashboard">
        Go to the dashboard
      </Link>
    </div>
  );
}
