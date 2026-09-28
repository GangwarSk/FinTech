import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/core/api/endpoints';
import { daysAgoIso, formatDate, todayIso } from '@/shared/lib/utils';
import type { DashboardPeriod } from '@/shared/types/dashboard';
import { ErrorState, LoadingState, PageHeader } from '@/shared/components/common';
import { HighlightCard, SummaryCard } from '@/shared/components/summary-card';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, Input, Label } from '@/shared/components/ui/primitives';

const PERIODS: { value: DashboardPeriod; label: string }[] = [
  { value: 'Today', label: 'Today' },
  { value: 'Last7Days', label: 'Last 7 days' },
  { value: 'Last30Days', label: 'Last 30 days' },
  { value: 'Last90Days', label: 'Last 90 days' },
  { value: 'Custom', label: 'Custom' },
];

export function DashboardPage() {
  const [period, setPeriod] = React.useState<DashboardPeriod>('Last30Days');
  const [from, setFrom] = React.useState(daysAgoIso(29));
  const [to, setTo] = React.useState(todayIso());

  const isCustom = period === 'Custom';
  const rangeIsValid = !isCustom || (Boolean(from) && Boolean(to) && from <= to);

  const query = useQuery({
    queryKey: ['dashboard', period, isCustom ? from : null, isCustom ? to : null],
    queryFn: () => dashboardApi.summary(period, isCustom ? from : undefined, isCustom ? to : undefined),
    enabled: rangeIsValid,
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        description="Headline numbers for the selected period, each compared against the equivalent previous period."
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 p-4">
          <div className="flex flex-wrap gap-2">
            {PERIODS.map((option) => (
              <Button
                key={option.value}
                variant={period === option.value ? 'default' : 'outline'}
                size="sm"
                onClick={() => setPeriod(option.value)}
              >
                {option.label}
              </Button>
            ))}
          </div>

          {isCustom && (
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <Label htmlFor="dashboard-from">From</Label>
                <Input
                  id="dashboard-from"
                  type="date"
                  value={from}
                  max={to}
                  onChange={(event) => setFrom(event.target.value)}
                  className="w-40"
                />
              </div>
              <div>
                <Label htmlFor="dashboard-to">To</Label>
                <Input
                  id="dashboard-to"
                  type="date"
                  value={to}
                  min={from}
                  onChange={(event) => setTo(event.target.value)}
                  className="w-40"
                />
              </div>
            </div>
          )}

          {query.data && (
            <p className="ml-auto text-sm text-muted-foreground">
              {formatDate(query.data.from)} - {formatDate(query.data.to)} · compared with{' '}
              {formatDate(query.data.previousFrom)} - {formatDate(query.data.previousTo)}
            </p>
          )}
        </CardContent>
      </Card>

      {!rangeIsValid && (
        <ErrorState message="Select a valid custom date range - the start date must not be after the end date." />
      )}

      {query.isLoading && <LoadingState label="Loading dashboard..." />}

      {query.isError && (
        <ErrorState
          message={(query.error as Error).message ?? 'Unable to load the dashboard.'}
          onRetry={() => query.refetch()}
        />
      )}

      {query.data && (
        <>
          <section aria-label="Key performance indicators" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {query.data.cards.map((card) => (
              <SummaryCard key={card.key} card={card} />
            ))}
          </section>

          <section aria-label="Period highlights" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <HighlightCard
              title="Top spending card"
              name={query.data.topSpendingCard?.name}
              amount={query.data.topSpendingCard?.amount}
              count={query.data.topSpendingCard?.transactionCount}
            />
            <HighlightCard
              title="Top spending vendor"
              name={query.data.topSpendingVendor?.name}
              amount={query.data.topSpendingVendor?.amount}
              count={query.data.topSpendingVendor?.transactionCount}
            />
            <HighlightCard
              title="Most active bank"
              name={query.data.mostActiveBank?.name}
              amount={query.data.mostActiveBank?.amount}
              count={query.data.mostActiveBank?.transactionCount}
            />
            <HighlightCard
              title="Most active person"
              name={query.data.mostActivePerson?.name}
              amount={query.data.mostActivePerson?.amount}
              count={query.data.mostActivePerson?.transactionCount}
            />
          </section>
        </>
      )}
    </div>
  );
}
