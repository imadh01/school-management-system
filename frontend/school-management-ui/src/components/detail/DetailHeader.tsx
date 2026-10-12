import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import { initialsOf } from "@/utils/initials";

interface Props {
  back: { to: string; label: string };
  title: string;
  /** Short facts under the name, e.g. admission number and class. */
  meta?: ReactNode[];
  /** Status badge, flags. */
  badges?: ReactNode;
  /** Buttons on the right (Edit, Delete...). */
  actions?: ReactNode;
  photoUrl?: string | null;
}

/** U2: the top of a detail page: back link, avatar, name, facts, badges and actions. */
export function DetailHeader({
  back,
  title,
  meta = [],
  badges,
  actions,
  photoUrl,
}: Props) {
  return (
    <div className="detail-head">
      <Link to={back.to} className="back-link">
        <ArrowLeft size={16} aria-hidden="true" />
        {back.label}
      </Link>
      <div className="detail-head__card">
        {photoUrl ? (
          <img className="detail-head__photo" src={photoUrl} alt="" />
        ) : (
          <span className="avatar detail-head__avatar" aria-hidden="true">
            {initialsOf(title)}
          </span>
        )}
        <div className="detail-head__text">
          <h1>{title}</h1>
          {meta.length > 0 && (
            <p className="detail-head__meta">
              {meta.map((m, i) => (
                <span key={i}>{m}</span>
              ))}
            </p>
          )}
          {badges && <div className="detail-head__badges">{badges}</div>}
        </div>
        {actions && <div className="detail-head__actions">{actions}</div>}
      </div>
    </div>
  );
}
