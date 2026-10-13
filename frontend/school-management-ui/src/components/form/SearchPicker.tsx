import { useMemo, useState } from "react";
import { TextField } from "./Inputs";

/**
 * Type-to-search over a list already in the browser, with a "Select" button per result. Used to
 * pick an existing parent while enrolling and a student when linking a child to a parent.
 * `items` null = still loading.
 */
export function SearchPicker<T>({
  id,
  label,
  items,
  itemKey,
  itemLabel,
  onPick,
  minChars = 2,
  maxResults = 8,
}: {
  id: string;
  label: string;
  items: readonly T[] | null;
  itemKey: (item: T) => string | number;
  /** What is shown and searched ("Aisha Khan · 98450 12345"). */
  itemLabel: (item: T) => string;
  onPick: (item: T) => void;
  minChars?: number;
  maxResults?: number;
}) {
  const [text, setText] = useState("");
  const q = text.trim().toLowerCase();
  const results = useMemo(
    () =>
      !items || q.length < minChars
        ? []
        : items
            .filter((item) => itemLabel(item).toLowerCase().includes(q))
            .slice(0, maxResults),
    [items, q, minChars, maxResults, itemLabel],
  );

  return (
    <div className="search-picker">
      <TextField
        id={id}
        label={label}
        placeholder={`At least ${minChars} characters`}
        value={text}
        onChange={setText}
        autoFocus
      />
      {items === null && <p role="status">Loading…</p>}
      {items !== null && q.length >= minChars && results.length === 0 && (
        <p className="field-note" role="status">
          Nothing matches “{text.trim()}”.
        </p>
      )}
      {results.map((item) => (
        <div key={itemKey(item)} className="choice-option">
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => onPick(item)}
          >
            Select
          </button>
          <span>{itemLabel(item)}</span>
        </div>
      ))}
    </div>
  );
}
