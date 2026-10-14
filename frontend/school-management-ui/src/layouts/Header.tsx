import { Link, useLocation } from "react-router-dom";
import { House, Menu, Moon, Sun } from "lucide-react";
import { findNavEntry } from "./navConfig";
import { useAuth } from "../store/AuthContext";
import { useTheme } from "../store/ThemeContext";
import { IconButton } from "../components/IconButton";
import { initialsOf } from "@/utils/initials";
import { useSchoolBasics } from "@/hooks/useSchoolBasics";

interface Props {
  onOpenMenu: () => void;
}

/** U1 topbar: menu button (narrow screens), breadcrumb, theme switch, signed-in user. */
export function Header({ onOpenMenu }: Props) {
  const location = useLocation();
  const { user } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const school = useSchoolBasics();
  const entry = findNavEntry(location.pathname);
  const isNested = entry !== null && location.pathname !== entry.item.path;

  return (
    <header className="topbar">
      <IconButton
        icon={Menu}
        label="Open menu"
        size="lg"
        className="topbar__menu"
        aria-controls="app-sidebar"
        onClick={onOpenMenu}
      />

      <nav className="breadcrumb" aria-label="Breadcrumb">
        <ol>
          <li>
            <Link to="/dashboard" aria-label="Home">
              <House size={16} aria-hidden="true" />
            </Link>
          </li>
          {entry?.section && (
            <li>
              <span aria-hidden="true">›</span>
              <span>{entry.section}</span>
            </li>
          )}
          {entry && (
            <li>
              <span aria-hidden="true">›</span>
              {isNested ? (
                <Link to={entry.item.path}>{entry.item.label}</Link>
              ) : (
                <span aria-current="page">{entry.item.label}</span>
              )}
            </li>
          )}
        </ol>
      </nav>

      {school && (
        <span className="topbar__school" title={school.schoolName}>
          {school.shortName || school.schoolName}
        </span>
      )}

      <IconButton
        icon={theme === "dark" ? Sun : Moon}
        label={
          theme === "dark" ? "Switch to light theme" : "Switch to dark theme"
        }
        size="lg"
        onClick={toggleTheme}
      />

      <div className="topbar__user">
        <span className="avatar" aria-hidden="true">
          {initialsOf(user?.username)}
        </span>
        <div className="topbar__user-text">
          <div className="topbar__user-name">{user?.username}</div>
          <div className="topbar__user-role">{user?.roles[0] ?? ""}</div>
        </div>
      </div>
    </header>
  );
}
