import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResponse, CurrentUser, Permission } from '@/shared/types/auth';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  accessTokenExpiresOnUtc: string | null;
  user: CurrentUser | null;
  setSession: (session: AuthResponse) => void;
  setUser: (user: CurrentUser) => void;
  clear: () => void;
  isAuthenticated: () => boolean;
  can: (permission: Permission | string) => boolean;
  isInRole: (role: string) => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      accessTokenExpiresOnUtc: null,
      user: null,

      setSession: (session) =>
        set({
          accessToken: session.accessToken,
          refreshToken: session.refreshToken,
          accessTokenExpiresOnUtc: session.accessTokenExpiresOnUtc,
          user: session.user,
        }),

      setUser: (user) => set({ user }),

      clear: () =>
        set({ accessToken: null, refreshToken: null, accessTokenExpiresOnUtc: null, user: null }),

      isAuthenticated: () => Boolean(get().accessToken && get().user),

      // Administrators implicitly hold every permission, mirroring the server-side policy.
      can: (permission) => {
        const state = get();
        if (!state.user) return false;
        if (state.user.roles.includes('Administrator')) return true;
        return state.user.permissions.includes(permission);
      },

      isInRole: (role) => get().user?.roles.includes(role) ?? false,
    }),
    {
      name: 'financeaudit360.auth',
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        accessTokenExpiresOnUtc: state.accessTokenExpiresOnUtc,
        user: state.user,
      }),
    },
  ),
);
