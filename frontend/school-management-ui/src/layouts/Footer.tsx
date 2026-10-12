const SCHOOL_NAME = "Demo School"; // TODO (E2): from SchoolSettings, like Sidebar.tsx

export function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="app-footer">
      <span>
        © {year} {SCHOOL_NAME}
      </span>
      <span>v0.1.0</span>
    </footer>
  );
}
