import { useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import { Sidebar } from "./Sidebar";
import { Header } from "./Header";
import { Footer } from "./Footer";

export function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const location = useLocation();

  // Close the drawer after navigating (narrow screens). Adjusting state during render when the
  // path changes avoids an extra effect pass (React's "storing information from previous renders").
  const [lastPath, setLastPath] = useState(location.pathname);
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname);
    setMenuOpen(false);
  }

  return (
    <div className="app-shell">
      <Sidebar isOpen={menuOpen} onClose={() => setMenuOpen(false)} />
      <div className="main">
        <Header onOpenMenu={() => setMenuOpen(true)} />
        <main className="content" id="main-content">
          <Outlet />
        </main>
        <Footer />
      </div>
    </div>
  );
}
