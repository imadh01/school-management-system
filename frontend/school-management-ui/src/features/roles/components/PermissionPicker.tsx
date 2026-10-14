import { useMemo, useState, type KeyboardEvent } from "react";
import { ChevronDown, ChevronRight, Info, Lock } from "lucide-react";
import { CheckboxField } from "@/components/form/Inputs";
import { Notice } from "@/components/Notice";
import { SearchBox } from "@/components/SearchBox";
import type { PermissionInfo, PermissionModule } from "../types/role.types";
import {
  STANDARD_ACTIONS,
  applyTick,
  buildMatrix,
  tickState,
  actionOf,
  type MatrixRow,
} from "../utils/permissionMatrix";
import { requirementsOf } from "../utils/permissionRules";
import { TriStateCheckbox } from "./TriStateCheckbox";

interface Props {
  idPrefix: string;
  catalog: PermissionModule[];
  selected: string[];
  onChange: (selected: string[]) => void;
  /** Permissions you may hand out: you can only grant what you hold yourself (no escalation). */
  canGrant: (permission: string) => boolean;
  /** Nothing can be changed (the Admin role, or the role you hold yourself). */
  readOnly?: boolean;
}

const NOT_HELD = "You don't hold this permission";
const READ_ONLY = "This role's permissions can't be changed here";

/**
 * E3c: the permissions of a role as a matrix: a row per module under group headings, columns View /
 * Create / Edit / Delete / Other actions. Same rules as before, only laid out for less scrolling:
 *   - ticking a permission also ticks what it needs; unticking one also unticks what then loses
 *     what it needs, and a line says what moved (permissionRules.grant / revoke);
 *   - permissions you do not hold yourself are locked and every "check all" skips them
 *     (applyTick), so the set sent to the API is one it will accept.
 * Check all works per group, per row and per column within a group. Arrow keys move between boxes.
 */
export function PermissionPicker({
  idPrefix,
  catalog,
  selected,
  onChange,
  canGrant,
  readOnly = false,
}: Props) {
  const requires = useMemo(() => requirementsOf(catalog), [catalog]);
  const groups = useMemo(() => buildMatrix(catalog), [catalog]);
  const held = useMemo(() => new Set(selected), [selected]);
  const total = catalog.reduce((n, m) => n + m.permissions.length, 0);

  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [search, setSearch] = useState("");
  const [onlyTicked, setOnlyTicked] = useState(false);
  const [note, setNote] = useState<{ tone: "info" | "warn"; text: string } | null>(null);

  const mayChange = (name: string) => !readOnly && canGrant(name);

  /** The one place a click, a row/column/group "check all" and a single box all go through. */
  const change = (targets: readonly string[], on: boolean) => {
    const r = applyTick(selected, targets, on, requires, canGrant);
    const lines: string[] = [];
    if (r.added.length) lines.push(`Also ticked, because they are needed: ${r.added.join(", ")}.`);
    if (r.removed.length) lines.push(`Also unticked, because they need it: ${r.removed.join(", ")}.`);
    if (r.skipped.length)
      lines.push(
        `Left as they were, because that would involve a permission you don't hold: ${r.skipped.join(", ")}.`,
      );
    setNote(
      lines.length
        ? { tone: r.skipped.length ? "warn" : "info", text: lines.join(" ") }
        : null,
    );
    onChange(r.next);
  };

  /** Click on an "all of these" box: tick every one you may change, or untick them if all are ticked. */
  const toggleScope = (names: readonly string[]) => {
    const changeable = names.filter(mayChange);
    if (changeable.length === 0) return;
    change(changeable, !changeable.every((n) => held.has(n)));
  };

  const toggleGroup = (name: string) =>
    setCollapsed((c) => {
      const next = new Set(c);
      if (!next.delete(name)) next.add(name);
      return next;
    });

  const q = search.trim().toLowerCase();
  const filtering = q !== "" || onlyTicked;
  const rowShown = (r: MatrixRow) =>
    (!q || r.label.toLowerCase().includes(q) || r.module.toLowerCase().includes(q)) &&
    (!onlyTicked || r.all.some((p) => held.has(p.name)));

  // Arrow keys move between boxes: left/right along the row, up/down within the same column.
  const onKeyDown = (e: KeyboardEvent<HTMLTableElement>) => {
    const box = e.target;
    if (!(box instanceof HTMLInputElement) || !box.classList.contains("perm-check")) return;
    if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(e.key)) return;
    const rows = [...e.currentTarget.querySelectorAll<HTMLTableRowElement>("tr[data-grid-row]")];
    const row = box.closest("tr");
    if (!row) return;
    const boxesOf = (tr: HTMLElement) => [...tr.querySelectorAll<HTMLInputElement>("input.perm-check")];
    let target: HTMLInputElement | undefined;
    if (e.key === "ArrowLeft" || e.key === "ArrowRight") {
      const cells = boxesOf(row);
      target = cells[cells.indexOf(box) + (e.key === "ArrowRight" ? 1 : -1)];
    } else {
      const step = e.key === "ArrowDown" ? 1 : -1;
      for (let i = rows.indexOf(row) + step; i >= 0 && i < rows.length && !target; i += step) {
        const cells = boxesOf(rows[i]);
        target = cells.find((c) => c.dataset.col === box.dataset.col);
      }
    }
    if (target) {
      e.preventDefault();
      target.focus();
    }
  };

  const describe = (p: PermissionInfo, row: MatrixRow) =>
    `${row.label}, ${actionOf(p.name)}`;

  /** One permission's box. `col` is only used for arrow-key movement. */
  const box = (p: PermissionInfo, row: MatrixRow, col: string) => {
    const locked = !mayChange(p.name);
    const why = readOnly ? READ_ONLY : !canGrant(p.name) ? NOT_HELD : undefined;
    return (
      <label className="perm-cell" title={why ?? p.description}>
        <input
          type="checkbox"
          className="perm-check"
          data-col={col}
          checked={held.has(p.name)}
          aria-label={`${describe(p, row)}. ${p.description}${why ? ` (${why})` : ""}`}
          aria-disabled={locked || undefined}
          onChange={(e) => {
            if (!locked) change([p.name], e.target.checked);
          }}
        />
        {!readOnly && !canGrant(p.name) && <Lock size={12} className="perm-lock" aria-hidden="true" />}
      </label>
    );
  };

  const scopeBox = (names: readonly string[], label: string, col: string) => {
    if (names.length === 0) return <span className="perm-na" aria-hidden="true">–</span>;
    const changeable = names.filter(mayChange);
    return (
      <TriStateCheckbox
        state={tickState(names, held)}
        label={label}
        col={col}
        locked={changeable.length === 0}
        title={changeable.length === 0 ? (readOnly ? READ_ONLY : NOT_HELD) : label}
        onToggle={() => toggleScope(names)}
      />
    );
  };

  const shownGroups = groups
    .map((g) => ({ ...g, shown: g.rows.filter(rowShown) }))
    .filter((g) => g.shown.length > 0);
  const allCollapsed = groups.every((g) => collapsed.has(g.name));

  return (
    <div className="perm-picker">
      <div className="perm-toolbar">
        <p className="perm-summary" aria-live="polite">
          <strong>{selected.length}</strong> of {total} permissions ticked
        </p>
        <SearchBox
          label="Search modules"
          placeholder="Search modules"
          value={search}
          onChange={setSearch}
        />
        <CheckboxField
          id={`${idPrefix}-only-ticked`}
          label="Show only ticked"
          checked={onlyTicked}
          onChange={setOnlyTicked}
        />
        <div className="perm-toolbar__buttons">
          <button type="button" className="btn btn--secondary btn--sm" onClick={() => setCollapsed(new Set())}>
            Expand all
          </button>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => setCollapsed(new Set(groups.map((g) => g.name)))}
            disabled={allCollapsed}
          >
            Collapse all
          </button>
        </div>
      </div>

      {readOnly && (
        <Notice tone="info" style={{ marginBottom: 12 }}>
          {READ_ONLY}.
        </Notice>
      )}
      <div role="status">
        {note && (
          <Notice tone={note.tone} style={{ marginBottom: 12 }}>
            {note.text}
          </Notice>
        )}
      </div>

      <div className="perm-matrix-wrap" role="region" aria-label="Permissions matrix" tabIndex={0}>
        <table className="perm-matrix" onKeyDown={onKeyDown}>
          <caption className="visually-hidden">
            Permissions by module. Columns: View, Create, Edit, Delete and other actions.
          </caption>
          <thead>
            <tr>
              <th scope="col" className="perm-sticky">Module</th>
              {STANDARD_ACTIONS.map((a) => (
                <th scope="col" key={a} className="perm-col">{a}</th>
              ))}
              <th scope="col" className="perm-others">Other actions</th>
            </tr>
          </thead>
          {shownGroups.length === 0 && (
            <tbody>
              <tr>
                <td colSpan={6} className="perm-empty">
                  No module matches{q ? ` "${search.trim()}"` : ""}{onlyTicked ? " with a ticked permission" : ""}.
                </td>
              </tr>
            </tbody>
          )}
          {shownGroups.map((g) => {
            const isOpen = filtering || !collapsed.has(g.name);
            const groupNames = g.rows.flatMap((r) => r.all.map((p) => p.name));
            const ticked = groupNames.filter((n) => held.has(n)).length;
            return (
              <tbody key={g.name}>
                <tr className="perm-group" data-grid-row>
                  <th scope="rowgroup" className="perm-sticky">
                    <span className="perm-name">
                      <button
                        type="button"
                        className="perm-toggle"
                        aria-expanded={isOpen}
                        aria-label={`${g.name}, ${isOpen ? "collapse" : "expand"}`}
                        onClick={() => toggleGroup(g.name)}
                        disabled={filtering}
                      >
                        {isOpen ? <ChevronDown size={16} aria-hidden="true" /> : <ChevronRight size={16} aria-hidden="true" />}
                      </button>
                      {scopeBox(groupNames, `${g.name}, check all`, "all")}
                      <span>{g.name}</span>
                      <span className="perm-count">{ticked}/{groupNames.length}</span>
                    </span>
                  </th>
                  {STANDARD_ACTIONS.map((a, i) => (
                    <td key={a} className="perm-col">
                      {scopeBox(
                        g.rows.flatMap((r) => (r.cells[a] ? [r.cells[a]!.name] : [])),
                        `${g.name} group, all ${a}`,
                        String(i),
                      )}
                    </td>
                  ))}
                  <td className="perm-others">
                    {scopeBox(
                      g.rows.flatMap((r) => r.others.map((p) => p.name)),
                      `${g.name} group, all other actions`,
                      "o",
                    )}
                  </td>
                </tr>
                {isOpen &&
                  g.shown.map((r) => (
                    <tr key={r.module} className="perm-row" data-grid-row>
                      <th scope="row" className="perm-sticky">
                        <span className="perm-name">
                          {scopeBox(
                            r.all.map((p) => p.name),
                            `${r.label}, check all`,
                            "all",
                          )}
                          <span>{r.label}</span>
                          <span
                            className="perm-info"
                            role="img"
                            title={r.all.map((p) => `${actionOf(p.name)}: ${p.description}`).join("\n")}
                            aria-label={`About ${r.label}: ${r.all.map((p) => `${actionOf(p.name)}: ${p.description}`).join(" ")}`}
                          >
                            <Info size={14} aria-hidden="true" />
                          </span>
                        </span>
                      </th>
                      {STANDARD_ACTIONS.map((a, i) => {
                        const p = r.cells[a];
                        return (
                          <td key={a} className="perm-col">
                            {p ? (
                              box(p, r, String(i))
                            ) : (
                              <span className="perm-na" title={`${r.label} has no ${a} permission`}>
                                <span aria-hidden="true">–</span>
                                <span className="visually-hidden">{r.label}, {a}: not available</span>
                              </span>
                            )}
                          </td>
                        );
                      })}
                      <td className="perm-others">
                        {r.others.length === 0 ? (
                          <span className="perm-na" aria-hidden="true">–</span>
                        ) : (
                          <span className="perm-chips">
                            {r.others.map((p) => (
                              <span className="perm-chip" key={p.name}>
                                {box(p, r, "o")}
                                <span>{actionOf(p.name)}</span>
                              </span>
                            ))}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
              </tbody>
            );
          })}
        </table>
      </div>
    </div>
  );
}
