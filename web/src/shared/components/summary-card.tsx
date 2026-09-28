import { Minus, TrendingDown, TrendingUp } from 'lucide-react';
import { cn, formatCurrency, formatNumber, formatPercent } from '@/shared/lib/utils';
import type { KpiCardDto } from '@/shared/types/dashboard';
import { Card, CardContent } from './ui/primitives';

const TREND_STYLES = {
  up: 'text-success',
  down: 'text-destructive',
  flat: 'text-muted-foreground',
} as const;

const TREND_ICONS = {
  up: TrendingUp,
  down: TrendingDown,
  flat: Minus,
} as const;

export function SummaryCard({ card }: { card: KpiCardDto }) {
  const Icon = TREND_ICONS[card.trend] ?? Minus;
  const format = card.valueFormat === 'currency' ? formatCurrency : formatNumber;
  const hasComparison = card.previousValue !== card.value;

  return (
    <Card className="transition-all duration-200 hover:-translate-y-0.5 hover:border-indigo-600/30">
      <CardContent className="p-5">
        <p className="text-sm font-medium text-muted-foreground">{card.title}</p>
        <p className="tabular mt-2 font-['Manrope'] text-2xl font-extrabold tracking-tight text-slate-950 dark:text-white">{format(card.value)}</p>

        <div className="mt-2 flex items-center gap-1.5 text-xs">
          <Icon className={cn('h-3.5 w-3.5', TREND_STYLES[card.trend])} aria-hidden="true" />
          <span className={cn('tabular font-medium', TREND_STYLES[card.trend])}>
            {card.difference >= 0 ? '+' : ''}
            {format(card.difference)}
          </span>
          {card.percentChange !== null && card.percentChange !== undefined && (
            <span className={cn('tabular', TREND_STYLES[card.trend])}>({formatPercent(card.percentChange)})</span>
          )}
          {hasComparison && <span className="text-muted-foreground">vs previous period</span>}
        </div>

        {card.caption && <p className="mt-2 text-xs text-muted-foreground">{card.caption}</p>}
      </CardContent>
    </Card>
  );
}

export function HighlightCard({
  title,
  name,
  amount,
  count,
  emptyLabel = 'No activity in this period',
}: {
  title: string;
  name?: string | null;
  amount?: number | null;
  count?: number | null;
  emptyLabel?: string;
}) {
  return (
    <Card>
      <CardContent className="p-5">
        <p className="text-sm font-medium text-muted-foreground">{title}</p>
        {name ? (
          <>
            <p className="mt-2 truncate text-lg font-semibold" title={name}>
              {name}
            </p>
            <p className="tabular mt-1 text-sm text-muted-foreground">
              {formatCurrency(amount)} · {formatNumber(count)} transactions
            </p>
          </>
        ) : (
          <p className="mt-2 text-sm text-muted-foreground">{emptyLabel}</p>
        )}
      </CardContent>
    </Card>
  );
}
