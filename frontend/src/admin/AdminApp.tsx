import { Link, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from '../components/ProtectedRoute';
import { ADMIN_ROLES, MODERATOR_UP, SUPER_ADMIN_ONLY } from '../context/roles';
import { AdminAuthLayout, AdminLayout } from './AdminLayout';
import { AdminLoginPage } from './AdminLoginPage';
import {
  AdminAdminsPage,
  AdminAppealsPage,
  AdminAuditLogsPage,
  AdminBansPage,
  AdminBoardsPage,
  AdminDashboardPage,
  AdminReportsPage,
  AdminThreadsPage,
  AdminUsersPage,
} from './AdminPages';
import './admin.css';

function AdminNotFound() {
  return (
    <div className="admin-page">
      <h2>Page not found</h2>
      <p><Link to="/admin">Back to overview</Link></p>
    </div>
  );
}

/**
 * Toàn bộ khu /admin/* (route gắn ở App.tsx, nạp lazy). Các path ở đây tương đối so với /admin.
 * Hoàn toàn không đi qua GlobalLayout / BoardLayout của người dùng.
 */
export default function AdminApp() {
  return (
    <Routes>
      <Route element={<AdminAuthLayout />}>
        <Route path="login" element={<AdminLoginPage />} />
      </Route>

      <Route element={<ProtectedRoute roles={ADMIN_ROLES} />}>
        <Route element={<AdminLayout />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="threads" element={<AdminThreadsPage />} />
          <Route path="reports" element={<AdminReportsPage />} />
          <Route element={<ProtectedRoute roles={MODERATOR_UP} fallback="/admin" />}>
            <Route path="boards" element={<AdminBoardsPage />} />
            <Route path="users" element={<AdminUsersPage />} />
            <Route path="bans" element={<AdminBansPage />} />
            <Route path="appeals" element={<AdminAppealsPage />} />
          </Route>
          <Route element={<ProtectedRoute roles={SUPER_ADMIN_ONLY} fallback="/admin" />}>
            <Route path="admins" element={<AdminAdminsPage />} />
            <Route path="audit-logs" element={<AdminAuditLogsPage />} />
          </Route>
          <Route path="*" element={<AdminNotFound />} />
        </Route>
      </Route>
    </Routes>
  );
}
