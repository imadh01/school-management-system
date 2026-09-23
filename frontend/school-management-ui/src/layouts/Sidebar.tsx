import { NavLink, useNavigate } from "react-router-dom";
import { NAV_SECTIONS, FOOTER_NAV } from "./navConfig";
import { useAuth } from "../store/AuthContext";
import { useDarkMode } from "../hooks/useDarkMode";
import brandMark from "../assets/synergein-mark.jpg";

// TODO: pull from a real School/Settings entity once that module exists —
// hardcoded here only because no such module has been built yet.
const SCHOOL_NAME = "Demo School";

export function Sidebar() {
  const { user, logout } = useAuth();
  const { isDark, toggle } = useDarkMode();
  const navigate = useNavigate();

  const initials = user?.username.charAt(0).toUpperCase() ?? "?";
  const primaryRole = user?.roles[0] ?? "";

  const handleLogout = () => {
    if (window.confirm(`Log out of ${SCHOOL_NAME}?`)) {
      logout();
      navigate("/login");
    }
  };

  const linkClass = ({ isActive }: { isActive: boolean }) =>
    isActive ? "sidebar__link sidebar__link--active" : "sidebar__link";

  return (
    <aside className="sidebar">
      <div className="sidebar__brand">
        <div className="sidebar__brand-mark">
          <img src={brandMark} alt="Synergein" />
        </div>
        <div>
          <div className="sidebar__brand-name">synergein</div>
          <div className="sidebar__brand-sub">School ERP</div>
        </div>
      </div>

      <div className="sidebar__profile">
        <div className="sidebar__avatar">{initials}</div>
        <div>
          <div className="sidebar__profile-name">{user?.username}</div>
          <div className="sidebar__profile-role">{primaryRole}</div>
        </div>
      </div>

      <nav className="sidebar__nav">
        {NAV_SECTIONS.map((section) => (
          <div key={section.label}>
            <div className="sidebar__section-label">{section.label}</div>
            {section.items.map((item) => (
              <NavLink key={item.key} to={item.path} className={linkClass}>
                {item.label}
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      <div className="sidebar__footer">
        {FOOTER_NAV.map((item) => (
          <NavLink key={item.key} to={item.path} className={linkClass}>
            {item.label}
          </NavLink>
        ))}
      </div>

      <div className="sidebar__utility">
        <button
          type="button"
          className="sidebar__darkmode-btn"
          onClick={toggle}
        >
          {isDark ? "☀ Light Mode" : "☾ Dark Mode"}
        </button>
        <button
          type="button"
          className="sidebar__logout-btn"
          onClick={handleLogout}
        >
          ⏻ Logout
        </button>
      </div>
    </aside>
  );
}
