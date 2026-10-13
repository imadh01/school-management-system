import { useMemo } from "react";
import {
  CalendarClock,
  CircleCheck,
  CircleX,
  FileText,
  Hourglass,
} from "lucide-react";
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
import { Notice } from "@/components/Notice";
import { ListEmptyState } from "@/components/ListEmptyState";
import { ExportCsvButton } from "@/components/ListButtons";
import { useAsyncData } from "@/hooks/useAsyncData";
import { useFilters } from "@/hooks/useFilters";
import { countBy, uniqueSorted } from "@/utils/collections";
import { downloadCsv } from "@/utils/csv";
import { formatDate, formatUtcDate } from "@/utils/format";
import { documentService } from "../services/documentService";
import { DocumentsTable } from "../components/DocumentsTable";
import {
  DOCUMENT_STATUSES,
  EXPIRY_LABEL,
  type DocumentExpiryState,
  type DocumentStatus,
} from "../types/document.types";

const STATUS_FILTERS: readonly (DocumentStatus | "All")[] = [
  "All",
  ...DOCUMENT_STATUSES,
];

const EXPIRY_OPTIONS = Object.values(EXPIRY_LABEL);
const expiryStateOf = (label: string) =>
  (Object.keys(EXPIRY_LABEL) as DocumentExpiryState[]).find(
    (k) => EXPIRY_LABEL[k] === label,
  );

/**
 * G1b: every student's and applicant's documents in one place — the review queue (Pending first
 * is one click away) and the expiry watch-list. Uploading happens on the student's or the
 * application's own page, where the owner is already known.
 */
export function DocumentsPage() {
  const list = useAsyncData(
    () => documentService.getAll(),
    [],
    "Could not load the documents.",
  );
  const documents = useMemo(() => list.data ?? [], [list.data]);

  const filters = useFilters({
    status: "All",
    type: "",
    expiry: "",
    search: "",
  });
  const f = filters.values;

  const typeNames = useMemo(
    () => uniqueSorted(documents.map((d) => d.documentTypeName)),
    [documents],
  );
  const counts = useMemo(
    (): Record<string, number> => ({
      All: documents.length,
      ...countBy(documents, (d) => d.status),
    }),
    [documents],
  );
  const expiryCount = documents.filter((d) => d.expiryState).length;

  const filtered = useMemo(() => {
    const q = f.search.trim().toLowerCase();
    const expiry = expiryStateOf(f.expiry);
    return documents.filter((d) => {
      if (f.status !== "All" && d.status !== f.status) return false;
      if (f.type && d.documentTypeName !== f.type) return false;
      if (expiry && d.expiryState !== expiry) return false;
      if (q) {
        const haystack =
          `${d.ownerName} ${d.ownerNumber} ${d.originalFileName} ${d.title ?? ""}`.toLowerCase();
        if (!haystack.includes(q)) return false;
      }
      return true;
    });
  }, [documents, f]);

  const exportCsv = () =>
    downloadCsv(
      "documents.csv",
      [
        "Document",
        "Label",
        "Belongs to",
        "Number",
        "File",
        "Expiry",
        "Status",
        "Reviewed by",
        "Rejection reason",
        "Uploaded",
        "Uploaded by",
      ],
      filtered.map((d) => [
        d.documentTypeName,
        d.title,
        d.ownerName,
        d.ownerNumber,
        d.originalFileName,
        formatDate(d.expiryDate),
        d.status,
        d.reviewedBy,
        d.rejectionReason,
        formatUtcDate(d.uploadedAt),
        d.uploadedBy,
      ]),
    );

  return (
    <div>
      <PageHero
        title="Documents"
        subtitle="Documents of students and applicants: review queue and expiry watch."
        stats={
          <StatGrid>
            <StatCard
              label="Pending review"
              value={counts.Pending ?? 0}
              icon={Hourglass}
              tone="warn"
            />
            <StatCard
              label="Verified"
              value={counts.Verified ?? 0}
              icon={CircleCheck}
              tone="ok"
            />
            <StatCard
              label="Rejected"
              value={counts.Rejected ?? 0}
              icon={CircleX}
              tone="bad"
            />
            <StatCard
              label="Expired or expiring"
              value={expiryCount}
              icon={CalendarClock}
              tone="info"
            />
          </StatGrid>
        }
      />

      <Panel
        title="All documents"
        subtitle={`${documents.length} documents · ${filtered.length} shown`}
        icon={FileText}
        actions={
          <ExportCsvButton
            onClick={exportCsv}
            disabled={filtered.length === 0}
          />
        }
      >
        <StatusPills
          label="Filter by status"
          options={STATUS_FILTERS}
          value={f.status as DocumentStatus | "All"}
          onChange={(v) => filters.set("status", v)}
          counts={counts}
        />

        <FilterBar>
          <FilterSelect
            label="Document type"
            allLabel="All types"
            options={typeNames}
            value={f.type}
            onChange={(v) => filters.set("type", v)}
          />
          <FilterSelect
            label="Expiry"
            allLabel="Any expiry"
            options={EXPIRY_OPTIONS}
            value={f.expiry}
            onChange={(v) => filters.set("expiry", v)}
          />
          <SearchBox
            label="Search documents"
            placeholder="Name, adm./reg. no., file"
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
        <DocumentsTable
          rows={filtered}
          showOwner
          loading={list.isLoading && !list.data}
          resetPageKey={filters.key}
          onChanged={list.reload}
          empty={
            <ListEmptyState
              icon={FileText}
              noun="documents"
              hasRecords={documents.length > 0}
              onClearFilters={filters.clear}
              emptyMessage="Documents are uploaded on each student's or application's page, in its Documents tab."
            />
          }
        />
      </Panel>
    </div>
  );
}
