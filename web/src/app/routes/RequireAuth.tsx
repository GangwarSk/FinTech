import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/core/stores/authStore';
import { EmptyState } from '@/shared/components/common';
import type { Permission } from '@/shared/types/auth';

/**
 * Client-side gate only. The API enforces the same permission on every endpoint, so a tampered
 * bundle cannot read or write anything it is not entitled to.
 */
export function RequireAuth({ children, permission }: { children: React.ReactNode; permission?: Permission }) {
  const location = useLocation();
  const { isAuthenticated, can } = useAuthStore();

  if (!isAuthenticated()) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (permission && !can(permission)) {
    return (
      <EmptyState
        title="Access denied"
        message="Your account does not have permission to view this page. Ask an administrator for access."
      />
    );
  }

  return <>{children}</>;
}
