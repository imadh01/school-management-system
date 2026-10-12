import { initialsOf } from "@/utils/initials";
import { useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
import { ChevronDown, LogOut } from "lucide-react";
import {
  NAV_SECTIONS,
  FOOTER_NAV,
  findNavEntry,
  type NavItem,
} from "./navConfig";
import { PAGE_PERMISSIONS } from "../routes/permissions";
import { useAuth } from "../store/AuthContext";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { IconButton } from "../components/IconButton";
import brandMark from "../assets/synergein-mark.jpg";

// TODO (E2): read from SchoolSettings once that module exists.
const SCHOOL_NAME = "Demo School";

interface Props {
  /** Narrow screens: the sidebar is a drawer. */
  isOpen: boolean;
  onClose: () => void;
}

export function Sidebar({ isOpen, onClose }: Props) {
  const { user, logout, hasAnyPermission } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const [confirmingLogout, setConfirmingLogout] = useState(false);

  // F1: show only the pages this user can open ("View or ViewScoped" per page).
  // Cosmetic: the route guard and the API check again.
  const canOpen = (item: NavItem) => {
    const required = PAGE_PERMISSIONS[item.path];
    return !required || hasAnyPermission(required);
  };
  const sections = NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter(canOpen),
  })).filter((section) => section.items.length > 0);

  // The group holding the current page is always open.
  const activeSection = findNavEntry(location.pathname)?.section;

  const handleLogout = async () => {
    setConfirmingLogout(false);
    await logout(); // D2: ends the session on the server too (refresh cookie revoked)
    navigate("/login");
  };

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? "sidebar__link sidebar__link--active" : "sidebar__link";

  const renderLink = (item: NavItem) => {
    const Icon = item.icon;
    return (
      <NavLink
        key={item.key}
        to={item.path}
        className={linkClass}
        onClick={onClose}
      >
        <span className="sidebar__link-icon">
          <Icon size={17} aria-hidden="true" />
        </span>
        <span style={{ flex: 1 }}>{item.label}</span>
        {!item.built && (
          <span
            className="badge badge--neutral"
            style={{ padding: "2px 8px", fontSize: 11 }}
          >
            Soon
          </span>
        )}
      </NavLink>
    );
  };

  return (
    <>
      <div
        className={`sidebar-backdrop${isOpen ? " sidebar-backdrop--open" : ""}`}
        onClick={onClose}
        aria-hidden="true"
      />
      <aside
        id="app-sidebar"
        className={`sidebar${isOpen ? " sidebar--open" : ""}`}
        aria-label="Main navigation"
      >
        <div className="sidebar__brand">
          <div className="sidebar__brand-mark">
            <img src={brandMark} alt="" />
          </div>
          <div>
            <div className="sidebar__brand-name">Synergein School</div>
            <div className="sidebar__brand-sub">{SCHOOL_NAME}</div>
          </div>
        </div>

        <nav className="sidebar__nav">
          {sections.map((section) => {
            const open =
              section.label === activeSection || !collapsed[section.label];
            const listId = `nav-group-${section.label.toLowerCase()}`;
            return (
              <div key={section.label} className="sidebar__group">
                <button
                  type="button"
                  className="sidebar__group-toggle"
                  aria-expanded={open}
                  aria-controls={listId}
                  onClick={() =>
                    setCollapsed((c) => ({ ...c, [section.label]: open }))
                  }
                >
                  {section.label}
                  <ChevronDown size={14} aria-hidden="true" />
                </button>
                {open && <div id={listId}>{section.items.map(renderLink)}</div>}
              </div>
            );
          })}
          <div
            className="sidebar__group"
            style={{
              borderTop: "1px solid var(--border)",
              marginTop: 12,
              paddingTop: 8,
            }}
          >
            {FOOTER_NAV.filter(canOpen).map(renderLink)}
          </div>
        </nav>

        <div className="sidebar__me">
          <span className="avatar" aria-hidden="true">
            {initialsOf(user?.username)}
          </span>
          <div className="sidebar__me-text">
            <div className="sidebar__me-name">{user?.username}</div>
            <div className="sidebar__me-role">{user?.roles[0] ?? ""}</div>
          </div>
          <IconButton
            icon={LogOut}
            label="Log out"
            tone="bad"
            onClick={() => setConfirmingLogout(true)}
          />
        </div>
      </aside>

      <ConfirmDialog
        isOpen={confirmingLogout}
        title="Log out"
        message={`Log out of ${SCHOOL_NAME}? You will need to sign in again.`}
        confirmLabel="Log out"
        onConfirm={handleLogout}
        onCancel={() => setConfirmingLogout(false)}
      />
    </>
  );
}
