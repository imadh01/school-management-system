import type { ReactNode } from "react";
import {
  Navigate,
  Route,
  createBrowserRouter,
  createRoutesFromElements,
} from "react-router-dom";
import { LoginPage } from "../features/authentication/pages/LoginPage";
import { ChangePasswordRequiredPage } from "../features/authentication/pages/ChangePasswordRequiredPage";
import { DashboardPage } from "../features/dashboard/pages/DashboardPage";
import { AdmissionsPage } from "../features/admissions/pages/AdmissionsPage";
import { AdmissionDetailPage } from "../features/admissions/pages/AdmissionDetailPage";
import { AdmissionFormPage } from "../features/admissions/pages/AdmissionFormPage";
import { StudentsPage } from "../features/students/pages/StudentsPage";
import { StudentDetailPage } from "../features/students/pages/StudentDetailPage";
import { StudentEditPage } from "../features/students/pages/StudentEditPage";
import { ParentsPage } from "../features/parents/pages/ParentsPage";
import { ParentDetailPage } from "../features/parents/pages/ParentDetailPage";
import { ParentFormPage } from "../features/parents/pages/ParentFormPage";
import { ClassSectionsPage } from "../features/class-sections/pages/ClassSectionsPage";
import { ClassSectionDetailPage } from "../features/class-sections/pages/ClassSectionDetailPage";
import { ClassSectionFormPage } from "../features/class-sections/pages/ClassSectionFormPage";
import { TeachersPage } from "../features/teachers/pages/TeachersPage";
import { TeacherDetailPage } from "../features/teachers/pages/TeacherDetailPage";
import { TeacherFormPage } from "../features/teachers/pages/TeacherFormPage";
import { SubjectsPage } from "../features/subjects/pages/SubjectsPage";
import { SubjectFormPage } from "../features/subjects/pages/SubjectFormPage";
import { CHANGE_PASSWORD_PATH, ProtectedRoute } from "./ProtectedRoute";
import { RequirePermission } from "./RequirePermission";
import {
  PAGE_PERMISSIONS,
  Permission,
  UNBUILT_MODULES_PERMISSIONS,
} from "./permissions";
import { AttendancePage } from "../features/attendance/pages/AttendancePage";
import { AppLayout } from "../layouts/AppLayout";
import { ALL_NAV_ITEMS } from "../layouts/navConfig";
import { ComingSoonPage } from "../components/ComingSoonPage";

/** A page behind the permissions listed for its path in PAGE_PERMISSIONS (F1). */
function guarded(
  path: string,
  page: ReactNode,
  anyOf = PAGE_PERMISSIONS[path],
) {
  return (
    <Route
      key={path}
      path={path}
      element={<RequirePermission anyOf={anyOf}>{page}</RequirePermission>}
    />
  );
}

/**
 * The usual pages of one record type (U2): list, add, the record's own page and edit.
 *   /parents            list    → PAGE_PERMISSIONS["/parents"]
 *   /parents/new        add     → the Create permission
 *   /parents/:id        detail  → same as the list (left out when the module has no detail page)
 *   /parents/:id/edit   edit    → the Edit permission
 * "new" is matched before ":id" by the router, so the order here does not matter.
 */
function recordRoutes(
  path: string,
  pages: { list: ReactNode; detail?: ReactNode; form: ReactNode },
  permissions: { create: string; edit: string },
) {
  return [
    guarded(path, pages.list),
    guarded(`${path}/new`, pages.form, [permissions.create]),
    ...(pages.detail
      ? [guarded(`${path}/:id`, pages.detail, PAGE_PERMISSIONS[path])]
      : []),
    guarded(`${path}/:id/edit`, pages.form, [permissions.edit]),
  ];
}

/*
 * U2: a "data router" (createBrowserRouter) instead of <BrowserRouter>. It routes the same way;
 * the difference is that it supports useBlocker, which the add/edit pages use to ask before
 * leaving with unsaved changes. The routes are built once, when the app starts.
 */
export const router = createBrowserRouter(
  createRoutesFromElements(
    <>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route
          path={CHANGE_PASSWORD_PATH}
          element={<ChangePasswordRequiredPage />}
        />
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          {recordRoutes(
            "/admissions",
            {
              list: <AdmissionsPage />,
              detail: <AdmissionDetailPage />,
              form: <AdmissionFormPage />,
            },
            {
              create: Permission.AdmissionsCreate,
              edit: Permission.AdmissionsEdit,
            },
          )}
          {guarded("/students", <StudentsPage />)}
          {guarded(
            "/students/:id",
            <StudentDetailPage />,
            PAGE_PERMISSIONS["/students"],
          )}
          {guarded("/students/:id/edit", <StudentEditPage />, [
            Permission.StudentsEdit,
          ])}
          {recordRoutes(
            "/parents",
            {
              list: <ParentsPage />,
              detail: <ParentDetailPage />,
              form: <ParentFormPage />,
            },
            { create: Permission.ParentsCreate, edit: Permission.ParentsEdit },
          )}
          {recordRoutes(
            "/classes-sections",
            {
              list: <ClassSectionsPage />,
              detail: <ClassSectionDetailPage />,
              form: <ClassSectionFormPage />,
            },
            {
              create: Permission.ClassSectionsCreate,
              edit: Permission.ClassSectionsEdit,
            },
          )}
          {recordRoutes(
            "/subjects",
            { list: <SubjectsPage />, form: <SubjectFormPage /> },
            {
              create: Permission.SubjectsCreate,
              edit: Permission.SubjectsEdit,
            },
          )}
          {recordRoutes(
            "/teachers",
            {
              list: <TeachersPage />,
              detail: <TeacherDetailPage />,
              form: <TeacherFormPage />,
            },
            {
              create: Permission.TeachersCreate,
              edit: Permission.TeachersEdit,
            },
          )}
          {guarded("/attendance", <AttendancePage />)}
          {/* Modules from the prototype that are not built yet. */}
          {ALL_NAV_ITEMS.filter((i) => !i.built).map((i) =>
            guarded(i.path, <ComingSoonPage />, UNBUILT_MODULES_PERMISSIONS),
          )}
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </>,
  ),
);
