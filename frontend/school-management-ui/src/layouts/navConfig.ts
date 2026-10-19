import type { LucideIcon } from "lucide-react";
import {
  Building,
  Calendar,
  CalendarCheck,
  CalendarDays,
  ChartColumn,
  ClipboardList,
  Contact,
  Database,
  FileText,
  FolderOpen,
  Gavel,
  GraduationCap,
  Handshake,
  Info,
  Landmark,
  LayoutDashboard,
  LibraryBig,
  LifeBuoy,
  Medal,
  Megaphone,
  MessageSquareWarning,
  NotebookPen,
  Package,
  Receipt,
  ShieldCheck,
  Repeat,
  ScrollText,
  Settings,
  Shapes,
  Sparkles,
  Ticket,
  Trophy,
  UserCog,
  UserRound,
  Users,
  Wallet,
} from "lucide-react";

export interface NavItem {
  key: string;
  label: string;
  path: string;
  icon: LucideIcon;
  /** False for modules from the prototype that are not built yet: they open a "coming soon" page. */
  built: boolean;
}

export interface NavSection {
  label: string;
  items: NavItem[];
}

export const NAV_SECTIONS: NavSection[] = [
  {
    label: "Overview",
    items: [
      {
        key: "dashboard",
        label: "Dashboard",
        path: "/dashboard",
        icon: LayoutDashboard,
        built: true,
      },
      {
        key: "reports",
        label: "Reports",
        path: "/reports",
        icon: ChartColumn,
        built: false,
      },
      {
        key: "masters",
        label: "Masters",
        path: "/masters",
        icon: Database,
        built: true,
      },
    ],
  },
  {
    label: "Records",
    items: [
      {
        key: "admissions",
        label: "Admissions",
        path: "/admissions",
        icon: FileText,
        built: true,
      },
      {
        key: "students",
        label: "Students",
        path: "/students",
        icon: GraduationCap,
        built: true,
      },
      {
        key: "parents",
        label: "Parents",
        path: "/parents",
        icon: Users,
        built: true,
      },
      {
        key: "classes-sections",
        label: "Classes & Sections",
        path: "/classes-sections",
        icon: Building,
        built: true,
      },
      {
        key: "subjects",
        label: "Subjects",
        path: "/subjects",
        icon: LibraryBig,
        built: true,
      },
      {
        key: "teachers",
        label: "Teachers",
        path: "/teachers",
        icon: UserRound,
        built: true,
      },
      {
        key: "assets",
        label: "Assets",
        path: "/assets",
        icon: Package,
        built: false,
      },
      {
        key: "stock-inventory",
        label: "Stock / Inventory",
        path: "/stock-inventory",
        icon: Shapes,
        built: false,
      },
    ],
  },
  {
    label: "Daily",
    items: [
      {
        key: "attendance",
        label: "Attendance",
        path: "/attendance",
        icon: CalendarCheck,
        built: true,
      },
      {
        key: "timetable",
        label: "Timetable",
        path: "/timetable",
        icon: Calendar,
        built: false,
      },
      {
        key: "substitutes",
        label: "Substitutes",
        path: "/substitutes",
        icon: Repeat,
        built: false,
      },
      {
        key: "school-notices",
        label: "School Notices",
        path: "/school-notices",
        icon: Megaphone,
        built: false,
      },
      { key: "ptm", label: "PTM", path: "/ptm", icon: Handshake, built: false },
      {
        key: "calendar",
        label: "Calendar",
        path: "/calendar",
        icon: CalendarDays,
        built: false,
      },
      {
        key: "lesson-planning",
        label: "Lesson Planning",
        path: "/lesson-planning",
        icon: NotebookPen,
        built: false,
      },
      {
        key: "teaching-logbook",
        label: "Teaching Logbook",
        path: "/teaching-logbook",
        icon: ClipboardList,
        built: false,
      },
    ],
  },
  {
    label: "Academic",
    items: [
      {
        key: "exams",
        label: "Exams",
        path: "/exams",
        icon: ScrollText,
        built: false,
      },
      {
        key: "results-marks",
        label: "Results / Marks",
        path: "/results-marks",
        icon: Trophy,
        built: false,
      },
      {
        key: "hall-tickets",
        label: "Hall Tickets",
        path: "/hall-tickets",
        icon: Ticket,
        built: false,
      },
      {
        key: "discipline",
        label: "Discipline",
        path: "/discipline",
        icon: Gavel,
        built: false,
      },
      {
        key: "conduct",
        label: "Conduct",
        path: "/conduct",
        icon: Medal,
        built: false,
      },
      {
        key: "activities",
        label: "Activities",
        path: "/activities",
        icon: Sparkles,
        built: false,
      },
    ],
  },
  {
    label: "Finance",
    items: [
      {
        key: "fees-collection",
        label: "Fees Collection",
        path: "/fees-collection",
        icon: Wallet,
        built: false,
      },
      {
        key: "daily-accounts",
        label: "Daily Accounts",
        path: "/daily-accounts",
        icon: Landmark,
        built: false,
      },
      {
        key: "fee-structure",
        label: "Fee Structure",
        path: "/fee-structure",
        icon: Receipt,
        built: false,
      },
    ],
  },
  {
    label: "Support",
    items: [
      {
        key: "helpdesk",
        label: "Helpdesk",
        path: "/helpdesk",
        icon: LifeBuoy,
        built: false,
      },
      {
        key: "complaints",
        label: "Complaints",
        path: "/complaints",
        icon: MessageSquareWarning,
        built: false,
      },
      {
        key: "documents",
        label: "Documents",
        path: "/documents",
        icon: FolderOpen,
        built: true,
      },
    ],
  },
  {
    label: "Administration",
    items: [
      {
        key: "users-staff",
        label: "Users / Staff",
        path: "/users-staff",
        icon: UserCog,
        built: true,
      },
      {
        key: "roles-permissions",
        label: "Roles & Permissions",
        path: "/roles-permissions",
        icon: ShieldCheck,
        built: true,
      },
    ],
  },
];

export const FOOTER_NAV: NavItem[] = [
  {
    key: "my-account",
    label: "My Account",
    path: "/my-account",
    icon: Contact,
    built: true,
  },
  {
    key: "settings",
    label: "Settings",
    path: "/settings",
    icon: Settings,
    built: true,
  },
  {
    key: "about-app",
    label: "About App",
    path: "/about-app",
    icon: Info,
    built: false,
  },
];

export const ALL_NAV_ITEMS: NavItem[] = [
  ...NAV_SECTIONS.flatMap((s) => s.items),
  ...FOOTER_NAV,
];

/**
 * The nav item (and its section) that a path belongs to. "/students/12/edit" belongs to
 * Students, so the breadcrumb and the active link follow nested pages too.
 */
export function findNavEntry(
  pathname: string,
): { section: string | null; item: NavItem } | null {
  for (const section of NAV_SECTIONS) {
    const item = section.items.find(
      (i) => pathname === i.path || pathname.startsWith(i.path + "/"),
    );
    if (item) return { section: section.label, item };
  }
  const footer = FOOTER_NAV.find(
    (i) => pathname === i.path || pathname.startsWith(i.path + "/"),
  );
  return footer ? { section: null, item: footer } : null;
}

/** Kept for older callers: the label of the page a path belongs to. */
export function findNavLabel(pathname: string): string {
  return findNavEntry(pathname)?.item.label ?? "";
}
