import * as React from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  Banknote,
  ChevronLeft,
  ChevronRight,
  FileUp,
  LayoutDashboard,
  ListFilter,
  LogOut,
  Menu,
  Moon,
  Settings2,
  ShieldCheck,
  Sun,
  Trash2,
  Users,
  X,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { authApi } from '@/core/api/endpoints';
import { useAuthStore } from '@/core/stores/authStore';
import { cn } from '@/shared/lib/utils';
import { Permissions, type Permission } from '@/shared/types/auth';
import { Button } from '@/shared/components/ui/button';

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  permission: Permission;
}

interface NavGroup {
  label: string;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Overview',
    items: [{ to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, permission: Permissions.ReportsRead }],
  },
  {
    label: 'Statements',
    items: [
      { to: '/statements', label: 'Statement Upload', icon: FileUp, permission: Permissions.StatementsRead },
      { to: '/statements/recycle-bin', label: 'Recycle Bin', icon: Trash2, permission: Permissions.StatementsDelete },
    ],
  },
  {
    label: 'Audit',
    items: [
      { to: '/transactions', label: 'Transaction Explorer', icon: ListFilter, permission: Permissions.TransactionsRead },
      { to: '/persons', label: 'Person Audit', icon: Users, permission: Permissions.PersonsRead },
    ],
  },
  {
    label: 'Administration',
    items: [{ to: '/masters', label: 'Master Management', icon: Settings2, permission: Permissions.MastersRead }],
  },
];

const SIDEBAR_KEY = 'financeaudit360.sidebar';

const ALL_NAV_ITEMS = NAV_GROUPS.flatMap((group) => group.items);

function initialsOf(name: string | undefined) {
  if (!name) return 'U';
  const parts = name.trim().split(/\s+/);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || 'U';
}

export function AppLayout() {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const { user, refreshToken, clear, can } = useAuthStore();
  const [sidebarOpen, setSidebarOpen] = React.useState(false);
  const [collapsed, setCollapsed] = React.useState(() => localStorage.getItem(SIDEBAR_KEY) === 'collapsed');

  const toggleCollapsed = () => {
    setCollapsed((current) => {
      const next = !current;
      localStorage.setItem(SIDEBAR_KEY, next ? 'collapsed' : 'expanded');
      return next;
    });
  };
  const [dark, setDark] = React.useState(() => document.documentElement.classList.contains('dark'));

  const toggleTheme = () => {
    const next = !dark;
    setDark(next);
    document.documentElement.classList.toggle('dark', next);
    localStorage.setItem('financeaudit360.theme', next ? 'dark' : 'light');
  };

  const signOut = async () => {
    try {
      if (refreshToken) await authApi.logout(refreshToken);
    } finally {
      clear();
      navigate('/login', { replace: true });
    }
  };

  const visibleGroups = NAV_GROUPS.map((group) => ({
    label: group.label,
    items: group.items.filter((item) => can(item.permission)),
  })).filter((group) => group.items.length > 0);

  const currentPage = ALL_NAV_ITEMS.find((item) => item.to === pathname)?.label ?? 'Workspace';
  const roleLabel = user?.roles[0] ?? 'Member';

  return (
    <div className="flex min-h-screen">
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-72 shrink-0 flex-col border-r border-slate-200/70 bg-white/80 shadow-2xl shadow-slate-900/5 backdrop-blur-md transition-[transform,width] duration-200 dark:border-slate-700/60 dark:bg-slate-950/75 lg:sticky lg:top-0 lg:h-screen lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
          collapsed && 'lg:w-20',
        )}
      >
        <button
          type="button"
          onClick={toggleCollapsed}
          className="absolute -right-3.5 top-7 z-50 hidden h-7 w-7 place-items-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-md transition-colors hover:border-indigo-300 hover:text-indigo-600 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300 dark:hover:text-white lg:grid"
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          aria-expanded={!collapsed}
          title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
        >
          {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </button>

        <div
          className={cn(
            'flex h-20 items-center gap-3 border-b border-slate-200/70 px-5 dark:border-slate-700/60',
            collapsed && 'lg:justify-center lg:px-0',
          )}
        >
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-emerald-600 text-white shadow-lg shadow-indigo-600/20">
            <Banknote className="h-6 w-6" aria-hidden="true" />
          </div>
          <div className={cn('min-w-0', collapsed && 'lg:hidden')}>
            <p className="font-['Manrope'] text-lg font-extrabold tracking-tight text-slate-950 dark:text-white">FinanceAudit360</p>
            <p className="text-xs font-bold text-slate-500 dark:text-slate-400">Statement Audit Command</p>
          </div>
          <button
            type="button"
            className="ml-auto rounded-xl p-2 text-slate-500 hover:bg-slate-100 lg:hidden dark:hover:bg-slate-800"
            onClick={() => setSidebarOpen(false)}
            aria-label="Close navigation"
          >
            <X className="h-[18px] w-[18px]" />
          </button>
        </div>

        <nav className="scrollbar-thin flex-1 overflow-y-auto overflow-x-hidden px-3 py-5" aria-label="Main navigation">
          {visibleGroups.map((group) => (
            <div className="mb-6" key={group.label}>
              <p className={cn('mb-2 px-3 text-[11px] font-black uppercase tracking-wider text-slate-400', collapsed && 'lg:hidden')}>
                {group.label}
              </p>
              {collapsed && <div className="mx-auto mb-2 hidden h-px w-8 bg-slate-200 dark:bg-slate-700 lg:block" aria-hidden="true" />}
              {group.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  // Exact matching so "Statement Upload" does not also light up on /statements/recycle-bin.
                  end
                  onClick={() => setSidebarOpen(false)}
                  title={collapsed ? item.label : undefined}
                  aria-label={item.label}
                  className={({ isActive }) =>
                    cn(
                      'group mb-1 flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-bold transition-all hover:scale-[1.02]',
                      collapsed && 'lg:justify-center lg:px-0',
                      isActive
                        ? 'bg-gradient-to-r from-indigo-600 to-emerald-600 text-white shadow-lg shadow-indigo-600/20'
                        : 'text-slate-600 hover:bg-slate-100 hover:text-indigo-700 dark:text-slate-300 dark:hover:bg-slate-800/80 dark:hover:text-white',
                    )
                  }
                >
                  <item.icon className="h-[18px] w-[18px] shrink-0" strokeWidth={2} aria-hidden="true" />
                  <span className={cn('truncate', collapsed && 'lg:hidden')}>{item.label}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="border-t border-slate-200/70 p-3 dark:border-slate-700/60">
          <div
            className={cn(
              'mb-3 rounded-xl border border-emerald-200/70 bg-gradient-to-br from-emerald-50 to-indigo-50 p-3 dark:border-emerald-500/20 dark:from-emerald-950/30 dark:to-indigo-950/30',
              collapsed && 'lg:hidden',
            )}
          >
            <div className="flex items-center gap-2 text-xs font-black text-emerald-700 dark:text-emerald-300">
              <ShieldCheck className="h-3.5 w-3.5" aria-hidden="true" />
              Secure workspace
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">Every action is permission-checked and audited.</p>
          </div>
          <div
            className={cn(
              'flex items-center gap-3 rounded-xl bg-slate-100/80 p-3 dark:bg-slate-800/70',
              collapsed && 'lg:flex-col lg:gap-2 lg:px-0 lg:py-2',
            )}
          >
            <span
              className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-gradient-to-br from-indigo-600 to-emerald-600 text-xs font-bold text-white"
              title={collapsed ? `${user?.fullName ?? ''} (${roleLabel})` : undefined}
            >
              {initialsOf(user?.fullName)}
            </span>
            <div className={cn('min-w-0 flex-1', collapsed && 'lg:hidden')}>
              <strong className="block truncate text-sm text-slate-900 dark:text-white">{user?.fullName}</strong>
              <span className="block truncate text-xs text-slate-500 dark:text-slate-400" title={user?.email}>
                {roleLabel}
              </span>
            </div>
            <button
              type="button"
              onClick={signOut}
              className="rounded-lg p-2 text-slate-500 transition-colors hover:bg-white hover:text-rose-600 dark:hover:bg-slate-900"
              aria-label="Sign out"
              title="Sign out"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </div>
        </div>
      </aside>

      {sidebarOpen && (
        <div
          className="fixed inset-0 z-30 bg-slate-900/40 backdrop-blur-sm lg:hidden"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-20 items-center gap-4 border-b border-slate-200/70 bg-white/80 px-4 backdrop-blur-md dark:border-slate-700/60 dark:bg-slate-950/70 lg:px-8">
          <Button variant="ghost" size="icon" className="lg:hidden" onClick={() => setSidebarOpen(true)} aria-label="Open navigation">
            <Menu className="h-5 w-5" />
          </Button>
          <div className="min-w-0">
            <p className="text-[11px] font-black uppercase tracking-wider text-slate-400">FinanceAudit360</p>
            <p className="truncate font-['Manrope'] text-base font-extrabold text-slate-900 dark:text-white">{currentPage}</p>
          </div>
          <div className="flex-1" />
          <Button variant="outline" size="icon" onClick={toggleTheme} aria-label="Toggle theme">
            {dark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
          </Button>
        </header>

        <main className="scrollbar-thin flex-1 px-5 pb-12 pt-8 sm:px-8 xl:px-11">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
