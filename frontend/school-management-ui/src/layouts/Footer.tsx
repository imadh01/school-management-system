const SCHOOL_NAME = "Demo School"; // same TODO as Sidebar.tsx

export function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="app-footer">
      <span>
        © {year} {SCHOOL_NAME}. All rights reserved.
      </span>
      <span>v0.1.0</span>
    </footer>
  );
}
