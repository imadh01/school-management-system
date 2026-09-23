import { useLocation } from "react-router-dom";
import { findNavLabel } from "./navConfig";
import { useAuth } from "../store/AuthContext";

export function Header() {
  const location = useLocation();
  const { user } = useAuth();
  const title = findNavLabel(location.pathname);

  return (
    <header className="topbar">
      <div className="topbar__title">
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        >
          <path d="M21 12a9 9 0 1 1-2.64-6.36L21 8" />
          <path d="M21 3v5h-5" />
        </svg>
        {title}
      </div>
      <div className="topbar__right">
        <span>Welcome, {user?.username}</span>
      </div>
    </header>
  );
}
