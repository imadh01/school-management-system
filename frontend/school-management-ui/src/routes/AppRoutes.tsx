import type { ReactNode } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import { LoginPage } from "../features/authentication/pages/LoginPage";
import { ChangePasswordRequiredPage } from "../features/authentication/pages/ChangePasswordRequiredPage";
import { DashboardPage } from "../features/dashboard/pages/DashboardPage";
import { AdmissionsPage } from "../features/admissions/pages/AdmissionsPage";
import { StudentsPage } from "../features/students/pages/StudentsPage";
import { ParentsPage } from "../features/parents/pages/ParentsPage";
import { ClassSectionsPage } from "../features/class-sections/pages/ClassSectionsPage";
import { TeachersPage } from "../features/teachers/pages/TeachersPage";
import { SubjectsPage } from "../features/subjects/pages/SubjectsPage";
import { CHANGE_PASSWORD_PATH, ProtectedRoute } from "./ProtectedRoute";
import { RequirePermission } from "./RequirePermission";
import { PAGE_PERMISSIONS } from "./permissions";
import { AttendancePage } from "../features/attendance/pages/AttendancePage";
import { AppLayout } from "../layouts/AppLayout";

/** A page behind the permissions listed for its path in PAGE_PERMISSIONS (F1). */
function guarded(path: string, page: ReactNode) {
  return (
    <Route
      path={path}
      element={<RequirePermission anyOf={PAGE_PERMISSIONS[path]}>{page}</RequirePermission>}
    />
  );
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path={CHANGE_PASSWORD_PATH} element={<ChangePasswordRequiredPage />} />
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          {guarded("/admissions", <AdmissionsPage />)}
          {guarded("/students", <StudentsPage />)}
          {guarded("/parents", <ParentsPage />)}
          {guarded("/classes-sections", <ClassSectionsPage />)}
          {guarded("/subjects", <SubjectsPage />)}
          {guarded("/teachers", <TeachersPage />)}
          {guarded("/attendance", <AttendancePage />)}
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
