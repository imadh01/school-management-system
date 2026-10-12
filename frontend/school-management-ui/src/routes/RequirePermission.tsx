import type { ReactNode } from "react";
import { useAuth } from "../store/AuthContext";
import { NoAccessPage } from "../components/NoAccessPage";

interface Props {
  /** Any one of these opens the page (e.g. Students.View or Students.ViewScoped). */
  anyOf: readonly string[] | undefined;
  children: ReactNode;
}

/**
 * F1: guards the URL, not just the menu. Hiding a sidebar link does not stop
 * someone typing /students, so the page itself checks too and shows a clear
 * "no access" page. Cosmetic: the API enforces the same permissions.
 */
export function RequirePermission({ anyOf, children }: Props) {
  const { hasAnyPermission } = useAuth();

  if (anyOf && anyOf.length > 0 && !hasAnyPermission(anyOf)) {
    return <NoAccessPage />;
  }
  return <>{children}</>;
}
