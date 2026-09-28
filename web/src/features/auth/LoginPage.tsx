import * as React from 'react';
import { useNavigate, useLocation, Navigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowRight, Banknote, Eye, EyeOff, Loader2, LockKeyhole, Mail, ShieldCheck, Sparkles } from 'lucide-react';
import { ApiRequestError } from '@/core/api/client';
import { authApi } from '@/core/api/endpoints';
import { useAuthStore } from '@/core/stores/authStore';
import { cn } from '@/shared/lib/utils';
import { FieldError } from '@/shared/components/ui/primitives';

const schema = z.object({
  userNameOrEmail: z.string().min(1, 'Enter your user name or email.'),
  password: z.string().min(1, 'Enter your password.'),
});

type LoginForm = z.infer<typeof schema>;

const HIGHLIGHTS = [
  ['PDF + Excel', 'statements'],
  ['Every', 'transaction'],
  ['Full', 'audit trail'],
] as const;

const fieldShell = (invalid: boolean) =>
  cn(
    'group flex items-center gap-3 rounded-2xl border bg-slate-50 px-3.5 shadow-sm transition-all focus-within:border-indigo-400 focus-within:bg-white focus-within:shadow-md focus-within:ring-4 focus-within:ring-indigo-100 dark:bg-slate-800/60 dark:focus-within:bg-slate-900 dark:focus-within:ring-indigo-500/20',
    invalid ? 'border-rose-400' : 'border-slate-200 dark:border-slate-700',
  );

const fieldIcon =
  'flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-indigo-50 text-indigo-500 transition-colors group-focus-within:bg-indigo-600 group-focus-within:text-white dark:bg-indigo-500/10 dark:text-indigo-300';

const fieldInput =
  'w-full border-0 bg-transparent px-0 py-3 text-sm font-semibold text-slate-900 placeholder:font-medium placeholder:text-slate-400 focus:outline-none focus:ring-0 dark:text-white dark:placeholder:text-slate-500';

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const { setSession, isAuthenticated } = useAuthStore();
  const [serverError, setServerError] = React.useState<string | null>(null);
  const [showPassword, setShowPassword] = React.useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema) });

  if (isAuthenticated()) {
    return <Navigate to="/dashboard" replace />;
  }

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      const session = await authApi.login(values.userNameOrEmail, values.password);
      setSession(session);
      const from = (location.state as { from?: string } | null)?.from ?? '/dashboard';
      navigate(from, { replace: true });
    } catch (error) {
      setServerError(
        error instanceof ApiRequestError ? error.message : 'Unable to sign in. Please try again.',
      );
    }
  });

  return (
    <div className="relative flex min-h-screen w-full items-center justify-center overflow-hidden bg-gradient-to-br from-indigo-50 via-white to-emerald-50 px-4 py-24 dark:from-slate-950 dark:via-slate-900 dark:to-emerald-950/40">
      <div className="absolute left-8 top-8 flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-emerald-600 text-white shadow-lg shadow-indigo-600/20">
          <Banknote className="h-6 w-6" aria-hidden="true" />
        </div>
        <div>
          <p className="font-['Manrope'] text-lg font-extrabold text-slate-950 dark:text-white">FinanceAudit360</p>
          <p className="text-xs font-bold text-slate-500 dark:text-slate-400">Statement Audit Command</p>
        </div>
      </div>

      <div className="grid w-full max-w-5xl items-center gap-8 lg:grid-cols-[1fr_440px]">
        <div className="hidden lg:block">
          <div className="inline-flex items-center gap-2 rounded-full border border-emerald-200 bg-white/70 px-4 py-2 text-xs font-black uppercase tracking-wider text-emerald-700 shadow-sm backdrop-blur-md dark:border-emerald-500/20 dark:bg-slate-900/70 dark:text-emerald-300">
            <Sparkles className="h-[15px] w-[15px]" aria-hidden="true" /> Financial audit workspace
          </div>
          <h1 className="mt-6 max-w-xl font-['Manrope'] text-5xl font-extrabold leading-tight tracking-tight text-slate-950 dark:text-white">
            One command center for every bank and card statement.
          </h1>
          <p className="mt-5 max-w-lg text-base font-medium leading-7 text-slate-600 dark:text-slate-300">
            Upload statements, search and audit every transaction, and reconcile people, vendors and cards from a single dashboard.
          </p>
          <div className="mt-8 grid max-w-xl grid-cols-3 gap-3">
            {HIGHLIGHTS.map(([value, label]) => (
              <div
                key={label}
                className="rounded-xl border border-white/70 bg-white/70 p-4 shadow-sm backdrop-blur-md dark:border-slate-700/60 dark:bg-slate-900/70"
              >
                <p className="font-['Manrope'] text-2xl font-extrabold text-slate-950 dark:text-white">{value}</p>
                <p className="mt-1 text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">{label}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-xl border border-white/70 bg-white/80 p-6 shadow-2xl shadow-slate-900/10 backdrop-blur-md dark:border-slate-700/70 dark:bg-slate-900/80 sm:p-8">
          <div className="mb-7">
            <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-xl bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-300">
              <ShieldCheck className="h-6 w-6" aria-hidden="true" />
            </div>
            <h2 className="font-['Manrope'] text-3xl font-extrabold tracking-tight text-slate-950 dark:text-white">Welcome back</h2>
            <p className="mt-2 text-sm font-medium text-slate-500 dark:text-slate-400">
              Sign in to search and audit every statement in one place.
            </p>
          </div>

          {serverError && (
            <div
              role="alert"
              className="mb-4 rounded-xl border border-rose-200 bg-rose-50/90 p-4 text-sm font-semibold text-rose-700 dark:border-rose-500/20 dark:bg-rose-950/30 dark:text-rose-300"
            >
              {serverError}
            </div>
          )}

          <form onSubmit={onSubmit} className="space-y-4" noValidate>
            <div>
              <label htmlFor="userNameOrEmail" className="mb-1.5 block text-sm font-bold text-slate-700 dark:text-slate-200">
                User name or email
              </label>
              <div className={fieldShell(Boolean(errors.userNameOrEmail))}>
                <span className={fieldIcon}>
                  <Mail className="h-4 w-4" aria-hidden="true" />
                </span>
                <input
                  id="userNameOrEmail"
                  autoComplete="username"
                  autoFocus
                  placeholder="admin@financeaudit360.com"
                  aria-invalid={Boolean(errors.userNameOrEmail)}
                  className={fieldInput}
                  {...register('userNameOrEmail')}
                />
              </div>
              <FieldError message={errors.userNameOrEmail?.message} />
            </div>

            <div>
              <label htmlFor="password" className="mb-1.5 block text-sm font-bold text-slate-700 dark:text-slate-200">
                Password
              </label>
              <div className={fieldShell(Boolean(errors.password))}>
                <span className={fieldIcon}>
                  <LockKeyhole className="h-4 w-4" aria-hidden="true" />
                </span>
                <input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Enter password"
                  aria-invalid={Boolean(errors.password)}
                  className={fieldInput}
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((prev) => !prev)}
                  className="shrink-0 rounded-lg p-1.5 text-slate-400 transition-colors hover:bg-slate-200/60 hover:text-slate-600 dark:hover:bg-slate-700/60 dark:hover:text-slate-200"
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  tabIndex={-1}
                >
                  {showPassword ? <EyeOff className="h-[17px] w-[17px]" /> : <Eye className="h-[17px] w-[17px]" />}
                </button>
              </div>
              <FieldError message={errors.password?.message} />
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="group flex w-full items-center justify-between rounded-xl bg-gradient-to-r from-indigo-600 to-emerald-600 px-4 py-3 text-sm font-black text-white shadow-lg shadow-indigo-600/20 transition-all hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span>{isSubmitting ? 'Please wait...' : 'Sign in securely'}</span>
              {isSubmitting ? (
                <Loader2 className="h-[18px] w-[18px] animate-spin" aria-hidden="true" />
              ) : (
                <ArrowRight className="h-[18px] w-[18px] transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
