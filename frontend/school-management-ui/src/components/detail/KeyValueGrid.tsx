import type { ReactNode } from "react";

export interface KeyValue {
  label: string;
  value: ReactNode;
  /** Takes the whole row (long text such as notes). */
  wide?: boolean;
}

/** U2: read-only label/value pairs on detail pages. Empty values show a dash. */
export function KeyValueGrid({ items }: { items: KeyValue[] }) {
  return (
    <dl className="kv-grid">
      {items.map((item) => {
        const empty =
          item.value === null ||
          item.value === undefined ||
          item.value === "" ||
          item.value === false;
        return (
          <div
            key={item.label}
            className={
              item.wide ? "kv-grid__item kv-grid__item--wide" : "kv-grid__item"
            }
          >
            <dt>{item.label}</dt>
            <dd>
              {empty ? (
                <span className="cell-muted">
                  <span aria-hidden="true">—</span>
                  <span className="visually-hidden">Not recorded</span>
                </span>
              ) : (
                item.value
              )}
            </dd>
          </div>
        );
      })}
    </dl>
  );
}
