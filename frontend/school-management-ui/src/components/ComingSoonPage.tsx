import { Link, useLocation } from "react-router-dom";
import { Hourglass } from "lucide-react";
import { findNavLabel } from "../layouts/navConfig";
import { PageHero } from "./PageHero";
import { EmptyState } from "./EmptyState";

/** A module from the prototype that is not built yet. */
export function ComingSoonPage() {
  const label = findNavLabel(useLocation().pathname) || "This page";
  return (
    <>
      <PageHero
        title={label}
        subtitle="This module is planned but not built yet."
      />
      <div className="panel">
        <EmptyState
          icon={Hourglass}
          title={`${label} is coming soon`}
          message="It will appear here when its module is built."
          action={
            <Link className="btn btn--secondary" to="/dashboard">
              Go to the dashboard
            </Link>
          }
        />
      </div>
    </>
  );
}
