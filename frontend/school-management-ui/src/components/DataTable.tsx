import { useMemo, useState, type ReactNode } from "react";
import {
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  /** Makes the column sortable. Return a string or number; null sorts last. */
  sortValue?: (row: T) => string | number | null | undefined;
  /** Hide on narrow screens: "md" under 900 px, "lg" under 1200 px. */
  hideBelow?: "md" | "lg";
}

interface Props<T> {
  /** Read by screen readers as the table's name. */
  caption: string;
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  loading?: boolean;
  /** Shown when there are no rows (after filtering). */
  empty?: ReactNode;
  pageSizeOptions?: number[];
  initialPageSize?: number;
  /** Change this value to jump back to page 1. Pass a string or number (e.g. JSON of the filters), not a new object each render. */
  resetPageKey?: unknown;
}

type SortState = { key: string; direction: "asc" | "desc" } | null;

/**
 * U1: the standard list table. Sorting and paging happen in the browser (our lists are small);
 * when a list needs server paging, this component keeps its props and gains a "server" mode.
 */
export function DataTable<T>({
  caption,
  columns,
  rows,
  rowKey,
  loading = false,
  empty,
  pageSizeOptions = [10, 20, 50],
  initialPageSize = 20,
  resetPageKey,
}: Props<T>) {
  const [sort, setSort] = useState<SortState>(null);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const [page, setPage] = useState(1);

  // Back to page 1 when the filters change (adjusting state during render, no extra effect pass).
  const [lastResetKey, setLastResetKey] = useState(resetPageKey);
  if (lastResetKey !== resetPageKey) {
    setLastResetKey(resetPageKey);
    setPage(1);
  }

  const sorted = useMemo(() => {
    if (!sort) return rows;
    const column = columns.find((c) => c.key === sort.key);
    if (!column?.sortValue) return rows;
    const dir = sort.direction === "asc" ? 1 : -1;
    return [...rows].sort((a, b) => {
      const va = column.sortValue!(a);
      const vb = column.sortValue!(b);
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === "number" && typeof vb === "number")
        return (va - vb) * dir;
      return (
        String(va).localeCompare(String(vb), undefined, {
          numeric: true,
          sensitivity: "base",
        }) * dir
      );
    });
  }, [rows, columns, sort]);

  const total = sorted.length;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(page, pageCount);
  const start = (current - 1) * pageSize;
  const pageRows = sorted.slice(start, start + pageSize);

  const toggleSort = (key: string) =>
    setSort((s) =>
      s?.key !== key
        ? { key, direction: "asc" }
        : s.direction === "asc"
          ? { key, direction: "desc" }
          : null,
    );

  const hideClass = (c: Column<T>) =>
    c.hideBelow ? `hide-below-${c.hideBelow}` : undefined;

  // Up to five page numbers around the current page.
  const firstShown = Math.max(1, Math.min(current - 2, pageCount - 4));
  const pages = Array.from(
    { length: Math.min(5, pageCount) },
    (_, i) => firstShown + i,
  );

  return (
    <>
      <div className="table-box">
        <table>
          <caption className="visually-hidden">{caption}</caption>
          <thead>
            <tr>
              {columns.map((c) => {
                const active = sort?.key === c.key;
                const ariaSort = active
                  ? sort!.direction === "asc"
                    ? "ascending"
                    : "descending"
                  : undefined;
                const SortIcon = !active
                  ? ArrowUpDown
                  : sort!.direction === "asc"
                    ? ArrowUp
                    : ArrowDown;
                return (
                  <th
                    key={c.key}
                    scope="col"
                    className={hideClass(c)}
                    aria-sort={ariaSort}
                  >
                    {c.sortValue ? (
                      <button
                        type="button"
                        className="th-sort"
                        onClick={() => toggleSort(c.key)}
                      >
                        {c.header}
                        <SortIcon size={13} aria-hidden="true" />
                      </button>
                    ) : (
                      c.header
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={columns.length}>
                  <div className="empty-state" role="status">
                    Loading…
                  </div>
                </td>
              </tr>
            ) : pageRows.length === 0 ? (
              <tr>
                <td colSpan={columns.length}>
                  {empty ?? <div className="empty-state">Nothing to show.</div>}
                </td>
              </tr>
            ) : (
              pageRows.map((row) => (
                <tr key={rowKey(row)}>
                  {columns.map((c) => (
                    <td key={c.key} className={hideClass(c)}>
                      {c.render(row)}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="table-footer">
        <span>
          {total === 0 ? (
            "No records"
          ) : (
            <>
              Showing{" "}
              <strong>
                {start + 1}–{Math.min(start + pageSize, total)}
              </strong>{" "}
              of <strong>{total}</strong>
            </>
          )}
        </span>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 14,
            flexWrap: "wrap",
          }}
        >
          <label className="pagesize">
            Rows
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
            >
              {pageSizeOptions.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          <nav className="pager" aria-label={`${caption} pages`}>
            <button
              type="button"
              aria-label="Previous page"
              disabled={current <= 1}
              onClick={() => setPage(current - 1)}
            >
              <ChevronLeft size={16} aria-hidden="true" />
            </button>
            {pages.map((p) => (
              <button
                key={p}
                type="button"
                aria-label={`Page ${p}`}
                aria-current={p === current ? "page" : undefined}
                onClick={() => setPage(p)}
              >
                {p}
              </button>
            ))}
            <button
              type="button"
              aria-label="Next page"
              disabled={current >= pageCount}
              onClick={() => setPage(current + 1)}
            >
              <ChevronRight size={16} aria-hidden="true" />
            </button>
          </nav>
        </div>
      </div>
    </>
  );
}
