import { Check, Info } from "lucide-react";
import type { PermissionInfo, PermissionModule } from "../types/role.types";
import { STANDARD_ACTIONS, actionOf, buildMatrix } from "../utils/permissionMatrix";

interface Props {
  catalog: PermissionModule[];
  /** Names of the permissions the role grants. */
  granted: readonly string[];
}

/**
 * E3c: the read-only view of a role's permissions: the same module-by-action matrix as the edit
 * page, with a tick where the role has the permission. Shows what the role cannot do as well, which
 * is what people usually want to check.
 */
export function PermissionSummary({ catalog, granted }: Props) {
  const held = new Set(granted);
  const groups = buildMatrix(catalog);
  const total = catalog.reduce((n, m) => n + m.permissions.length, 0);

  const mark = (p: PermissionInfo, label: string) =>
    held.has(p.name) ? (
      <span className="perm-view perm-view--yes" title={`${label}: granted`}>
        <Check size={14} strokeWidth={3} aria-hidden="true" />
        <span className="visually-hidden">{label}: granted</span>
      </span>
    ) : (
      <span className="perm-view perm-view--no" title={`${label}: not granted`}>
        <span aria-hidden="true" />
        <span className="visually-hidden">{label}: not granted</span>
      </span>
    );

  return (
    <div>
      <p className="perm-summary" aria-live="polite" style={{ margin: "0 0 12px" }}>
        <strong>{granted.length}</strong> of {total} permissions granted
      </p>
      <div className="perm-matrix-wrap" role="region" aria-label="Permissions granted by this role" tabIndex={0}>
        <table className="perm-matrix">
          <caption className="visually-hidden">
            Permissions by module. A tick means the role has the permission.
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
          {groups.map((g) => {
            const names = g.rows.flatMap((r) => r.all.map((p) => p.name));
            return (
              <tbody key={g.name}>
                <tr className="perm-group">
                  <th scope="rowgroup" className="perm-sticky" colSpan={1}>
                    <span className="perm-name">
                      <span>{g.name}</span>
                      <span className="perm-count">
                        {names.filter((n) => held.has(n)).length}/{names.length}
                      </span>
                    </span>
                  </th>
                  <td colSpan={5} />
                </tr>
                {g.rows.map((r) => (
                  <tr key={r.module} className="perm-row">
                    <th scope="row" className="perm-sticky">
                      <span className="perm-name">
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
                    {STANDARD_ACTIONS.map((a) => {
                      const p = r.cells[a];
                      return (
                        <td key={a} className="perm-col">
                          {p ? (
                            mark(p, `${r.label}, ${a}`)
                          ) : (
                            <span className="perm-na">
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
                            <span
                              key={p.name}
                              className={`perm-chip perm-chip--${held.has(p.name) ? "on" : "off"}`}
                            >
                              {mark(p, `${r.label}, ${actionOf(p.name)}`)}
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
