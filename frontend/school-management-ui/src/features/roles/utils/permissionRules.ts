import type { PermissionModule } from "../types/role.types";

/**
 * A permission is only granted together with what it needs (the API's PermissionDependencies, which
 * stays the authority). The picker follows the same rules so the person never has to guess:
 * ticking one adds what it needs; unticking one also unticks whatever then loses what it needs.
 */
export type Requirements = ReadonlyMap<string, readonly (readonly string[])[]>;

export const requirementsOf = (catalog: PermissionModule[]): Requirements =>
  new Map(
    catalog.flatMap((m) => m.permissions.map((p) => [p.name, p.requires] as const)),
  );

const isSatisfied = (group: readonly string[], held: ReadonlySet<string>) =>
  group.some((n) => held.has(n));

/** Ticks `name` and everything it needs (the first name of each "any one of" group). */
export function grant(
  selected: readonly string[],
  name: string,
  requires: Requirements,
): { next: string[]; added: string[] } {
  const held = new Set(selected);
  const added: string[] = [];
  const queue = [name];
  while (queue.length > 0) {
    const current = queue.shift()!;
    if (!held.has(current)) {
      held.add(current);
      if (current !== name) added.push(current);
    }
    for (const group of requires.get(current) ?? []) {
      if (!isSatisfied(group, held)) queue.push(group[0]);
    }
  }
  return { next: [...held], added };
}

/** Unticks `name`, then everything that no longer has what it needs. */
export function revoke(
  selected: readonly string[],
  name: string,
  requires: Requirements,
): { next: string[]; removed: string[] } {
  const held = new Set(selected);
  held.delete(name);
  const removed: string[] = [];
  for (let changed = true; changed; ) {
    changed = false;
    for (const p of [...held]) {
      const unmet = (requires.get(p) ?? []).some((g) => !isSatisfied(g, held));
      if (unmet) {
        held.delete(p);
        removed.push(p);
        changed = true;
      }
    }
  }
  return { next: [...held], removed };
}
