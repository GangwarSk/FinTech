import * as React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Navigate, Route, Routes } from 'react-router-dom';
import { configureApiClient, ApiRequestError } from '@/core/api/client';
import { useAuthStore } from '@/core/stores/authStore';
import { AppLayout } from '@/app/layouts/AppLayout';
import { RequireAuth } from '@/app/routes/RequireAuth';
import { LoginPage } from '@/features/auth/LoginPage';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { StatementUploadPage } from '@/features/statements/StatementUploadPage';
import { RecycleBinPage } from '@/features/statements/RecycleBinPage';
import { TransactionExplorerPage } from '@/features/transactions/TransactionExplorerPage';
import { PersonAuditPage } from '@/features/persons/PersonAuditPage';
import { MasterManagementPage } from '@/features/masters/MasterManagementPage';
import { EmptyState } from '@/shared/components/common';
import { Toaster } from '@/shared/components/toast';
import { Permissions } from '@/shared/types/auth';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // 401 and 4xx are not worth retrying - only transient server/network errors are.
      retry: (failureCount, error) =>
        error instanceof ApiRequestError && error.status < 500 ? false : failureCount < 2,
    },
  },
});

// Wiring lives outside React so the fetch layer can refresh tokens without a hook.
configureApiClient({
  getTokens: () => {
    const { accessToken, refreshToken } = useAuthStore.getState();
    return accessToken && refreshToken ? { accessToken, refreshToken } : null;
  },
  onRefreshed: (session) => useAuthStore.getState().setSession(session),
  onAuthFailed: () => useAuthStore.getState().clear(),
});

export function App() {
  React.useEffect(() => {
    if (localStorage.getItem('financeaudit360.theme') === 'dark') {
      document.documentElement.classList.add('dark');
    }
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          element={
            <RequireAuth>
              <AppLayout />
            </RequireAuth>
          }
        >
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route
            path="/dashboard"
            element={
              <RequireAuth permission={Permissions.ReportsRead}>
                <DashboardPage />
              </RequireAuth>
            }
          />
          <Route
            path="/statements"
            element={
              <RequireAuth permission={Permissions.StatementsRead}>
                <StatementUploadPage />
              </RequireAuth>
            }
          />
          <Route
            path="/statements/recycle-bin"
            element={
              <RequireAuth permission={Permissions.StatementsDelete}>
                <RecycleBinPage />
              </RequireAuth>
            }
          />
          <Route
            path="/transactions"
            element={
              <RequireAuth permission={Permissions.TransactionsRead}>
                <TransactionExplorerPage />
              </RequireAuth>
            }
          />
          <Route
            path="/persons"
            element={
              <RequireAuth permission={Permissions.PersonsRead}>
                <PersonAuditPage />
              </RequireAuth>
            }
          />
          <Route
            path="/masters"
            element={
              <RequireAuth permission={Permissions.MastersRead}>
                <MasterManagementPage />
              </RequireAuth>
            }
          />
        </Route>
        <Route
          path="*"
          element={<EmptyState title="Page not found" message="The page you requested does not exist." />}
        />
      </Routes>
      <Toaster />
    </QueryClientProvider>
  );
}
