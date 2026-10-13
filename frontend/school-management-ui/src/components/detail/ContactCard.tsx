import type { ReactNode } from "react";
import { Mail, Phone } from "lucide-react";

interface Props {
  name: ReactNode;
  /** Relation, "Primary contact"... (use <Badge>). */
  badges?: ReactNode;
  phone?: string | null;
  email?: string | null;
  /** Any extra lines under the contact details. */
  children?: ReactNode;
  /** Buttons at the bottom (Make primary, Unlink...). */
  actions?: ReactNode;
}

/** U2: one person in a list of contacts (guardians, children, pickup persons). Phone and email are links. */
export function ContactCard({
  name,
  badges,
  phone,
  email,
  children,
  actions,
}: Props) {
  return (
    <div className="contact-card">
      <div className="contact-card__name">{name}</div>
      {badges && <div className="contact-card__badges">{badges}</div>}
      {phone && (
        <div className="contact-card__line">
          <Phone size={14} aria-hidden="true" />
          <a href={`tel:${phone}`}>{phone}</a>
        </div>
      )}
      {email && (
        <div className="contact-card__line">
          <Mail size={14} aria-hidden="true" />
          <a href={`mailto:${email}`}>{email}</a>
        </div>
      )}
      {children}
      {actions && <div className="contact-card__actions">{actions}</div>}
    </div>
  );
}

/** The grid that holds ContactCards. */
export function ContactList({ children }: { children: ReactNode }) {
  return <div className="contact-list">{children}</div>;
}
