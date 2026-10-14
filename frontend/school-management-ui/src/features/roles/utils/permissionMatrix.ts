import type { PermissionInfo, PermissionModule } from "../types/role.types";
import { grant, revoke, type Requirements } from "./permissionRules";

/**
 * E3c: the permission catalog laid out as a matrix: one row per module, grouped, with the four
 * standard actions as columns and everything else under "Other actions". Layout only: the rules
 * (what a permission needs, what you may hand out) stay in permissionRules and on the API.
 */
export const STANDARD_ACTIONS = ["View", "Create", "Edit", "Delete"] as const;
export type StandardAction = (typeof STANDARD_ACTIONS)[number];

/** Headings the module rows sit under. A module not listed here goes under "Other". */
const GROUPS: readonly { name: string; modules: readonly string[] }[] = [
  { name: "People", modules: ["Admissions", "Students", "Parents", "Teachers"] },
  { name: "Academic", modules: ["ClassSections", "Subjects", "Attendance"] },
  { name: "Records", modules: ["Documents"] },
  { name: "Administration", modules: ["Users", "Roles", "Settings"] },
];

export interface MatrixRow {
  module: string;
  /** "ClassSections" → "Class sections". */
  label: string;
  cells: Partial<Record<StandardAction, PermissionInfo>>;
  others: PermissionInfo[];
  all: PermissionInfo[];
}

export interface MatrixGroup {
  name: string;
  rows: MatrixRow[];
}

export const actionOf = (permission: string) =>
  permission.slice(permission.indexOf(".") + 1);

const labelOf = (module: string) => {
  const spaced = module.replace(/([a-z])([A-Z])/g, "$1 $2");
  return spaced.charAt(0) + spaced.slice(1).toLowerCase();
};

export function buildMatrix(catalog: PermissionModule[]): MatrixGroup[] {
  const rowOf = (m: PermissionModule): MatrixRow => {
    const row: MatrixRow = {
      module: m.module,
      label: labelOf(m.module),
      cells: {},
      others: [],
      all: m.permissions,
    };
    for (const p of m.permissions) {
      const action = actionOf(p.name);
      if ((STANDARD_ACTIONS as readonly string[]).includes(action))
        row.cells[action as StandardAction] = p;
      else row.others.push(p);
    }
    return row;
  };

  const rows = catalog.map(rowOf);
  const placed = new Set<string>();
  const groups: MatrixGroup[] = GROUPS.map((g) => ({
    name: g.name,
    rows: g.modules
      .map((mod) => rows.find((r) => r.module === mod))
      .filter((r): r is MatrixRow => !!r),
  }));
  groups.forEach((g) => g.rows.forEach((r) => placed.add(r.module)));
  const rest = rows.filter((r) => !placed.has(r.module));
  if (rest.length > 0) groups.push({ name: "Other", rows: rest });
  return groups.filter((g) => g.rows.length > 0);
}

export type TickState = "none" | "some" | "all";

/** none / some / all of these permissions are ticked (locked ones count: they are part of the picture). */
export function tickState(names: readonly string[], held: ReadonlySet<string>): TickState {
  const ticked = names.filter((n) => held.has(n)).length;
  return ticked === 0 ? "none" : ticked === names.length ? "all" : "some";
}

export interface TickResult {
  next: string[];
  /** Ticked as a side effect, because something needed them. */
  added: string[];
  /** Unticked as a side effect, because they needed something that was unticked. */
  removed: string[];
  /** Left as they were: they would have needed (or taken away) a permission you do not hold. */
  skipped: string[];
}

/**
 * Ticks or unticks `targets` one by one with the same dependency rules as a single click
 * (permissionRules.grant / revoke). A permission you cannot hand out is never touched, and a
 * change that would drag one along (a dependency you do not hold) is skipped, so "Check all" can
 * never produce a set the API would refuse.
 */
export function applyTick(
  selected: readonly string[],
  targets: readonly string[],
  on: boolean,
  requires: Requirements,
  canGrant: (permission: string) => boolean,
): TickResult {
  let current = [...selected];
  const added = new Set<string>();
  const removed = new Set<string>();
  const skipped: string[] = [];

  for (const name of targets) {
    if (!canGrant(name)) continue;
    const isTicked = current.includes(name);
    if (on === isTicked) continue;

    if (on) {
      const r = grant(current, name, requires);
      if (r.added.some((a) => !canGrant(a))) {
        skipped.push(name);
        continue;
      }
      current = r.next;
      r.added.forEach((a) => added.add(a));
    } else {
      const r = revoke(current, name, requires);
      if (r.removed.some((x) => !canGrant(x))) {
        skipped.push(name);
        continue;
      }
      current = r.next;
      r.removed.forEach((x) => removed.add(x));
    }
  }

  const wanted = new Set(targets);
  return {
    next: current,
    added: [...added].filter((a) => !wanted.has(a)),
    removed: [...removed].filter((x) => !wanted.has(x)),
    skipped,
  };
}
