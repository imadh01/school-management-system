import { Link } from "react-router-dom";
import { initialsOf } from "@/utils/initials";

/** A person in a table row: initials avatar, name (a link to their page when `to` is given) and a sub-line. */
export function PersonCell({
  name,
  to,
  sub,
}: {
  name: string;
  to?: string;
  sub?: string;
}) {
  return (
    <div className="person">
      <span className="avatar" aria-hidden="true">
        {initialsOf(name)}
      </span>
      <div>
        {to ? (
          <Link className="person__name person__link" to={to}>
            {name}
          </Link>
        ) : (
          <div className="person__name">{name}</div>
        )}
        {sub && <div className="person__sub">{sub}</div>}
      </div>
    </div>
  );
}
