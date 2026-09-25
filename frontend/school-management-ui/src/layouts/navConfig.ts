export interface NavItem {
  key: string;
  label: string;
  path: string;
}

export interface NavSection {
  label: string;
  items: NavItem[];
}

export const NAV_SECTIONS: NavSection[] = [
  {
    label: "Overview",
    items: [
      { key: "dashboard", label: "Dashboard", path: "/dashboard" },
      { key: "reports", label: "Reports", path: "/reports" },
    ],
  },
  {
    label: "Records",
    items: [
      { key: "admissions", label: "Admissions", path: "/admissions" },
      { key: "students", label: "Students", path: "/students" },
      {
        key: "classes-sections",
        label: "Classes & Sections",
        path: "/classes-sections",
      },
      { key: "parents", label: "Parents", path: "/parents" },
      { key: "assets", label: "Assets", path: "/assets" },
      {
        key: "stock-inventory",
        label: "Stock / Inventory",
        path: "/stock-inventory",
      },
      { key: "subjects", label: "Subjects", path: "/subjects" },
      { key: "teachers", label: "Teachers", path: "/teachers" },
    ],
  },
  {
    label: "Daily",
    items: [
      { key: "timetable", label: "Timetable", path: "/timetable" },
      { key: "attendance", label: "Attendance", path: "/attendance" },
      { key: "substitutes", label: "Substitutes", path: "/substitutes" },
      {
        key: "school-notices",
        label: "School Notices",
        path: "/school-notices",
      },
      { key: "ptm", label: "PTM", path: "/ptm" },
      { key: "calendar", label: "Calendar", path: "/calendar" },
      {
        key: "lesson-planning",
        label: "Lesson Planning",
        path: "/lesson-planning",
      },
      {
        key: "teaching-logbook",
        label: "Teaching Logbook",
        path: "/teaching-logbook",
      },
    ],
  },
  {
    label: "Academic",
    items: [
      { key: "exams", label: "Exams", path: "/exams" },
      {
        key: "results-marks",
        label: "Results / Marks",
        path: "/results-marks",
      },
      { key: "hall-tickets", label: "Hall Tickets", path: "/hall-tickets" },
      { key: "discipline", label: "Discipline", path: "/discipline" },
      { key: "conduct", label: "Conduct", path: "/conduct" },
      { key: "activities", label: "Activities", path: "/activities" },
    ],
  },
  {
    label: "Finance",
    items: [
      {
        key: "fees-collection",
        label: "Fees Collection",
        path: "/fees-collection",
      },
      {
        key: "daily-accounts",
        label: "Daily Accounts",
        path: "/daily-accounts",
      },
      { key: "fee-structure", label: "Fee Structure", path: "/fee-structure" },
    ],
  },
  {
    label: "Support",
    items: [
      { key: "helpdesk", label: "Helpdesk", path: "/helpdesk" },
      { key: "complaints", label: "Complaints", path: "/complaints" },
      { key: "documents", label: "Documents", path: "/documents" },
    ],
  },
  {
    label: "Administration",
    items: [
      { key: "users-staff", label: "Users / Staff", path: "/users-staff" },
    ],
  },
];

export const FOOTER_NAV: NavItem[] = [
  { key: "my-account", label: "My Account", path: "/my-account" },
  { key: "settings", label: "Settings", path: "/settings" },
  { key: "about-app", label: "About App", path: "/about-app" },
];

/** Looks up a nav item's label by path — used to derive the Topbar title,
 *  matching the prototype's own behavior where the topbar title always
 *  equals the active sidebar link's label. */
export function findNavLabel(pathname: string): string {
  const all = [...NAV_SECTIONS.flatMap((s) => s.items), ...FOOTER_NAV];
  return all.find((item) => item.path === pathname)?.label ?? "";
}
