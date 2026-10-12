/*
 * F1: "the API just refused something" (HTTP 403).
 *
 * apiClient reports every 403 here; AuthContext listens and reloads the
 * signed-in user's permissions from /auth/me, because an admin may have changed
 * their role since they were loaded. Kept in its own module so apiClient does
 * not depend on React.
 */

type Listener = () => void;
const listeners = new Set<Listener>();

export function subscribeToForbidden(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function notifyForbidden(): void {
  listeners.forEach((listener) => listener());
}
