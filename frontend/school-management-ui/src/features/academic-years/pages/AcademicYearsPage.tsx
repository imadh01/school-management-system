import { useMemo, useState } from "react";
import {
  CalendarRange,
  CircleCheck,
  Copy,
  Lock,
  Pencil,
  Plus,
} from "lucide-react";
import { Badge } from "@/components/Badge";
import { DataTable, type Column } from "@/components/DataTable";
import { EmptyState } from "@/components/EmptyState";
import { IconButton } from "@/components/IconButton";
import { RowActions } from "@/components/ListButtons";
import { Notice } from "@/components/Notice";
import { PageHero } from "@/components/PageHero";
import { Panel } from "@/components/Panel";
import { useToast } from "@/components/Toast";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useCan } from "@/hooks/useCan";
import { useConfirmAction } from "@/hooks/useConfirmAction";
import { Permission } from "@/routes/permissions";
import {
  academicYearService,
  type AcademicYearResponse,
} from "@/services/academicYearService";
import { AcademicYearDialog } from "../components/AcademicYearDialog";
import { CloseYearDialog } from "../components/CloseYearDialog";
import { CopyPeriodsDialog } from "../components/CopyPeriodsDialog";
import { SetCurrentDialog } from "../components/SetCurrentDialog";
import {
  YEAR_STATUS_HELP,
  YEAR_STATUS_TONE,
  formatYearDate,
} from "../types/academicYear.types";

type DialogState =
  | { kind: "edit"; year: AcademicYearResponse | null }
  | { kind: "setCurrent"; year: AcademicYearResponse }
  | { kind: "close"; year: AcademicYearResponse }
  | { kind: "copy"; year: AcademicYearResponse }
  | null;

/** The school's academic years and their life: Upcoming, Current, Ended, Closed. */
export function AcademicYearsPage() {
  const toast = useToast();
  const can = useCan();
  const years = useAsyncData(
    () => academicYearService.getAll(),
    [],
    "Could not load academic years.",
  );
  const rows = useMemo(() => years.data ?? [], [years.data]);
  const current = rows.find((y) => y.isCurrent) ?? null;

  const [dialog, setDialog] = useState<DialogState>(null);
  const closeDialog = () => setDialog(null);

  const canCreate = can(Permission.AcademicYearsCreate);
  const canEdit = can(Permission.AcademicYearsEdit);
  const canSetCurrent = can(Permission.AcademicYearsSetCurrent);
  const canClose = can(Permission.AcademicYearsClose);
  const canCopy = can(Permission.AcademicYearsCopyPeriods);
  const canDelete = can(Permission.AcademicYearsDelete);

  const remove = useConfirmAction<AcademicYearResponse>({
    title: "Delete academic year",
    message: (y) => `Delete the empty year ${y.name}?`,
    confirmLabel: "Delete",
    danger: true,
    run: async (y) => {
      await academicYearService.remove(y.id);
      await years.reload();
    },
    success: (y) => `${y.name} deleted.`,
    failure: "Could not delete this academic year.",
  });

  const columns: Column<AcademicYearResponse>[] = [
    {
      key: "name",
      header: "Year",
      sortValue: (y) => y.startDate,
      render: (y) => <span className="person__name">{y.name}</span>,
    },
    {
      key: "dates",
      header: "Dates",
      hideBelow: "md",
      sortValue: (y) => y.startDate,
      render: (y) => `${formatYearDate(y.startDate)} – ${formatYearDate(y.endDate)}`,
    },
    {
      key: "status",
      header: "Status",
      sortValue: (y) => y.status,
      render: (y) => <Badge tone={YEAR_STATUS_TONE[y.status] ?? "neutral"}>{y.status}</Badge>,
    },
    {
      key: "classes",
      header: "Classes",
      hideBelow: "md",
      sortValue: (y) => y.classCount,
      render: (y) => y.classCount,
    },
    {
      key: "students",
      header: "Active students",
      hideBelow: "lg",
      sortValue: (y) => y.activeEnrollmentCount,
      render: (y) => y.activeEnrollmentCount,
    },
    {
      key: "periods",
      header: "Periods",
      hideBelow: "lg",
      sortValue: (y) => y.periodCount,
      render: (y) => y.periodCount,
    },
    {
      key: "actions",
      header: "Actions",
      render: (y) => {
        const empty = y.classCount === 0 && y.periodCount === 0;
        return (
          <RowActions
            name={y.name}
            onDelete={canDelete && y.status === "Upcoming" && empty ? () => remove.ask(y) : undefined}
          >
            {canEdit && y.status !== "Closed" && (
              <IconButton
                icon={Pencil}
                tone="info"
                label={`Edit ${y.name}`}
                onClick={() => setDialog({ kind: "edit", year: y })}
              />
            )}
            {canSetCurrent && y.status === "Upcoming" && (
              <IconButton
                icon={CircleCheck}
                tone="ok"
                label={`Make ${y.name} the current year`}
                onClick={() => setDialog({ kind: "setCurrent", year: y })}
              />
            )}
            {canCopy && (y.status === "Upcoming" || y.status === "Current") && (
              <IconButton
                icon={Copy}
                tone="accent"
                label={`Copy periods into ${y.name}`}
                onClick={() => setDialog({ kind: "copy", year: y })}
              />
            )}
            {canClose && y.status === "Ended" && (
              <IconButton
                icon={Lock}
                tone="bad"
                label={`Close ${y.name}`}
                onClick={() => setDialog({ kind: "close", year: y })}
              />
            )}
          </RowActions>
        );
      },
    },
  ];

  const addButton = canCreate ? (
    <button
      type="button"
      className="btn btn--primary"
      onClick={() => setDialog({ kind: "edit", year: null })}
    >
      <Plus size={16} aria-hidden="true" />
      Add year
    </button>
  ) : undefined;

  return (
    <>
      <PageHero
        title="Academic Years"
        subtitle="The school's years and which one is current"
        actions={addButton}
      />

      {years.error && <Notice tone="warn">{years.error}</Notice>}

      <Panel
        title="Academic years"
        subtitle={`${rows.length} ${rows.length === 1 ? "year" : "years"}${current ? ` · current: ${current.name}` : ""}`}
        icon={CalendarRange}
        padded={false}
      >
        <div style={{ padding: 16 }}>
          <Notice>
            {YEAR_STATUS_HELP.map((h) => `${h.status}: ${h.text}`).join("  ")}
          </Notice>
        </div>
        <DataTable
          caption="Academic years"
          columns={columns}
          rows={rows}
          rowKey={(y) => y.id}
          loading={years.isLoading && !years.data}
          empty={
            <EmptyState
              icon={CalendarRange}
              title="No academic years"
              message="Add the first academic year."
              action={addButton}
            />
          }
        />
      </Panel>

      {dialog?.kind === "edit" && (
        <AcademicYearDialog
          year={dialog.year}
          onClose={closeDialog}
          onSave={async (values) => {
            if (dialog.year === null) {
              await academicYearService.create(values);
            } else {
              await academicYearService.update(dialog.year.id, {
                ...values,
                rowVersion: dialog.year.rowVersion,
              });
            }
            await years.reload();
            toast.success(dialog.year === null ? "Academic year added." : "Academic year saved.");
          }}
        />
      )}

      {dialog?.kind === "setCurrent" && (
        <SetCurrentDialog
          year={dialog.year}
          current={current}
          onClose={closeDialog}
          onConfirm={async () => {
            await academicYearService.setCurrent(dialog.year.id, {
              rowVersion: dialog.year.rowVersion,
              expectedCurrentYearId: current?.id ?? null,
            });
            await years.reload();
            toast.success(`${dialog.year.name} is now the current year.`);
          }}
        />
      )}

      {dialog?.kind === "close" && (
        <CloseYearDialog
          year={dialog.year}
          onClose={closeDialog}
          onConfirm={async (confirmName) => {
            await academicYearService.close(dialog.year.id, {
              rowVersion: dialog.year.rowVersion,
              confirmName,
            });
            await years.reload();
            toast.success(`${dialog.year.name} is closed.`);
          }}
        />
      )}

      {dialog?.kind === "copy" && (
        <CopyPeriodsDialog
          target={dialog.year}
          sources={rows.filter((y) => y.id !== dialog.year.id && y.periodCount > 0)}
          onClose={closeDialog}
          onCopy={async (sourceId) => {
            const result = await academicYearService.copyPeriods(dialog.year.id, sourceId);
            await years.reload();
            toast.success(
              `${result.copied} ${result.copied === 1 ? "period" : "periods"} copied into ${dialog.year.name}.`,
            );
          }}
        />
      )}

      {remove.dialog}
    </>
  );
}
