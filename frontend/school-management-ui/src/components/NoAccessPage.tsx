import { Link } from "react-router-dom";
import { Lock } from "lucide-react";
import { EmptyState } from "./EmptyState";

/** Shown when a signed-in user opens a page their role does not include (F1). */
export function NoAccessPage() {
  return (
    <div className="card" style={{ maxWidth: 560, margin: "48px auto" }}>
      <h1 className="visually-hidden">No access</h1>
      <EmptyState
        icon={Lock}
        title="You don’t have access to this page"
        message="Your role does not include it. If you need it for your work, ask an administrator to update your role."
        action={
          <Link className="btn btn--primary" to="/dashboard">
            Go to the dashboard
          </Link>
        }
      />
    </div>
  );
}
