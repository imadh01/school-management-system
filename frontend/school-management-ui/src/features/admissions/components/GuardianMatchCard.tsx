import { useState, type ReactNode } from "react";
import { Search, TriangleAlert } from "lucide-react";
import type { GuardianMatch } from "../types/admission.types";
import type { ParentContactResponse } from "@/features/parents/types/parent.types";
import { Notice } from "@/components/Notice";
import { SearchPicker } from "@/components/form/SearchPicker";

/** Staff's choice for one guardian: a parent id, "new", or not yet chosen. */
export type GuardianChoice = number | "new" | null;

interface Props {
  guardian: GuardianMatch;
  choice: GuardianChoice;
  onChoose: (choice: GuardianChoice) => void;
  /** Parents picked through "Search another parent" (shown as extra options). */
  extraParents: ParentContactResponse[];
  onPickExtra: (parent: ParentContactResponse) => void;
  /** All parents for the search; null until loaded. */
  allParents: ParentContactResponse[] | null;
  /** Asks the page to load all parents (only when staff open the search). */
  onSearchOpened: () => void;
}

/**
 * One guardian in the enrolment pop-up: link to a parent already in the school (matched by mobile,
 * or found with the search) or create a new parent. Nothing is chosen for staff when the match is
 * unsure.
 */
export function GuardianMatchCard({
  guardian: g,
  choice,
  onChoose,
  extraParents,
  onPickExtra,
  allParents,
  onSearchOpened,
}: Props) {
  const [searchOpen, setSearchOpen] = useState(false);
  const id = g.admissionGuardianId;
  const groupName = `enrol-guardian-${id}`;

  const option = (
    key: string | number,
    value: GuardianChoice,
    label: ReactNode,
  ) => (
    <label key={key} className="choice-option">
      <input
        type="radio"
        name={groupName}
        checked={choice === value}
        onChange={() => onChoose(value)}
      />
      <span>{label}</span>
    </label>
  );

  return (
    <fieldset className="choice-card">
      <legend className="choice-card__title">
        {g.relationType}: {g.name} <span>· {g.mobile ?? "no mobile"}</span>
      </legend>
      {g.suggestedAction === "MissingMobile" ? (
        <p className="field__error">
          A mobile number is required. Edit the application and add one before
          enrolling.
        </p>
      ) : (
        <>
          {g.suggestedAction === "MustChoose" && (
            <Notice
              tone="warn"
              icon={TriangleAlert}
              style={{ margin: "0 0 8px" }}
            >
              More than one parent has this number. Choose carefully.
            </Notice>
          )}
          {g.candidates.length === 0 && extraParents.length === 0 && (
            <p className="field-note" style={{ margin: "0 0 6px" }}>
              No parent in the school has this mobile number.
            </p>
          )}
          {g.candidates.map((c) =>
            option(
              c.parentId,
              c.parentId,
              <>
                Link to existing parent <b>{c.name}</b> · {c.mobile}
                {c.linkedChildren.length > 0 && (
                  <small>Children: {c.linkedChildren.join(", ")}</small>
                )}
              </>,
            ),
          )}
          {extraParents.map((p) =>
            option(
              p.id,
              p.id,
              <>
                Link to existing parent <b>{p.name}</b> · {p.mobile}
              </>,
            ),
          )}
          {option("new", "new", "New parent (create from this guardian)")}

          {searchOpen ? (
            <div style={{ marginTop: 8 }}>
              <SearchPicker
                id={`enrol-search-${id}`}
                label="Search parents by name or mobile"
                items={allParents}
                itemKey={(p) => p.id}
                itemLabel={(p) => `${p.name} · ${p.mobile}`}
                onPick={(p) => {
                  onPickExtra(p);
                  setSearchOpen(false);
                }}
              />
            </div>
          ) : (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              style={{ marginTop: 6 }}
              onClick={() => {
                setSearchOpen(true);
                onSearchOpened();
              }}
            >
              <Search size={14} aria-hidden="true" />
              Search another parent…
            </button>
          )}
        </>
      )}
    </fieldset>
  );
}
