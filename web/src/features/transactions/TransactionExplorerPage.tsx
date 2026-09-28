import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import type { ColumnDef, SortingState } from '@tanstack/react-table';
import { Download, Loader2 } from 'lucide-react';
import { transactionsApi } from '@/core/api/endpoints';
import {
  useAccountLookup,
  useBankLookup,
  useCardLookup,
  useCategoryLookup,
  useEnumCatalog,
  useStatementFileLookup,
  useVendorLookup,
  toOptions,
} from '@/shared/hooks/useLookups';
import { cn, formatCurrency, formatDate, formatDateTime, formatNumber } from '@/shared/lib/utils';
import type { TransactionDto, TransactionFilterRequest } from '@/shared/types/transactions';
import { DataTable } from '@/shared/components/data-table';
import {
  BooleanSelect,
  DateRangePicker,
  FilterBar,
  MultiSelect,
  PageHeader,
  SearchBar,
  type FilterChip,
} from '@/shared/components/common';
import { toast } from '@/shared/components/toast';
import { Button } from '@/shared/components/ui/button';
import { Badge, Card, CardContent, Input, Label, Select } from '@/shared/components/ui/primitives';

const GROUPINGS = [
  { value: '', label: 'No grouping' },
  { value: 'vendor', label: 'Vendor' },
  { value: 'person', label: 'Person' },
  { value: 'card', label: 'Card' },
  { value: 'account', label: 'Account' },
  { value: 'bank', label: 'Bank' },
  { value: 'category', label: 'Category' },
  { value: 'month', label: 'Month' },
  { value: 'type', label: 'Transaction type' },
];

const DEFAULT_FILTER: TransactionFilterRequest = {
  pageNumber: 1,
  pageSize: 25,
  sortBy: 'TransactionDate',
  sortDirection: 1,
};

export function TransactionExplorerPage() {
  const [filter, setFilter] = React.useState<TransactionFilterRequest>(DEFAULT_FILTER);
  // Controls inside the filter dialog edit this copy; nothing hits the API until Apply.
  const [draft, setDraft] = React.useState<TransactionFilterRequest>(DEFAULT_FILTER);
  const [keywordInput, setKeywordInput] = React.useState('');
  const [exporting, setExporting] = React.useState(false);
  // Year is a narrowing step for the file dropdown, not a server-side transaction filter of its own.
  const [fileYear, setFileYear] = React.useState<number | null>(null);

  const banks = useBankLookup();
  const cards = useCardLookup();
  const accounts = useAccountLookup();
  const vendors = useVendorLookup();
  const categories = useCategoryLookup();
  const enums = useEnumCatalog();

  // Debounce keyword changes so typing does not fire a query per keystroke.
  React.useEffect(() => {
    const timer = setTimeout(() => {
      setFilter((prev) =>
        (prev.keyword ?? '') === keywordInput ? prev : { ...prev, keyword: keywordInput || null, pageNumber: 1 },
      );
    }, 350);
    return () => clearTimeout(timer);
  }, [keywordInput]);

  const query = useQuery({
    queryKey: ['transactions', filter],
    queryFn: () => transactionsApi.search(filter),
    placeholderData: (previous) => previous,
  });

  const patch = (changes: Partial<TransactionFilterRequest>) =>
    setFilter((prev) => ({ ...prev, ...changes, pageNumber: changes.pageNumber ?? 1 }));

  const patchDraft = (changes: Partial<TransactionFilterRequest>) =>
    setDraft((prev) => ({ ...prev, ...changes }));

  // Files are scoped to one bank: with several banks selected there is no single list to cascade from.
  const selectedBankId = draft.bankIds?.length === 1 ? draft.bankIds[0] : null;
  const statementFiles = useStatementFileLookup(selectedBankId, fileYear);

  const fileYears = React.useMemo(() => {
    const years = new Set<number>();
    for (const file of statementFiles.data ?? []) {
      if (file.periodEnd) years.add(new Date(file.periodEnd).getFullYear());
    }
    return [...years].sort((a, b) => b - a);
  }, [statementFiles.data]);

  const fileOptions = React.useMemo(
    () =>
      (statementFiles.data ?? []).map((file) => ({
        value: file.id,
        label: `${file.label} - ${file.transactionCount} txns`,
      })),
    [statementFiles.data],
  );

  const transactionTypeOptions = React.useMemo(
    () => (enums.data?.transactionType ?? []).map((option) => ({ value: String(option.value), label: option.label })),
    [enums.data],
  );

  // Changing bank or year invalidates the chosen files, so they are cleared rather than silently
  // filtering by a file that is no longer in the list.
  const selectBanks = (values: string[]) => {
    setFileYear(null);
    patchDraft({ bankIds: values.length ? values : null, statementFileIds: null });
  };

  const selectYear = (year: number | null) => {
    setFileYear(year);
    patchDraft({ statementFileIds: null });
  };

  // Keyword and grouping stay live outside the dialog, so they are carried over on apply.
  const applyFilters = () =>
    setFilter((prev) => ({
      ...draft,
      keyword: prev.keyword,
      groupBy: prev.groupBy,
      pageSize: prev.pageSize,
      pageNumber: 1,
    }));

  const reset = () => {
    setFilter((prev) => ({ ...DEFAULT_FILTER, groupBy: prev.groupBy, pageSize: prev.pageSize }));
    setDraft(DEFAULT_FILTER);
    setKeywordInput('');
    setFileYear(null);
  };

  const removeFilter = (changes: Partial<TransactionFilterRequest>) => {
    patch(changes);
    setDraft((prev) => ({ ...prev, ...changes }));
  };

  const activeFilterCount = React.useMemo(() => {
    const ignored = new Set(['pageNumber', 'pageSize', 'sortBy', 'sortDirection', 'groupBy']);
    return Object.entries(filter).filter(([key, value]) => {
      if (ignored.has(key)) return false;
      if (value === null || value === undefined || value === '') return false;
      if (Array.isArray(value)) return value.length > 0;
      return true;
    }).length;
  }, [filter]);

  const chips = React.useMemo<FilterChip[]>(() => {
    const labelsFor = (options: { value: string; label: string }[], ids?: string[] | null) => {
      if (!ids?.length) return null;
      const map = new Map(options.map((option) => [option.value, option.label]));
      // Lookups load asynchronously, so fall back to a count until the labels are known.
      return ids.every((id) => map.has(id)) ? ids.map((id) => map.get(id)!).join(', ') : `${ids.length} selected`;
    };

    const list: FilterChip[] = [];
    const add = (id: string, label: string, value: string | null, changes: Partial<TransactionFilterRequest>) => {
      if (value) list.push({ id, label, value, onRemove: () => removeFilter(changes) });
    };
    const anyOr = (value: string | null) => value ?? 'Any';

    if (filter.dateFrom || filter.dateTo) {
      add(
        'date',
        'Date',
        `${anyOr(filter.dateFrom ? formatDate(filter.dateFrom) : null)} - ${anyOr(filter.dateTo ? formatDate(filter.dateTo) : null)}`,
        { dateFrom: null, dateTo: null },
      );
    }

    if (filter.amountFrom != null || filter.amountTo != null) {
      add(
        'amount',
        'Amount',
        `${anyOr(filter.amountFrom != null ? formatCurrency(filter.amountFrom) : null)} - ${anyOr(filter.amountTo != null ? formatCurrency(filter.amountTo) : null)}`,
        { amountFrom: null, amountTo: null },
      );
    }

    if (filter.creditOnly) add('direction', 'Direction', 'Credits only', { creditOnly: null });
    if (filter.debitOnly) add('direction', 'Direction', 'Debits only', { debitOnly: null });

    add('bank', 'Bank', labelsFor(toOptions(banks.data), filter.bankIds), { bankIds: null, statementFileIds: null });
    add('file', 'File', labelsFor(fileOptions, filter.statementFileIds), { statementFileIds: null });
    add('card', 'Card', labelsFor(toOptions(cards.data), filter.creditCardIds), { creditCardIds: null });
    add('account', 'Account', labelsFor(toOptions(accounts.data), filter.bankAccountIds), { bankAccountIds: null });
    add('vendor', 'Vendor', labelsFor(toOptions(vendors.data), filter.vendorIds), { vendorIds: null });
    add('category', 'Category', labelsFor(toOptions(categories.data), filter.categoryIds), { categoryIds: null });
    add(
      'type',
      'Type',
      labelsFor(transactionTypeOptions, (filter.transactionTypes ?? []).map(String)),
      { transactionTypes: null },
    );
    add('reference', 'Reference', filter.referenceNumber ?? null, { referenceNumber: null });

    if (filter.isReconciled != null) {
      add('reconciled', 'Reconciled', filter.isReconciled ? 'Yes' : 'No', { isReconciled: null });
    }
    if (filter.isDisputed != null) {
      add('disputed', 'Disputed', filter.isDisputed ? 'Yes' : 'No', { isDisputed: null });
    }

    return list;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    filter,
    fileOptions,
    transactionTypeOptions,
    banks.data,
    cards.data,
    accounts.data,
    vendors.data,
    categories.data,
  ]);

  const sorting: SortingState = filter.sortBy
    ? [{ id: filter.sortBy, desc: filter.sortDirection !== 0 }]
    : [];

  const exportCsv = async () => {
    setExporting(true);
    try {
      const response = await transactionsApi.exportCsv(filter);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `transactions-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
      toast.success('Export ready', 'The filtered transactions were downloaded as CSV.');
    } catch {
      toast.error('Export failed', 'The transactions could not be exported.');
    } finally {
      setExporting(false);
    }
  };

  const columns = React.useMemo<ColumnDef<TransactionDto, unknown>[]>(
    () => [
      { id: 'TransactionDate', header: 'Transaction Date', cell: ({ row }) => formatDate(row.original.transactionDate) },
      { id: 'PostingDate', header: 'Posting Date', cell: ({ row }) => formatDate(row.original.postingDate) },
      {
        id: 'Amount',
        header: 'Amount',
        cell: ({ row }) => <span className="tabular font-medium">{formatCurrency(row.original.amount)}</span>,
      },
      {
        id: 'CreditAmount',
        header: 'Credit',
        cell: ({ row }) => (
          <span className={cn('tabular', row.original.creditAmount > 0 && 'text-success')}>
            {row.original.creditAmount > 0 ? formatCurrency(row.original.creditAmount) : '-'}
          </span>
        ),
      },
      {
        id: 'DebitAmount',
        header: 'Debit',
        cell: ({ row }) => (
          <span className={cn('tabular', row.original.debitAmount > 0 && 'text-destructive')}>
            {row.original.debitAmount > 0 ? formatCurrency(row.original.debitAmount) : '-'}
          </span>
        ),
      },
      { id: 'card', header: 'Card', enableSorting: false, cell: ({ row }) => row.original.creditCardName ?? '-' },
      { id: 'account', header: 'Account', enableSorting: false, cell: ({ row }) => row.original.bankAccountName ?? '-' },
      { id: 'bank', header: 'Bank', enableSorting: false, cell: ({ row }) => row.original.bankName ?? '-' },
      { id: 'vendor', header: 'Vendor', enableSorting: false, cell: ({ row }) => row.original.vendorName ?? '-' },
      { id: 'person', header: 'Person', enableSorting: false, cell: ({ row }) => row.original.personName ?? '-' },
      {
        id: 'Description',
        header: 'Description',
        cell: ({ row }) => (
          <span className="block max-w-[280px] truncate" title={row.original.description}>
            {row.original.description}
          </span>
        ),
      },
      { id: 'ReferenceNumber', header: 'Reference', cell: ({ row }) => row.original.referenceNumber ?? '-' },
      {
        id: 'TransactionType',
        header: 'Type',
        cell: ({ row }) => <Badge variant="secondary">{row.original.transactionType}</Badge>,
      },
      { id: 'category', header: 'Category', enableSorting: false, cell: ({ row }) => row.original.categoryName ?? '-' },
      {
        id: 'statement',
        header: 'Source Statement',
        enableSorting: false,
        cell: ({ row }) => row.original.statementPeriod ?? 'Manual entry',
      },
      {
        id: 'sourceFile',
        header: 'Source File',
        enableSorting: false,
        cell: ({ row }) =>
          row.original.sourceFileName ? (
            <span className="block max-w-[220px] truncate" title={row.original.sourceFileName}>
              {row.original.sourceFileName}
            </span>
          ) : (
            'Manual entry'
          ),
      },
    ],
    [],
  );

  const filterBar = (
    <FilterBar
      title="Filters"
      description="Set as many filters as you need, then apply them together."
      activeCount={activeFilterCount}
      chips={chips}
      onOpen={() => setDraft(filter)}
      onApply={applyFilters}
      onClear={reset}
      columns={2}
      leading={
        <SearchBar
          value={keywordInput}
          onChange={setKeywordInput}
          placeholder="Search description, reference, vendor or person"
          className="w-full sm:w-72"
        />
      }
      trailing={
        <Select
          value={filter.groupBy ?? ''}
          onChange={(event) => setFilter((prev) => ({ ...prev, groupBy: event.target.value || null }))}
          aria-label="Group by"
          className="h-8 w-auto text-xs"
        >
          {GROUPINGS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.value ? `Grouped by ${option.label.toLowerCase()}` : option.label}
            </option>
          ))}
        </Select>
      }
    >
      <div className="sm:col-span-2">
        <DateRangePicker
          value={{ from: draft.dateFrom ?? null, to: draft.dateTo ?? null }}
          onChange={(range) => patchDraft({ dateFrom: range.from, dateTo: range.to })}
          label="Transaction date"
        />
      </div>

      <div className="space-y-2">
        <Label>Amount range</Label>
        <div className="flex items-center gap-2">
          <Input
            type="number"
            min={0}
            step="0.01"
            placeholder="Min"
            value={draft.amountFrom ?? ''}
            onChange={(event) => patchDraft({ amountFrom: event.target.value ? Number(event.target.value) : null })}
            aria-label="Minimum amount"
          />
          <span className="text-sm text-muted-foreground">to</span>
          <Input
            type="number"
            min={0}
            step="0.01"
            placeholder="Max"
            value={draft.amountTo ?? ''}
            onChange={(event) => patchDraft({ amountTo: event.target.value ? Number(event.target.value) : null })}
            aria-label="Maximum amount"
          />
        </div>
      </div>

      <div className="space-y-2">
        <Label>Direction</Label>
        <Select
          value={draft.creditOnly ? 'credit' : draft.debitOnly ? 'debit' : ''}
          onChange={(event) =>
            patchDraft({
              creditOnly: event.target.value === 'credit' ? true : null,
              debitOnly: event.target.value === 'debit' ? true : null,
            })
          }
          aria-label="Direction"
        >
          <option value="">Credits and debits</option>
          <option value="credit">Credits only</option>
          <option value="debit">Debits only</option>
        </Select>
      </div>

      <MultiSelect
        label="Bank"
        options={toOptions(banks.data)}
        selected={draft.bankIds ?? []}
        onChange={(values) => selectBanks(values)}
      />

      <div className="space-y-2">
        <Label htmlFor="file-year">Statement year</Label>
        <Select
          id="file-year"
          value={fileYear === null ? '' : String(fileYear)}
          onChange={(event) => selectYear(event.target.value ? Number(event.target.value) : null)}
          disabled={!selectedBankId}
        >
          <option value="">All years</option>
          {fileYears.map((year) => (
            <option key={year} value={year}>
              {year}
            </option>
          ))}
        </Select>
        <p className="text-xs text-muted-foreground">
          {selectedBankId ? 'Narrows the file list below.' : 'Select a single bank first.'}
        </p>
      </div>

      <div className="space-y-2">
        <MultiSelect
          label="Uploaded file"
          options={fileOptions}
          selected={draft.statementFileIds ?? []}
          onChange={(values) => patchDraft({ statementFileIds: values.length ? values : null })}
          placeholder={selectedBankId ? 'All files' : 'Select a bank first'}
        />
        <p className="text-xs text-muted-foreground">
          {!selectedBankId
            ? 'Files load once a single bank is selected.'
            : statementFiles.isLoading
              ? 'Loading files...'
              : fileOptions.length === 0
                ? 'No imported files for this bank.'
                : `${fileOptions.length} file${fileOptions.length === 1 ? '' : 's'} available.`}
        </p>
      </div>

      <MultiSelect
        label="Card"
        options={toOptions(cards.data)}
        selected={draft.creditCardIds ?? []}
        onChange={(values) => patchDraft({ creditCardIds: values.length ? values : null })}
      />
      <MultiSelect
        label="Account"
        options={toOptions(accounts.data)}
        selected={draft.bankAccountIds ?? []}
        onChange={(values) => patchDraft({ bankAccountIds: values.length ? values : null })}
      />
      <MultiSelect
        label="Vendor"
        options={toOptions(vendors.data)}
        selected={draft.vendorIds ?? []}
        onChange={(values) => patchDraft({ vendorIds: values.length ? values : null })}
      />
      <MultiSelect
        label="Category"
        options={toOptions(categories.data)}
        selected={draft.categoryIds ?? []}
        onChange={(values) => patchDraft({ categoryIds: values.length ? values : null })}
      />
      <MultiSelect
        label="Transaction type"
        options={transactionTypeOptions}
        selected={(draft.transactionTypes ?? []).map(String)}
        onChange={(values) => patchDraft({ transactionTypes: values.length ? values.map(Number) : null })}
      />

      <div className="space-y-2">
        <Label>Reference number</Label>
        <Input
          value={draft.referenceNumber ?? ''}
          onChange={(event) => patchDraft({ referenceNumber: event.target.value || null })}
          placeholder="Exact or partial"
          aria-label="Reference number"
        />
      </div>

      <BooleanSelect
        label="Reconciled"
        value={draft.isReconciled}
        onChange={(value) => patchDraft({ isReconciled: value })}
      />
      <BooleanSelect label="Disputed" value={draft.isDisputed} onChange={(value) => patchDraft({ isDisputed: value })} />
    </FilterBar>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Transaction Explorer"
        description="Every credit and debit from every statement, searchable across banks, cards, accounts, vendors and people."
        actions={
          <Button variant="outline" onClick={exportCsv} disabled={exporting}>
            {exporting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            Export CSV
          </Button>
        }
      />

      <section aria-label="Result totals" className="grid gap-4 sm:grid-cols-4">
        <TotalCard label="Transactions" value={formatNumber(query.data?.totalCount ?? 0)} />
        <TotalCard label="Total credit" value={formatCurrency(query.data?.totalCredit ?? 0)} tone="success" />
        <TotalCard label="Total debit" value={formatCurrency(query.data?.totalDebit ?? 0)} tone="destructive" />
        <TotalCard
          label="Net amount"
          value={formatCurrency(query.data?.netAmount ?? 0)}
          tone={(query.data?.netAmount ?? 0) >= 0 ? 'success' : 'destructive'}
        />
      </section>

      {filter.groupBy && (query.data?.groups.length ?? 0) > 0 && (
        <Card>
          <CardContent className="p-0">
            <div className="scrollbar-thin overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 text-xs font-bold uppercase tracking-wide text-slate-500 dark:bg-slate-800/70 dark:text-slate-400">
                  <tr className="border-b border-slate-200 dark:border-slate-700">
                    <th className="px-4 py-3 text-left font-bold">Group</th>
                    <th className="px-4 py-3 text-right font-bold">Count</th>
                    <th className="px-4 py-3 text-right font-bold">Credit</th>
                    <th className="px-4 py-3 text-right font-bold">Debit</th>
                    <th className="px-4 py-3 text-right font-bold">Net</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {query.data?.groups.map((group) => (
                    <tr key={group.key} className="text-slate-700 transition-colors hover:bg-indigo-50/40 dark:text-slate-200 dark:hover:bg-slate-800/50">
                      <td className="px-4 py-3">{group.label}</td>
                      <td className="tabular px-4 py-3 text-right">{formatNumber(group.count)}</td>
                      <td className="tabular px-4 py-3 text-right text-success">{formatCurrency(group.totalCredit)}</td>
                      <td className="tabular px-4 py-3 text-right text-destructive">{formatCurrency(group.totalDebit)}</td>
                      <td className="tabular px-4 py-3 text-right font-semibold">{formatCurrency(group.net)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={filter.pageNumber}
        pageSize={filter.pageSize}
        onPageChange={(page) => setFilter((prev) => ({ ...prev, pageNumber: page }))}
        onPageSizeChange={(size) => setFilter((prev) => ({ ...prev, pageSize: size, pageNumber: 1 }))}
        sorting={sorting}
        onSortingChange={(next) =>
          setFilter((prev) => ({
            ...prev,
            sortBy: next[0]?.id ?? 'TransactionDate',
            sortDirection: next[0]?.desc === false ? 0 : 1,
            pageNumber: 1,
          }))
        }
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
        toolbar={filterBar}
        renderRowDetails={(row) => <TransactionDetails transaction={row} />}
        emptyMessage="No transactions matched the current filters. Try widening the date range or clearing filters."
      />
    </div>
  );
}

function TransactionDetails({ transaction: t }: { transaction: TransactionDto }) {
  const fields: { label: string; value: React.ReactNode }[] = [
    { label: 'Transaction date', value: formatDate(t.transactionDate) },
    { label: 'Posting date', value: formatDate(t.postingDate) },
    {
      label: 'Amount',
      value: (
        <span className={cn('tabular font-semibold', t.direction === 'Credit' ? 'text-success' : 'text-destructive')}>
          {t.direction === 'Credit' ? '+' : '-'}
          {formatCurrency(t.amount)} {t.currency}
        </span>
      ),
    },
    { label: 'Direction', value: t.direction },
    { label: 'Type', value: t.transactionType },
    { label: 'Source', value: t.source },
    { label: 'Reference', value: t.referenceNumber },
    { label: 'Bank', value: t.bankName },
    { label: 'Account', value: t.bankAccountName },
    { label: 'Card', value: t.creditCardName },
    { label: 'Vendor', value: t.vendorName },
    { label: 'Person', value: t.personName },
    { label: 'Category', value: t.categoryName },
    { label: 'Location', value: t.location },
    { label: 'Source statement', value: t.statementPeriod ?? 'Manual entry' },
    { label: 'Source file', value: t.sourceFileName ?? 'Manual entry' },
    { label: 'Created', value: `${formatDateTime(t.createdOnUtc)}${t.createdBy ? ` by ${t.createdBy}` : ''}` },
    {
      label: 'Modified',
      value: t.modifiedOnUtc ? `${formatDateTime(t.modifiedOnUtc)}${t.modifiedBy ? ` by ${t.modifiedBy}` : ''}` : null,
    },
  ];

  const flags = [
    t.isReconciled && 'Reconciled',
    t.isRecurring && 'Recurring',
    t.isDisputed && 'Disputed',
  ].filter(Boolean) as string[];

  const longText = [
    { label: 'Description', value: t.description },
    { label: 'Merchant text (as printed)', value: t.merchantRawText },
    { label: 'Notes', value: t.notes },
  ].filter((item) => item.value);

  return (
    <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-4 text-sm shadow-sm dark:border-slate-700 dark:bg-slate-900">
      <div className="space-y-3">
        {longText.map((item) => (
          <div key={item.label}>
            <p className="text-xs font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">{item.label}</p>
            <p className="mt-1 whitespace-pre-wrap break-words text-slate-800 dark:text-slate-100">{item.value}</p>
          </div>
        ))}
      </div>

      <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {fields.map((field) => (
          <div key={field.label} className="min-w-0">
            <dt className="text-xs font-bold uppercase tracking-wide text-slate-500 dark:text-slate-400">{field.label}</dt>
            <dd className="mt-0.5 break-words text-slate-800 dark:text-slate-100">{field.value || '-'}</dd>
          </div>
        ))}
      </dl>

      {flags.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {flags.map((flag) => (
            <Badge key={flag} variant="secondary">
              {flag}
            </Badge>
          ))}
        </div>
      )}
    </div>
  );
}

function TotalCard({
  label,
  value,
  tone,
}: {
  label: string;
  value: string;
  tone?: 'success' | 'destructive';
}) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs font-medium text-muted-foreground">{label}</p>
        <p
          className={cn(
            'tabular mt-1 text-xl font-semibold',
            tone === 'success' && 'text-success',
            tone === 'destructive' && 'text-destructive',
          )}
        >
          {value}
        </p>
      </CardContent>
    </Card>
  );
}
