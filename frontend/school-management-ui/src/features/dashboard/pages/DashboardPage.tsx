import { useAuth } from "../../../store/AuthContext";

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div>
      <h1>Welcome, {user?.username}</h1>
      <p>Role: {user?.roles.join(", ")}</p>
      <p>
        The real Dashboard module (per-role views) hasn't been built yet — this
        is a placeholder.
      </p>
    </div>
  );
}
