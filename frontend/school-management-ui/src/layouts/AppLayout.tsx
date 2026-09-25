import { Outlet } from "react-router-dom";
import { Sidebar } from "./Sidebar";
import { Header } from "./Header";
import { Footer } from "./Footer";

export function AppLayout() {
  return (
    <div className="app-shell">
      <Sidebar />
      <div className="main">
        <Header />
        <div className="content">
          <Outlet />
        </div>
        <Footer />
      </div>
    </div>
  );
}
