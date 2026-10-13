/**
 * "Export CSV" on every list page. One place for quoting rules, so a name with a comma or a quote
 * never breaks a column.
 */
type Cell = string | number | boolean | null | undefined;

function toCell(value: Cell): string {
  const text = value === null || value === undefined ? "" : String(value);
  return `"${text.replace(/"/g, '""')}"`;
}

/** Builds the file in the browser and starts the download. */
export function downloadCsv(
  filename: string,
  headers: string[],
  rows: Cell[][],
): void {
  const lines = [headers, ...rows].map((row) => row.map(toCell).join(","));
  const blob = new Blob([lines.join("\n")], {
    type: "text/csv;charset=utf-8;",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
