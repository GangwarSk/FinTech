import * as React from 'react';
import { AlertCircle, Inbox, Loader2, Search, SlidersHorizontal, X } from 'lucide-react';
import { cn, daysAgoIso, todayIso } from '@/shared/lib/utils';
import { Button } from './ui/button';
import { Dialog } from './ui/dialog';
import { Input, Label, Select } from './ui/primitives';

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <p className="eyebrow">FinanceAudit360</p>
        <h1 className="text-[clamp(1.65rem,2.5vw,2.25rem)] font-extrabold leading-tight tracking-tight text-slate-950 dark:text-white">{title}</h1>
        {description && <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

export function LoadingState({ label = 'Loading...', className }: { label?: string; className?: string }) {
  return (
    <div className={cn('flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground', className)}>
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
      <span role="status">{label}</span>
    </div>
  );
}

export function EmptyState({
  title = 'Nothing to show',
  message,
  action,
}: {
  title?: string;
  message?: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-14 text-center">
      <Inbox className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
      <div>
        <p className="text-sm font-medium">{title}</p>
        {message && <p className="mt-1 text-sm text-muted-foreground">{message}</p>}
      </div>
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-14 text-center">
      <AlertCircle className="h-8 w-8 text-destructive" aria-hidden="true" />
      <p className="max-w-md text-sm text-muted-foreground">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}

export function SearchBar({
  value,
  onChange,
  placeholder = 'Search...',
  className,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
}) {
  return (
    <div className={cn('relative', className)}>
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="pl-8 pr-8"
        aria-label={placeholder}
      />
      {value && (
        <button
          type="button"
          onClick={() => onChange('')}
          aria-label="Clear search"
          className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}

export interface DateRangeValue {
  from: string | null;
  to: string | null;
}

const PRESETS = [
  { label: 'Today', from: () => todayIso(), to: () => todayIso() },
  { label: 'Last 7 days', from: () => daysAgoIso(6), to: () => todayIso() },
  { label: 'Last 30 days', from: () => daysAgoIso(29), to: () => todayIso() },
  { label: 'Last 90 days', from: () => daysAgoIso(89), to: () => todayIso() },
];

export function DateRangePicker({
  value,
  onChange,
  label = 'Date range',
}: {
  value: DateRangeValue;
  onChange: (value: DateRangeValue) => void;
  label?: string;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <div className="flex flex-wrap items-center gap-2">
        <Input
          type="date"
          value={value.from ?? ''}
          max={value.to ?? undefined}
          onChange={(event) => onChange({ ...value, from: event.target.value || null })}
          aria-label={`${label} start`}
          className="w-40"
        />
        <span className="text-sm text-muted-foreground">to</span>
        <Input
          type="date"
          value={value.to ?? ''}
          min={value.from ?? undefined}
          onChange={(event) => onChange({ ...value, to: event.target.value || null })}
          aria-label={`${label} end`}
          className="w-40"
        />
      </div>
      <div className="flex flex-wrap gap-1.5">
        {PRESETS.map((preset) => (
          <Button
            key={preset.label}
            variant="outline"
            size="sm"
            onClick={() => onChange({ from: preset.from(), to: preset.to() })}
          >
            {preset.label}
          </Button>
        ))}
        <Button variant="ghost" size="sm" onClick={() => onChange({ from: null, to: null })}>
          Clear
        </Button>
      </div>
    </div>
  );
}

export interface MultiSelectOption {
  value: string;
  label: string;
  description?: string | null;
}

export function MultiSelect({
  options,
  selected,
  onChange,
  label,
  placeholder = 'Select...',
  maxHeight = 200,
}: {
  options: MultiSelectOption[];
  selected: string[];
  onChange: (values: string[]) => void;
  label: string;
  placeholder?: string;
  maxHeight?: number;
}) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const containerRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    if (!open) return;
    const onClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [open]);

  const filtered = React.useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? options.filter((option) => option.label.toLowerCase().includes(q)) : options;
  }, [options, query]);

  const toggle = (value: string) =>
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value]);

  return (
    <div className="space-y-2" ref={containerRef}>
      <Label>{label}</Label>
      <div className="relative">
        <button
          type="button"
          onClick={() => setOpen((prev) => !prev)}
          aria-expanded={open}
          aria-haspopup="listbox"
          className="flex h-9 w-full items-center justify-between rounded-md border border-input bg-card px-3 text-left text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          <span className={cn('truncate', selected.length === 0 && 'text-muted-foreground')}>
            {selected.length === 0
              ? placeholder
              : selected.length === 1
                ? (options.find((o) => o.value === selected[0])?.label ?? '1 selected')
                : `${selected.length} selected`}
          </span>
        </button>

        {open && (
          <div className="absolute z-30 mt-1 w-full rounded-md border bg-card p-2 shadow-lg">
            <Input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Filter..."
              className="mb-2 h-8"
              aria-label={`Filter ${label}`}
            />
            <ul role="listbox" className="scrollbar-thin overflow-y-auto" style={{ maxHeight }}>
              {filtered.length === 0 && <li className="px-2 py-3 text-sm text-muted-foreground">No matches</li>}
              {filtered.map((option) => (
                <li key={option.value}>
                  <label className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent">
                    <input
                      type="checkbox"
                      className="h-4 w-4 accent-primary"
                      checked={selected.includes(option.value)}
                      onChange={() => toggle(option.value)}
                    />
                    <span className="truncate">{option.label}</span>
                  </label>
                </li>
              ))}
            </ul>
            {selected.length > 0 && (
              <Button variant="ghost" size="sm" className="mt-1 w-full" onClick={() => onChange([])}>
                Clear selection
              </Button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export interface FilterChip {
  id: string;
  /** Short field name, e.g. "Bank". */
  label: string;
  /** Human-readable applied value, e.g. "ICICI, HDFC". */
  value: string;
  onRemove?: () => void;
}

/**
 * Compact filter bar for screens whose filter controls would otherwise take several rows.
 * The controls live in a dialog and are only committed on Apply, so the caller keeps a draft
 * copy of its filter state; the applied values stay visible as removable chips.
 */
export function FilterBar({
  title = 'Filters',
  description,
  activeCount = 0,
  chips = [],
  leading,
  trailing,
  children,
  onOpen,
  onApply,
  onClear,
  columns = 2,
}: {
  title?: string;
  description?: string;
  activeCount?: number;
  chips?: FilterChip[];
  leading?: React.ReactNode;
  trailing?: React.ReactNode;
  children: React.ReactNode;
  /** Called before the dialog opens so the caller can snapshot the applied filter into its draft. */
  onOpen?: () => void;
  onApply: () => void;
  onClear?: () => void;
  columns?: 1 | 2 | 3;
}) {
  const [open, setOpen] = React.useState(false);

  const apply = () => {
    onApply();
    setOpen(false);
  };

  const clear = () => {
    onClear?.();
    setOpen(false);
  };

  return (
    <div className="flex w-full flex-wrap items-center gap-2">
      {leading}

      <Button
        variant="outline"
        size="sm"
        onClick={() => {
          onOpen?.();
          setOpen(true);
        }}
        aria-haspopup="dialog"
        aria-expanded={open}
      >
        <SlidersHorizontal className="h-4 w-4" />
        {title}
        {activeCount > 0 && (
          <span className="grid h-5 min-w-[1.25rem] place-items-center rounded-full bg-indigo-600 px-1.5 text-[11px] font-black text-white">
            {activeCount}
          </span>
        )}
      </Button>

      {chips.map((chip) => (
        <span
          key={chip.id}
          className="inline-flex max-w-[16rem] items-center gap-1 rounded-full border border-indigo-200 bg-indigo-50/80 py-1 pl-2.5 pr-1 text-xs text-indigo-700 dark:border-indigo-500/40 dark:bg-indigo-500/10 dark:text-indigo-200"
        >
          <span className="truncate">
            <span className="font-bold">{chip.label}:</span> {chip.value}
          </span>
          {chip.onRemove && (
            <button
              type="button"
              onClick={chip.onRemove}
              aria-label={`Remove ${chip.label} filter`}
              title={`Remove ${chip.label} filter`}
              className="grid h-4 w-4 shrink-0 place-items-center rounded-full transition hover:bg-indigo-200 dark:hover:bg-indigo-500/30"
            >
              <X className="h-3 w-3" />
            </button>
          )}
        </span>
      ))}

      {activeCount > 0 && onClear && (
        <Button variant="ghost" size="sm" onClick={onClear}>
          <X className="h-3.5 w-3.5" />
          Clear all
        </Button>
      )}

      {trailing}

      <Dialog
        open={open}
        onClose={() => setOpen(false)}
        title={title}
        description={description}
        className="max-w-3xl"
        footer={
          <>
            {onClear && (
              <Button variant="ghost" onClick={clear}>
                Clear all
              </Button>
            )}
            <div className="flex-1" />
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={apply}>Apply filters</Button>
          </>
        }
      >
        <div
          className={cn(
            'grid gap-4',
            columns >= 2 && 'sm:grid-cols-2',
            columns === 3 && 'lg:grid-cols-3',
          )}
        >
          {children}
        </div>
      </Dialog>
    </div>
  );
}

export function BooleanSelect({
  label,
  value,
  onChange,
  trueLabel = 'Yes',
  falseLabel = 'No',
}: {
  label: string;
  value: boolean | null | undefined;
  onChange: (value: boolean | null) => void;
  trueLabel?: string;
  falseLabel?: string;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Select
        value={value === null || value === undefined ? '' : String(value)}
        onChange={(event) => onChange(event.target.value === '' ? null : event.target.value === 'true')}
        aria-label={label}
      >
        <option value="">Any</option>
        <option value="true">{trueLabel}</option>
        <option value="false">{falseLabel}</option>
      </Select>
    </div>
  );
}
