import { LayoutDashboard } from "lucide-react";
import { useAuth } from "../../../store/AuthContext";
import { PageHero } from "@/components/PageHero";
import { EmptyState } from "@/components/EmptyState";

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div>
      <PageHero
        title={`Welcome, ${user?.username ?? ""}`}
        subtitle={`Role: ${user?.roles.join(", ") ?? ""}`}
      />
      <div className="card">
        <EmptyState
          icon={LayoutDashboard}
          title="Dashboard coming soon"
          message="The per-role dashboard module hasn't been built yet. Use the menu to open a module."
        />
      </div>
    </div>
  );
}
