import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { type UserRole } from '../context/auth-context';
import { useAuth } from '../context/useAuth';

export function ProtectedRoute({ roles, fallback = '/' }: { roles: UserRole[]; fallback?: string }) {
  const { user, ready } = useAuth();
  const location = useLocation();

  if (!ready) return <div className="session-loading" role="status">Restoring session…</div>;
  if (!user) return <Navigate to={location.pathname.startsWith('/admin') ? '/admin/login' : '/login'} replace state={{ from: location }} />;
  if (!roles.includes(user.role)) return <Navigate to={fallback} replace />;
  return <Outlet />;
}