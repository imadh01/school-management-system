import { Routes, Route, Navigate } from "react-router-dom";
import { LoginPage } from "../features/authentication/pages/LoginPage";
import { DashboardPage } from "../features/dashboard/pages/DashboardPage";
import { AdmissionsPage } from "../features/admissions/pages/AdmissionsPage";
import { ProtectedRoute } from "./ProtectedRoute";
import { AppLayout } from "../layouts/AppLayout";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/admissions" element={<AdmissionsPage />} />
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
