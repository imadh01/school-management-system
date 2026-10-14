import { useMemo } from "react";
import { CircleCheck, ShieldCheck, ShieldPlus } from "lucide-react";
import { roleService } from "../services/roleService";
import type { RoleSummary } from "../types/role.types";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import { FilterBar } from "@/components/FilterBar";
import { SearchBox } from "@/components/SearchBox";
import { StatusPills } from "@/components/StatusPills";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge } from "@/components/Badge";
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
import { PersonCell } from "@/components/PersonCell";
import { NewRecordLink, RowActions } from "@/components/ListButtons";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useFilters } from "@/hooks/useFilters";
import { Permission } from "@/routes/permissions";
import { useAuth } from "@/store/AuthContext";
import { countBy } from "@/utils/collections";

const STATUS_FILTERS = ["All", "Active", "Inactive"] as const;
const statusOf = (r: RoleSummary) => (r.isActive ? "Active" : "Inactive");

/**
 * E3c: Roles & Permissions (Administration). One row per role; a role's permissions are on its own
 * page. Roles are never deleted, only deactivated. You cannot edit a role you hold yourself.
 */
export function RolesPage() {
  const can = useCan();
  const { user: me } = useAuth();
  const canCreate = can(Permission.RolesCreate);
  const canEdit = can(Permission.RolesEdit);

  const list = useAsyncData(() => roleService.getAll(), [], "Could not load roles.");
  const roles = useMemo(() => list.data ?? [], [list.data]);
  const filters = useFilters({ status: "All", search: "" });
  const f = filters.values;

  const counts: Record<string, number> = useMemo(
    () => ({ All: roles.length, ...countBy(roles, statusOf) }),
    [roles],
  );
  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return roles.filter(
      (r) =>
        (f.status === "All" || statusOf(r) === f.status) &&
        (!q || `${r.name} ${r.description ?? ""}`.toLowerCase().includes(q)),
    );
  }, [roles, f]);

  const newLink = canCreate ? (
    <NewRecordLink to="/roles-permissions/new" label="Add role" />
  ) : undefined;

  const columns: Column<RoleSummary>[] = [
    {
      key: "role",
      header: "Role",
      sortValue: (r) => r.name,
      render: (r) => (
        <div>
          <PersonCell
            name={r.name}
            to={`/roles-permissions/${r.id}`}
            sub={r.description ?? undefined}
          />
          {r.isSystem && <Badge tone="info">System</Badge>}
        </div>
      ),
    },
    {
      key: "users",
      header: "Users",
      sortValue: (r) => r.userCount,
      render: (r) => r.userCount,
    },
    {
      key: "permissions",
      header: "Permissions",
      sortValue: (r) => (r.hasAllPermissions ? Number.MAX_SAFE_INTEGER : r.permissionCount),
      render: (r) => (r.hasAllPermissions ? "All" : r.permissionCount),
    },
    {
      key: "status",
      header: "Status",
      sortValue: statusOf,
      render: (r) => <Badge tone={r.isActive ? "ok" : "neutral"}>{statusOf(r)}</Badge>,
    },
    {
      key: "actions",
      header: "Actions",
      render: (r) => (
        <RowActions
          name={r.name}
          viewTo={`/roles-permissions/${r.id}`}
          editTo={
            canEdit && !r.hasAllPermissions && !me?.roles.includes(r.name)
              ? `/roles-permissions/${r.id}/edit`
              : undefined
          }
        />
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Roles & Permissions"
        subtitle="What each role may do. Give a user a role on the Users page."
        actions={newLink}
        stats={
          <StatGrid>
            <StatCard label="Roles" value={roles.length} icon={ShieldCheck} tone="info" />
            <StatCard label="Active" value={counts.Active ?? 0} icon={CircleCheck} tone="ok" />
            <StatCard
              label="Custom"
              value={roles.filter((r) => !r.isSystem).length}
              icon={ShieldPlus}
              tone="accent"
            />
          </StatGrid>
        }
      />
      <Panel title="All roles" subtitle={`${roles.length} roles · ${filtered.length} shown`} icon={ShieldCheck}>
        <StatusPills
          label="Filter by status"
          options={STATUS_FILTERS}
          value={f.status as (typeof STATUS_FILTERS)[number]}
          onChange={(v) => filters.set("status", v)}
          counts={counts}
        />
        <FilterBar>
          <SearchBox
            label="Search roles"
            placeholder="Name or description"
            value={f.search}
            onChange={(v) => filters.set("search", v)}
          />
        </FilterBar>
        {list.error && (
          <Notice tone="warn" style={{ margin: 16 }}>
            {list.error}
          </Notice>
        )}
        <DataTable
          caption="Roles"
          columns={columns}
          rows={filtered}
          rowKey={(r) => r.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={ShieldCheck}
              noun="roles"
              hasRecords={roles.length > 0}
              onClearFilters={filters.clear}
              createAction={newLink}
            />
          }
        />
      </Panel>
    </div>
  );
}
