import { useRef, type KeyboardEvent, type ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

export interface TabItem {
  id: string;
  label: string;
  icon?: LucideIcon;
  /** Shown as a small number after the label (e.g. how many guardians). */
  count?: number;
}

interface TabsProps {
  /** Prefix for the element ids, unique on the page. */
  idBase: string;
  label: string;
  tabs: TabItem[];
  active: string;
  onChange: (id: string) => void;
}

const tabId = (idBase: string, id: string) => `${idBase}-tab-${id}`;
const panelId = (idBase: string, id: string) => `${idBase}-panel-${id}`;

/**
 * U2: accessible tabs (WAI-ARIA tabs pattern): one Tab stop for the whole row, then
 * Left/Right/Home/End move between tabs.
 */
export function Tabs({ idBase, label, tabs, active, onChange }: TabsProps) {
  const refs = useRef<(HTMLButtonElement | null)[]>([]);

  const onKeyDown = (e: KeyboardEvent, index: number) => {
    let next = -1;
    if (e.key === "ArrowRight") next = (index + 1) % tabs.length;
    else if (e.key === "ArrowLeft")
      next = (index - 1 + tabs.length) % tabs.length;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = tabs.length - 1;
    if (next < 0) return;
    e.preventDefault();
    onChange(tabs[next].id);
    refs.current[next]?.focus();
  };

  return (
    <div className="tabs" role="tablist" aria-label={label}>
      {tabs.map((t, i) => {
        const selected = t.id === active;
        const Icon = t.icon;
        return (
          <button
            key={t.id}
            ref={(el) => {
              refs.current[i] = el;
            }}
            type="button"
            role="tab"
            id={tabId(idBase, t.id)}
            aria-selected={selected}
            aria-controls={panelId(idBase, t.id)}
            tabIndex={selected ? 0 : -1}
            className="tabs__tab"
            onClick={() => onChange(t.id)}
            onKeyDown={(e) => onKeyDown(e, i)}
          >
            {Icon && <Icon size={16} aria-hidden="true" />}
            {t.label}
            {t.count !== undefined && (
              <span className="tabs__count">{t.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

interface TabPanelProps {
  idBase: string;
  id: string;
  children: ReactNode;
}

export function TabPanel({ idBase, id, children }: TabPanelProps) {
  return (
    <div
      role="tabpanel"
      id={panelId(idBase, id)}
      aria-labelledby={tabId(idBase, id)}
      tabIndex={0}
      className="tab-panel"
    >
      {children}
    </div>
  );
}
