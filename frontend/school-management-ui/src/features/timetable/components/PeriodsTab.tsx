import { useMemo, useState } from "react";
import { Clock, Pencil, Plus } from "lucide-react";
import { Badge } from "@/components/Badge";
import { DataTable, type Column } from "@/components/DataTable";
import { EmptyState } from "@/components/EmptyState";
import { IconButton } from "@/components/IconButton";
import { Notice } from "@/components/Notice";
import { Panel } from "@/components/Panel";
import { RowActions } from "@/components/ListButtons";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { timetableService } from "../services/timetableService";
import type { TimetablePeriodResponse } from "../types/timetable.types";
import { PeriodDialog } from "./PeriodDialog";

interface Props {
  yearId: number | null;
  /** Timetable.ManagePeriods: add, edit and delete. Without it the list is read only. */
  canManage: boolean;
}

/** The periods and breaks that make up the school day; every class's grid is drawn from them. */
export function PeriodsTab({ yearId, canManage }: Props) {
  const toast = useToast();
  const periods = useAsyncData(
    () =>
      yearId === null
        ? Promise.resolve([] as TimetablePeriodResponse[])
        : timetableService.getPeriods(yearId),
    [yearId],
    "Could not load periods.",
  );
  const rows = useMemo(
    () => [...(periods.data ?? [])].sort((a, b) => a.sortOrder - b.sortOrder),
    [periods.data],
  );

  // undefined = closed, null = adding, a period = editing it.
  const [dialog, setDialog] = useState<TimetablePeriodResponse | null | undefined>(
    undefined,
  );

  const remove = useConfirmAction<TimetablePeriodResponse>({
    title: "Delete period",
    message: (p) => `Delete "${p.label}" (${p.startTime}–${p.endTime})?`,
    confirmLabel: "Delete",
    danger: true,
    run: async (p) => {
      await timetableService.deletePeriod(p.id);
      await periods.reload();
    },
    success: (p) => `"${p.label}" deleted.`,
    failure: "Could not delete this period.",
  });

  const columns: Column<TimetablePeriodResponse>[] = [
    {
      key: "order",
      header: "Order",
      sortValue: (p) => p.sortOrder,
      render: (p) => p.sortOrder,
    },
    {
      key: "label",
      header: "Name",
      sortValue: (p) => p.label,
      render: (p) => <span className="person__name">{p.label}</span>,
    },
    {
      key: "time",
      header: "Time",
      sortValue: (p) => p.startTime,
      render: (p) => `${p.startTime} – ${p.endTime}`,
    },
    {
      key: "kind",
      header: "Type",
      sortValue: (p) => p.kind,
      render: (p) => (
        <Badge tone={p.kind === "Teaching" ? "info" : "warn"}>{p.kind}</Badge>
      ),
    },
    {
      key: "lessons",
      header: "Lessons",
      hideBelow: "md",
      sortValue: (p) => p.cellCount,
      render: (p) => p.cellCount,
    },
    ...(canManage
      ? [
          {
            key: "actions",
            header: "Actions",
            render: (p: TimetablePeriodResponse) => (
              <RowActions
                name={p.label}
                onDelete={p.cellCount === 0 ? () => remove.ask(p) : undefined}
              >
                <IconButton
                  icon={Pencil}
                  tone="info"
                  label={`Edit ${p.label}`}
                  onClick={() => setDialog(p)}
                />
              </RowActions>
            ),
          },
        ]
      : []),
  ];

  const addButton = canManage ? (
    <button
      type="button"
      className="btn btn--primary"
      onClick={() => setDialog(null)}
      disabled={yearId === null}
    >
      <Plus size={16} aria-hidden="true" />
      Add period
    </button>
  ) : undefined;

  return (
    <>
      {periods.error && <Notice tone="warn">{periods.error}</Notice>}
      <Panel
        title="Periods"
        subtitle={`${rows.length} configured · ${rows.filter((p) => p.kind === "Teaching").length} teaching`}
        icon={Clock}
        actions={addButton}
      >
        {canManage && rows.some((p) => p.cellCount > 0) && (
          <Notice style={{ margin: 16 }}>
            A period that has lessons on a timetable cannot be deleted. Clear
            its lessons first.
          </Notice>
        )}
        <DataTable
          caption="Periods"
          columns={columns}
          rows={rows}
          rowKey={(p) => p.id}
          loading={periods.isLoading && !periods.data}
          empty={
            <EmptyState
              icon={Clock}
              title="No periods yet"
              message="Add the periods and breaks of the school day. Each class's timetable is drawn from them."
              action={addButton}
            />
          }
        />
      </Panel>

      {dialog !== undefined && yearId !== null && (
        <PeriodDialog
          period={dialog}
          existing={rows}
          onClose={() => setDialog(undefined)}
          onSave={async (values) => {
            if (dialog === null) {
              await timetableService.createPeriod({
                academicYearId: yearId,
                ...values,
              });
            } else {
              await timetableService.updatePeriod(dialog.id, {
                ...values,
                rowVersion: dialog.rowVersion,
              });
            }
            await periods.reload();
            toast.success(dialog === null ? "Period added." : "Period saved.");
          }}
        />
      )}

      {remove.dialog}
    </>
  );
}
