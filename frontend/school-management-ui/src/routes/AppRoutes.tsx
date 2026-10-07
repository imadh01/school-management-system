import { Routes, Route, Navigate } from "react-router-dom";
import { LoginPage } from "../features/authentication/pages/LoginPage";
import { DashboardPage } from "../features/dashboard/pages/DashboardPage";
import { AdmissionsPage } from "../features/admissions/pages/AdmissionsPage";
import { StudentsPage } from "../features/students/pages/StudentsPage";
import { ParentsPage } from "../features/parents/pages/ParentsPage";
import { ClassSectionsPage } from "../features/class-sections/pages/ClassSectionsPage";
import { TeachersPage } from "../features/teachers/pages/TeachersPage";
import { SubjectsPage } from "../features/subjects/pages/SubjectsPage";
import { ProtectedRoute } from "./ProtectedRoute";
import { AttendancePage } from "../features/attendance/pages/AttendancePage";
import { AppLayout } from "../layouts/AppLayout";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/admissions" element={<AdmissionsPage />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/parents" element={<ParentsPage />} />
          <Route path="/classes-sections" element={<ClassSectionsPage />} />
          <Route path="/subjects" element={<SubjectsPage />} />
          <Route path="/teachers" element={<TeachersPage />} />
          <Route path="/attendance" element={<AttendancePage />} />
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
