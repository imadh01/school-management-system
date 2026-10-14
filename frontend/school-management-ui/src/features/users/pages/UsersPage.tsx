import { useMemo, useState } from "react";
import { CircleCheck, KeyRound, Lock, UserCog, UserRoundX } from "lucide-react";
import { userService } from "../services/userService";
import {
  PORTAL_ROLES,
  USER_STATUSES,
  STAFF_GENDERS,
  type UserListItem,
} from "../types/user.types";
import {
  DeleteUserDialog,
  ResetPasswordDialog,
  type AccountTarget,
} from "../components/AccountActionDialogs";
import { PageHero } from "@/components/PageHero";
import { StatCard, StatGrid } from "@/components/StatCard";
import { Panel } from "@/components/Panel";
import {
  ClearFiltersButton,
  FilterBar,
  FilterSelect,
} from "@/components/FilterBar";
import { SearchBox } from "@/components/SearchBox";
import { StatusPills } from "@/components/StatusPills";
import { DataTable, type Column } from "@/components/DataTable";
import { Badge, StatusBadge } from "@/components/Badge";
import { IconButton } from "@/components/IconButton";
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
import { PersonCell } from "@/components/PersonCell";
import { NewRecordLink, RowActions } from "@/components/ListButtons";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useFilters } from "@/hooks/useFilters";
import { Permission } from "@/routes/permissions";
import { useAuth } from "@/store/AuthContext";
import { countBy, uniqueSorted } from "@/utils/collections";
import { formatDate, formatUtcDateTime } from "@/utils/format";

const STATUS_FILTERS = ["All", ...USER_STATUSES] as const;

type Action =
  | { kind: "reset"; user: UserListItem }
  | { kind: "delete"; user: UserListItem };

const targetOf = (u: UserListItem): AccountTarget => ({
  id: u.id,
  name: u.fullName,
  status: u.status,
  rowVersion: "", // reset and delete do not need it; status changes happen on the user's page
});

/**
 * E3a: the Users / Staff list (Administration). One row per staff login. "Needs name" marks
 * accounts whose name is still the username the E2a migration copied in. Reset password and
 * delete are pop-ups that sign the user out everywhere; status, role and unlock are on the
 * user's own page. Your own row has no reset/delete (the API refuses them for yourself).
 */
export function UsersPage() {
  const can = useCan();
  const toast = useToast();
  const { user: me } = useAuth();
  const canCreate = can(Permission.UsersCreate);
  const canEdit = can(Permission.UsersEdit);
  const canDelete = can(Permission.UsersDelete);
  const canReset = can(Permission.UsersResetPassword);

  const list = useAsyncData(
    () => userService.getAll(),
    [],
    "Could not load users.",
  );
  const users = useMemo(() => list.data ?? [], [list.data]);
  const [action, setAction] = useState<Action | null>(null);

  const filters = useFilters({ status: "All", role: "", gender: "", search: "" });
  const f = filters.values;

  // Roles that occur in the list (custom roles included); never Student/Parent (decision 1).
  const roles = useMemo(
    () =>
      uniqueSorted(users.map((u) => u.role)).filter(
        (r) => !PORTAL_ROLES.includes(r),
      ),
    [users],
  );
  const counts: Record<string, number> = useMemo(
    () => ({ All: users.length, ...countBy(users, (u) => u.status) }),
    [users],
  );
  const needsName = users.filter((u) => u.needsName).length;
  const locked = users.filter((u) => u.isLocked).length;

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return users.filter((u) => {
      if (f.status !== "All" && u.status !== f.status) return false;
      if (f.role && u.role !== f.role) return false;
      if (f.gender && u.gender !== f.gender) return false;
      if (
        q &&
        !`${u.fullName} ${u.username} ${u.email} ${u.employeeNumber ?? ""}`
          .toLowerCase()
          .includes(q)
      )
        return false;
      return true;
    });
  }, [users, f]);

  const newLink = canCreate ? (
    <NewRecordLink to="/users-staff/new" label="Add user" />
  ) : undefined;

  const columns: Column<UserListItem>[] = [
    {
      key: "user",
      header: "User",
      sortValue: (u) => u.fullName,
      render: (u) => (
        <div>
          <PersonCell
            name={u.fullName}
            to={`/users-staff/${u.id}`}
            sub={`@${u.username}`}
          />
          {u.needsName && (
            <Badge tone="warn">Needs name</Badge>
          )}
        </div>
      ),
    },
    {
      key: "contact",
      header: "Contact",
      hideBelow: "md",
      sortValue: (u) => u.email,
      render: (u) => (
        <div>
          <div>{u.email}</div>
          {u.mobile && <div className="person__sub">{u.mobile}</div>}
        </div>
      ),
    },
    {
      key: "role",
      header: "Role",
      sortValue: (u) => u.role,
      render: (u) =>
        u.role ? (
          <Badge tone="purple">{u.role}</Badge>
        ) : (
          <span className="cell-muted">—</span>
        ),
    },
    {
      key: "gender",
      header: "Gender",
      hideBelow: "lg",
      sortValue: (u) => u.gender,
      render: (u) => u.gender ?? <span className="cell-muted">—</span>,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (u) => u.status,
      render: (u) => (
        <>
          <StatusBadge status={u.status} />{" "}
          {u.isLocked && <Badge tone="bad">Locked</Badge>}
        </>
      ),
    },
    {
      key: "joined",
      header: "Joined",
      hideBelow: "lg",
      sortValue: (u) => u.joiningDate,
      render: (u) =>
        formatDate(u.joiningDate) ?? <span className="cell-muted">—</span>,
    },
    {
      key: "lastLogin",
      header: "Last login",
      hideBelow: "md",
      sortValue: (u) => u.lastLoginAt,
      render: (u) =>
        formatUtcDateTime(u.lastLoginAt) ?? (
          <span className="cell-muted">Never</span>
        ),
    },
    {
      key: "actions",
      header: "Actions",
      render: (u) => {
        const isMe = u.id === me?.userId;
        return (
          <RowActions
            name={u.fullName}
            viewTo={`/users-staff/${u.id}`}
            editTo={canEdit ? `/users-staff/${u.id}/edit` : undefined}
            onDelete={
              canDelete && !isMe
                ? () => setAction({ kind: "delete", user: u })
                : undefined
            }
          >
            {canReset && !isMe && (
              <IconButton
                icon={KeyRound}
                tone="accent"
                label={`Reset password of ${u.fullName}`}
                onClick={() => setAction({ kind: "reset", user: u })}
              />
            )}
          </RowActions>
        );
      },
    },
  ];

  return (
    <div>
      <PageHero
        title="Users / Staff"
        subtitle="Staff logins, their role and their profile. Teachers are added here too, with the role Teacher."
        actions={newLink}
        stats={
          <StatGrid>
            <StatCard
              label="Users"
              value={users.length}
              icon={UserCog}
              tone="info"
            />
            <StatCard
              label="Active"
              value={counts.Active ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Need a name"
              value={needsName}
              icon={UserRoundX}
              tone="accent"
            />
            <StatCard label="Locked" value={locked} icon={Lock} tone="bad" />
          </StatGrid>
        }
      />

      <Panel
        title="All users"
        subtitle={`${users.length} users · ${filtered.length} shown`}
        icon={UserCog}
      >
        <StatusPills
          label="Filter by status"
          options={STATUS_FILTERS}
          value={f.status as (typeof STATUS_FILTERS)[number]}
          onChange={(v) => filters.set("status", v)}
          counts={counts}
        />
        <FilterBar>
          <FilterSelect
            label="Role"
            allLabel="All roles"
            options={roles}
            value={f.role}
            onChange={(v) => filters.set("role", v)}
          />
          <FilterSelect
            label="Gender"
            allLabel="All genders"
            options={STAFF_GENDERS}
            value={f.gender}
            onChange={(v) => filters.set("gender", v)}
          />
          <SearchBox
            label="Search users"
            placeholder="Name, username, email, employee no."
            value={f.search}
            onChange={(v) => filters.set("search", v)}
          />
          <ClearFiltersButton
            visible={filters.isActive}
            onClear={filters.clear}
          />
        </FilterBar>

        {needsName > 0 && (
          <Notice style={{ margin: 16 }}>
            {needsName === 1 ? "1 account still has" : `${needsName} accounts still have`}{" "}
            the username as its name (copied when staff profiles were
            introduced). Open the user and enter the real name.
          </Notice>
        )}
        {list.error && (
          <Notice tone="warn" style={{ margin: 16 }}>
            {list.error}
          </Notice>
        )}
        <DataTable
          caption="Users"
          columns={columns}
          rows={filtered}
          rowKey={(u) => u.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={UserCog}
              noun="users"
              hasRecords={users.length > 0}
              onClearFilters={filters.clear}
              createAction={newLink}
            />
          }
        />
      </Panel>

      {action?.kind === "reset" && (
        <ResetPasswordDialog
          user={targetOf(action.user)}
          onClose={() => setAction(null)}
          onReset={() => {
            toast.success(
              `A temporary password was set for ${action.user.fullName}. They were signed out everywhere and must choose their own password at the next sign-in.`,
            );
            void list.reload();
          }}
        />
      )}
      {action?.kind === "delete" && (
        <DeleteUserDialog
          user={targetOf(action.user)}
          isTeacher={action.user.teacherId !== null}
          onClose={() => setAction(null)}
          onDeleted={() => {
            toast.success(`${action.user.fullName} was deleted.`);
            void list.reload();
          }}
        />
      )}
    </div>
  );
}
