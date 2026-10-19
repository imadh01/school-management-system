const APP_NAME = "Escoule";

export function Footer() {
  const year = new Date().getFullYear();
  return (
    <footer className="app-footer">
      <span>
        © {year} {APP_NAME}
      </span>
      <span>v0.1.0</span>
    </footer>
  );
}
