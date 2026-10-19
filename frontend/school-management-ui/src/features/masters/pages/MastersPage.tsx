import { Link } from "react-router-dom";
import { Banknote, type LucideIcon } from "lucide-react";
import { PageHero } from "@/components/PageHero";
import { Panel } from "@/components/Panel";

interface MasterItem {
  path: string;
  title: string;
  description: string;
  icon: LucideIcon;
}

/**
 * The master (lookup) tables the school keeps in one place. Add a row here when a new master
 * is built; the hub needs no other change.
 */
const MASTERS: readonly MasterItem[] = [
  {
    path: "/masters/admission-fees",
    title: "Admission fees",
    description: "The admission fee of each class, suggested when an admission is confirmed.",
    icon: Banknote,
  },
];

export function MastersPage() {
  return (
    <>
      <PageHero
        title="Masters"
        subtitle="Lookup data the rest of the system uses"
      />
      <Panel title="Master tables" subtitle={`${MASTERS.length} available`} padded>
        <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "grid", gap: 12 }}>
          {MASTERS.map((m) => (
            <li key={m.path}>
              <Link
                to={m.path}
                style={{ display: "flex", gap: 12, alignItems: "center" }}
              >
                <m.icon size={20} aria-hidden="true" />
                <span>
                  <strong>{m.title}</strong>
                  <br />
                  <span className="person__sub">{m.description}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </Panel>
    </>
  );
}
