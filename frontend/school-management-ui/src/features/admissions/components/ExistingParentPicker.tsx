import { useEffect, useState } from "react";
import { Search, UsersRound } from "lucide-react";
import type { ParentCandidate } from "../types/admission.types";
import { admissionService } from "../services/admissionService";
import { TextField } from "@/components/form/Inputs";
import { Notice } from "@/components/Notice";

/** Digits of a phone number as typed ("+971 50-123 4567" -> "971501234567"). */
const digitsOf = (text: string) => text.replace(/\D/g, "");

/**
 * Searches parents already in the school (name or mobile) once the text is long enough, waiting a moment after
 * the last keystroke. `query` shorter than `minChars` gives an empty list without calling the API.
 */
function useParentSearch(query: string, minChars: number) {
  const text = query.trim();
  const [state, setState] = useState<{
    forText: string;
    results: ParentCandidate[];
    failed: boolean;
  }>({ forText: "", results: [], failed: false });

  useEffect(() => {
    if (text.length < minChars) return;
    let cancelled = false;
    const timer = window.setTimeout(() => {
      admissionService
        .searchParents(text)
        .then((results) => {
          if (!cancelled) setState({ forText: text, results, failed: false });
        })
        .catch(() => {
          if (!cancelled)
            setState({ forText: text, results: [], failed: true });
        });
    }, 300);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [text, minChars]);

  const ready = text.length >= minChars;
  const settled = ready && state.forText === text;
  return {
    results: settled ? state.results : [],
    failed: settled && state.failed,
    searching: ready && !settled,
  };
}

const childrenNote = (p: ParentCandidate) =>
  p.linkedChildren.length > 0
    ? ` · Children: ${p.linkedChildren.join(", ")}`
    : " · No children linked yet";

/**
 * G2: "Find an existing parent" for one guardian row. Shown only to users who hold Parents.View: the
 * results include mobile numbers and children, and the API refuses the search for anyone else.
 */
export function ExistingParentPicker({
  idPrefix,
  excludeParentIds,
  onPick,
}: {
  idPrefix: string;
  /** Parents already used by another guardian of this application. */
  excludeParentIds: number[];
  onPick: (parent: ParentCandidate) => void;
}) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const { results, failed, searching } = useParentSearch(text, 2);
  const shown = results.filter((p) => !excludeParentIds.includes(p.parentId));

  if (!open)
    return (
      <button
        type="button"
        className="btn btn--secondary btn--sm"
        onClick={() => setOpen(true)}
      >
        <Search size={14} aria-hidden="true" />
        Find an existing parent
      </button>
    );

  return (
    <div className="search-picker">
      <TextField
        id={`${idPrefix}-parent-search`}
        label="Find an existing parent by name or mobile"
        placeholder="At least 2 letters or 3 digits"
        value={text}
        onChange={setText}
        autoFocus
      />
      {failed && (
        <p className="field__error" role="alert">
          Could not search parents. Try again.
        </p>
      )}
      {searching && <p role="status">Searching…</p>}
      {!searching &&
        !failed &&
        text.trim().length >= 2 &&
        shown.length === 0 && (
          <p className="field-note" role="status">
            No parent matches “{text.trim()}”.
          </p>
        )}
      {shown.map((p) => (
        <div key={p.parentId} className="choice-option">
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => {
              onPick(p);
              setOpen(false);
              setText("");
            }}
          >
            Select
          </button>
          <span>
            <b>{p.name}</b> · {p.mobile}
            <small>{childrenNote(p).replace(/^ · /, "")}</small>
          </span>
        </div>
      ))}
      <button
        type="button"
        className="btn btn--secondary btn--sm"
        style={{ marginTop: 6 }}
        onClick={() => {
          setOpen(false);
          setText("");
        }}
      >
        Cancel search
      </button>
    </div>
  );
}

/**
 * G2: a gentle, non-blocking hint under a typed guardian whose mobile number already belongs to a parent in the
 * school ("this may be the same family"). Same permission rule as the picker: render it only for Parents.View.
 */
export function SameNumberHint({
  mobile,
  excludeParentIds,
  onUse,
}: {
  mobile: string | null;
  excludeParentIds: number[];
  onUse: (parent: ParentCandidate) => void;
}) {
  const digits = digitsOf(mobile ?? "");
  const { results } = useParentSearch(digits, 7);
  const matches = results.filter(
    (p) =>
      !excludeParentIds.includes(p.parentId) && digitsOf(p.mobile) === digits,
  );
  if (matches.length === 0) return null;

  return (
    <div style={{ gridColumn: "1 / -1" }}>
      {matches.map((p) => (
        <Notice key={p.parentId} icon={UsersRound}>
          A parent with this number is already in the school: <b>{p.name}</b>
          {childrenNote(p)}.{" "}
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => onUse(p)}
          >
            Use this parent
          </button>
        </Notice>
      ))}
    </div>
  );
}
