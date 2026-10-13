import { useAuth } from "@/store/AuthContext";

/**
 * `const can = useCan(); can(Permission.ParentsEdit)` — true when the signed-in user holds ANY of
 * the given permissions. Only for showing or hiding buttons: the API checks every request itself.
 */
export function useCan(): (...permissions: string[]) => boolean {
  const { hasAnyPermission } = useAuth();
  return (...permissions) => hasAnyPermission(permissions);
}
