import { useMemo } from "react";
import { CircleCheck, CircleMinus, Users } from "lucide-react";
import { parentService } from "../services/parentService";
import { isFullParent, type ParentDetail } from "../types/parent.types";
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
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
import { PersonCell } from "@/components/PersonCell";
import {
  ExportCsvButton,
  NewRecordLink,
  PrintButton,
  RowActions,
} from "@/components/ListButtons";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { useFilters } from "@/hooks/useFilters";
import { Permission } from "@/routes/permissions";
import { CONTACT_METHODS } from "@/types/lookups";
import { countBy } from "@/utils/collections";
import { downloadCsv } from "@/utils/csv";

const STATUS_FILTERS = ["All", "Active", "Inactive"] as const;
const muted = <span className="cell-muted">—</span>;

/**
 * U2: the parents list. Opening a parent goes to their own page (details and children); adding and
 * editing are full pages. A teacher holding only Parents.ViewScoped gets the parents of their own
 * students from the API (F1), with name and mobile only, and no buttons that change anything.
 */
export function ParentsPage() {
  const can = useCan();
  const fullAccess = can(Permission.ParentsView);
  const canCreate = can(Permission.ParentsCreate);
  const canEdit = can(Permission.ParentsEdit);
  const canDelete = can(Permission.ParentsDelete);

  const list = useAsyncData(
    () => parentService.getAll(),
    [],
    "Could not load parents.",
  );
  const parents = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({ status: "All", method: "", search: "" });
  const f = filters.values;

  const counts: Record<string, number> = useMemo(
    () => ({
      All: parents.length,
      ...countBy(parents, (p) => (isFullParent(p) ? p.status : null)),
    }),
    [parents],
  );

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    return parents.filter((p) => {
      if (isFullParent(p)) {
        if (f.status !== "All" && p.status !== f.status) return false;
        if (f.method && p.preferredContactMethod !== f.method) return false;
      }
      if (q) {
        const email = isFullParent(p) ? (p.email ?? "") : "";
        if (!`${p.name} ${p.mobile} ${email}`.toLowerCase().includes(q))
          return false;
      }
      return true;
    });
  }, [parents, f]);

  const remove = useConfirmAction<ParentDetail>({
    title: "Delete parent",
    message: (p) => `Delete ${p.name}? This cannot be undone.`,
    confirmLabel: "Delete",
    danger: true,
    run: async (p) => {
      await parentService.delete(p.id);
      await list.reload();
    },
    success: (p) => `${p.name} was deleted.`,
    failure:
      "Could not delete this parent. They may still be linked to students.",
  });

  const exportCsv = () =>
    downloadCsv(
      "parents.csv",
      [
        "Name",
        "Mobile",
        "Email",
        "Employer",
        "Job Title",
        "Contact Method",
        "Status",
      ],
      filtered.map((p) =>
        isFullParent(p)
          ? [
              p.name,
              p.mobile,
              p.email,
              p.employer,
              p.jobTitle,
              p.preferredContactMethod,
              p.status,
            ]
          : [p.name, p.mobile],
      ),
    );

  const newLink = canCreate ? (
    <NewRecordLink to="/parents/new" label="Add parent" />
  ) : undefined;

  const columns: Column<ParentDetail>[] = [
    {
      key: "name",
      header: "Parent",
      sortValue: (p) => p.name,
      render: (p) => (
        <PersonCell
          name={p.name}
          to={`/parents/${p.id}`}
          sub={isFullParent(p) ? (p.email ?? undefined) : undefined}
        />
      ),
    },
    {
      key: "mobile",
      header: "Mobile",
      sortValue: (p) => p.mobile,
      render: (p) => <a href={`tel:${p.mobile}`}>{p.mobile}</a>,
    },
    ...(fullAccess
      ? ([
          {
            key: "employer",
            header: "Employer",
            hideBelow: "md",
            sortValue: (p) => (isFullParent(p) ? p.employer : null),
            render: (p) =>
              isFullParent(p) && p.employer ? (
                <div>
                  <div>{p.employer}</div>
                  {p.jobTitle && (
                    <div className="person__sub">{p.jobTitle}</div>
                  )}
                </div>
              ) : (
                muted
              ),
          },
          {
            key: "method",
            header: "Contact by",
            hideBelow: "md",
            sortValue: (p) =>
              isFullParent(p) ? p.preferredContactMethod : null,
            render: (p) =>
              isFullParent(p) && p.preferredContactMethod ? (
                <Badge tone="info">{p.preferredContactMethod}</Badge>
              ) : (
                muted
              ),
          },
          {
            key: "status",
            header: "Status",
            sortValue: (p) => (isFullParent(p) ? p.status : null),
            render: (p) =>
              isFullParent(p) ? <StatusBadge status={p.status} /> : muted,
          },
        ] satisfies Column<ParentDetail>[])
      : []),
    {
      key: "actions",
      header: "Actions",
      render: (p) => (
        <RowActions
          name={p.name}
          viewTo={`/parents/${p.id}`}
          editTo={canEdit ? `/parents/${p.id}/edit` : undefined}
          onDelete={canDelete ? () => remove.ask(p) : undefined}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHero
        title="Parents"
        subtitle={
          fullAccess
            ? "Parents and guardians, and the students they are linked to."
            : "The parents and guardians of your students."
        }
        actions={
          <>
            <PrintButton />
            {newLink}
          </>
        }
        stats={
          fullAccess ? (
            <StatGrid>
              <StatCard
                label="Total"
                value={parents.length}
                icon={Users}
                tone="info"
              />
              <StatCard
                label="Active"
                value={counts.Active ?? 0}
                icon={CircleCheck}
                tone="ok"
              />
              <StatCard
                label="Inactive"
                value={counts.Inactive ?? 0}
                icon={CircleMinus}
                tone="neutral"
              />
            </StatGrid>
          ) : undefined
        }
      />

      <Panel
        title="All parents"
        subtitle={`${parents.length} parents · ${filtered.length} shown`}
        icon={Users}
        actions={
          <ExportCsvButton
            onClick={exportCsv}
            disabled={filtered.length === 0}
          />
        }
      >
        {fullAccess && (
          <StatusPills
            label="Filter by status"
            options={STATUS_FILTERS}
            value={f.status as (typeof STATUS_FILTERS)[number]}
            onChange={(v) => filters.set("status", v)}
            counts={counts}
          />
        )}
        <FilterBar>
          {fullAccess && (
            <FilterSelect
              label="Contact method"
              allLabel="All contact methods"
              options={CONTACT_METHODS}
              value={f.method}
              onChange={(v) => filters.set("method", v)}
            />
          )}
          <SearchBox
            label="Search parents"
            placeholder="Name, mobile, email"
            value={f.search}
            onChange={(v) => filters.set("search", v)}
          />
          <ClearFiltersButton
            visible={filters.isActive}
            onClear={filters.clear}
          />
        </FilterBar>

        {list.error && (
          <Notice tone="warn" style={{ margin: 16 }}>
            {list.error}
          </Notice>
        )}
        <DataTable
          caption="Parents"
          columns={columns}
          rows={filtered}
          rowKey={(p) => p.id}
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          empty={
            <ListEmptyState
              icon={Users}
              noun="parents"
              hasRecords={parents.length > 0}
              onClearFilters={filters.clear}
              emptyMessage="Parents are added here, or created from a guardian when an admission is enrolled."
              createAction={newLink}
            />
          }
        />
      </Panel>

      {remove.dialog}
    </div>
  );
}
