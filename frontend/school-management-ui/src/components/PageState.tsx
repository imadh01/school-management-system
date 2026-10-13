import { Link } from "react-router-dom";
import { UserX } from "lucide-react";
import { EmptyState } from "./EmptyState";

/** The whole-page "Loading…" card, announced to screen readers. */
export function PageLoading() {
  return (
    <div className="card">
      <div className="empty-state" role="status">
        Loading…
      </div>
    </div>
  );
}

/** The whole-page "could not open this record" card with a way back. */
export function PageLoadError({
  title,
  message,
  backTo,
  backLabel,
}: {
  title: string;
  message: string;
  backTo: string;
  backLabel: string;
}) {
  return (
    <div className="card">
      <EmptyState
        icon={UserX}
        title={title}
        message={message}
        action={
          <Link className="btn btn--secondary" to={backTo}>
            {backLabel}
          </Link>
        }
      />
    </div>
  );
}
