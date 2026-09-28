import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SummaryCard } from '@/shared/components/summary-card';
import { EmptyState, ErrorState, SearchBar } from '@/shared/components/common';
import { DataTable } from '@/shared/components/data-table';
import type { KpiCardDto } from '@/shared/types/dashboard';

describe('SummaryCard', () => {
  const baseCard: KpiCardDto = {
    key: 'totalDebit',
    title: 'Total Debit',
    value: 45320.75,
    valueFormat: 'currency',
    previousValue: 40000,
    difference: 5320.75,
    percentChange: 13.3,
    trend: 'down',
  };

  it('renders the title, value and comparison', () => {
    render(<SummaryCard card={baseCard} />);

    expect(screen.getByText('Total Debit')).toBeInTheDocument();
    expect(screen.getByText(/45,320.75/)).toBeInTheDocument();
    expect(screen.getByText('(+13.3%)')).toBeInTheDocument();
    expect(screen.getByText('vs previous period')).toBeInTheDocument();
  });

  it('formats plain counts without a currency symbol', () => {
    render(
      <SummaryCard
        card={{ ...baseCard, key: 'count', title: 'Total Transactions', value: 1234, valueFormat: 'number', difference: 0, percentChange: null, trend: 'flat', previousValue: 1234 }}
      />,
    );

    expect(screen.getByText('1,234')).toBeInTheDocument();
    expect(screen.queryByText('vs previous period')).not.toBeInTheDocument();
  });

  it('shows the optional caption', () => {
    render(<SummaryCard card={{ ...baseCard, caption: 'Receivable from people' }} />);
    expect(screen.getByText('Receivable from people')).toBeInTheDocument();
  });
});

describe('SearchBar', () => {
  it('reports each keystroke to the caller', async () => {
    const onChange = vi.fn();
    render(<SearchBar value="" onChange={onChange} placeholder="Search transactions" />);

    await userEvent.type(screen.getByPlaceholderText('Search transactions'), 'a');
    expect(onChange).toHaveBeenCalledWith('a');
  });

  it('clears the value through the clear button', async () => {
    const onChange = vi.fn();
    render(<SearchBar value="swiggy" onChange={onChange} />);

    await userEvent.click(screen.getByLabelText('Clear search'));
    expect(onChange).toHaveBeenCalledWith('');
  });
});

describe('state components', () => {
  it('renders an empty state message', () => {
    render(<EmptyState title="No transactions" message="Try widening the date range." />);
    expect(screen.getByText('No transactions')).toBeInTheDocument();
    expect(screen.getByText('Try widening the date range.')).toBeInTheDocument();
  });

  it('invokes the retry handler', async () => {
    const onRetry = vi.fn();
    render(<ErrorState message="Something failed" onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});

interface Row {
  id: string;
  name: string;
  amount: number;
}

describe('DataTable', () => {
  const columns = [
    { id: 'name', header: 'Name', cell: ({ row }: { row: { original: Row } }) => row.original.name },
    { id: 'amount', header: 'Amount', cell: ({ row }: { row: { original: Row } }) => row.original.amount },
  ];

  const rows: Row[] = [
    { id: '1', name: 'Swiggy', amount: 845 },
    { id: '2', name: 'Amazon', amount: 3250 },
  ];

  const baseProps = {
    columns: columns as never,
    data: rows,
    totalCount: 2,
    pageNumber: 1,
    pageSize: 25,
    onPageChange: vi.fn(),
    onPageSizeChange: vi.fn(),
    getRowId: (row: Row) => row.id,
  };

  it('renders every row', () => {
    render(<DataTable {...baseProps} />);

    expect(screen.getByText('Swiggy')).toBeInTheDocument();
    expect(screen.getByText('Amazon')).toBeInTheDocument();
    expect(screen.getByText('Showing 1-2 of 2')).toBeInTheDocument();
  });

  it('shows the empty message when there are no rows', () => {
    render(<DataTable {...baseProps} data={[]} totalCount={0} emptyMessage="Nothing matched." />);

    expect(screen.getByText('Nothing matched.')).toBeInTheDocument();
    expect(screen.getByText('No records')).toBeInTheDocument();
  });

  it('disables paging controls on a single page', () => {
    render(<DataTable {...baseProps} />);

    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });

  it('requests the next page', async () => {
    const onPageChange = vi.fn();
    render(<DataTable {...baseProps} totalCount={100} onPageChange={onPageChange} />);

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('toggles column visibility', async () => {
    render(<DataTable {...baseProps} />);

    await userEvent.click(screen.getByRole('button', { name: /Columns/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Name' }));

    expect(screen.queryByText('Swiggy')).not.toBeInTheDocument();
  });
});
