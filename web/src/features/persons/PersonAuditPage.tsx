import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import type { ColumnDef } from '@tanstack/react-table';
import { Plus, Trash2, UserPlus } from 'lucide-react';
import { ApiRequestError } from '@/core/api/client';
import { personsApi } from '@/core/api/endpoints';
import { useAuthStore } from '@/core/stores/authStore';
import { cn, formatCurrency, formatDate, formatNumber, todayIso } from '@/shared/lib/utils';
import { Permissions } from '@/shared/types/auth';
import type { LedgerEntryDto, PersonDto } from '@/shared/types/dashboard';
import { DataTable } from '@/shared/components/data-table';
import {
  DateRangePicker,
  EmptyState,
  LoadingState,
  PageHeader,
  SearchBar,
} from '@/shared/components/common';
import { ConfirmDialog, Dialog } from '@/shared/components/ui/dialog';
import { toast } from '@/shared/components/toast';
import { Button } from '@/shared/components/ui/button';
import {
  Badge,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Checkbox,
  FieldError,
  Input,
  Label,
  Select,
  Textarea,
} from '@/shared/components/ui/primitives';

const personSchema = z.object({
  name: z.string().min(1, 'Name is required.').max(200),
  mobile: z.string().max(20).optional().or(z.literal('')),
  email: z.string().email('Enter a valid email.').optional().or(z.literal('')),
  relationship: z.string().max(100).optional().or(z.literal('')),
  line1: z.string().max(250).optional().or(z.literal('')),
  city: z.string().max(100).optional().or(z.literal('')),
  state: z.string().max(100).optional().or(z.literal('')),
  postalCode: z.string().max(20).optional().or(z.literal('')),
  matchKeywords: z.string().max(1000).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
  isActive: z.boolean().default(true),
});

type PersonForm = z.infer<typeof personSchema>;

const ledgerSchema = z.object({
  entryDate: z.string().min(1, 'Pick a date.'),
  amount: z.coerce.number().positive('Amount must be greater than zero.').max(100_000_000),
  entryType: z.coerce.number().int(),
  description: z.string().max(1000).optional().or(z.literal('')),
  referenceNumber: z.string().max(100).optional().or(z.literal('')),
});

type LedgerForm = z.infer<typeof ledgerSchema>;

const ENTRY_TYPES = [
  { value: 1, label: 'Given (I lent money)' },
  { value: 2, label: 'Taken (I borrowed money)' },
  { value: 3, label: 'Settlement received' },
  { value: 4, label: 'Settlement paid' },
];

export function PersonAuditPage() {
  const queryClient = useQueryClient();
  const canWrite = useAuthStore((state) => state.can(Permissions.PersonsWrite));

  const [keyword, setKeyword] = React.useState('');
  const [debouncedKeyword, setDebouncedKeyword] = React.useState('');
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [range, setRange] = React.useState<{ from: string | null; to: string | null }>({ from: null, to: null });
  const [ledgerPage, setLedgerPage] = React.useState(1);
  const [ledgerPageSize, setLedgerPageSize] = React.useState(25);
  const [entryTypeFilter, setEntryTypeFilter] = React.useState<number | ''>('');
  const [personDialogOpen, setPersonDialogOpen] = React.useState(false);
  const [ledgerDialogOpen, setLedgerDialogOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<PersonDto | null>(null);
  const [entryToDelete, setEntryToDelete] = React.useState<LedgerEntryDto | null>(null);

  React.useEffect(() => {
    const timer = setTimeout(() => setDebouncedKeyword(keyword), 350);
    return () => clearTimeout(timer);
  }, [keyword]);

  const people = useQuery({
    queryKey: ['persons', debouncedKeyword],
    queryFn: () =>
      personsApi.search({ pageNumber: 1, pageSize: 200, keyword: debouncedKeyword || null, sortBy: 'Name', sortDirection: 0 }),
  });

  React.useEffect(() => {
    if (!selectedId && people.data?.items.length) {
      setSelectedId(people.data.items[0].id);
    }
  }, [people.data, selectedId]);

  const audit = useQuery({
    queryKey: ['person-audit', selectedId, range.from, range.to],
    queryFn: () => personsApi.audit(selectedId!, range.from ?? undefined, range.to ?? undefined),
    enabled: Boolean(selectedId),
  });

  const ledger = useQuery({
    queryKey: ['person-ledger', selectedId, ledgerPage, ledgerPageSize, range, entryTypeFilter],
    queryFn: () =>
      personsApi.ledger(selectedId!, {
        pageNumber: ledgerPage,
        pageSize: ledgerPageSize,
        dateFrom: range.from,
        dateTo: range.to,
        entryTypes: entryTypeFilter === '' ? null : [entryTypeFilter],
        sortBy: 'EntryDate',
        sortDirection: 1,
      }),
    enabled: Boolean(selectedId),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['persons'] });
    void queryClient.invalidateQueries({ queryKey: ['person-audit'] });
    void queryClient.invalidateQueries({ queryKey: ['person-ledger'] });
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  };

  const personForm = useForm<PersonForm>({
    resolver: zodResolver(personSchema),
    defaultValues: { isActive: true },
  });

  const ledgerForm = useForm<LedgerForm>({
    resolver: zodResolver(ledgerSchema),
    defaultValues: { entryDate: todayIso(), entryType: 1 },
  });

  const savePerson = useMutation({
    mutationFn: async (values: PersonForm) => {
      const payload = {
        name: values.name,
        mobile: values.mobile || null,
        email: values.email || null,
        relationship: values.relationship || null,
        matchKeywords: values.matchKeywords || null,
        notes: values.notes || null,
        address: {
          line1: values.line1 || null,
          line2: null,
          city: values.city || null,
          state: values.state || null,
          postalCode: values.postalCode || null,
          country: 'India',
        },
      };

      return editing
        ? personsApi.update(editing.id, { ...payload, isActive: values.isActive })
        : personsApi.create(payload);
    },
    onSuccess: () => {
      toast.success(editing ? 'Person updated' : 'Person added');
      setPersonDialogOpen(false);
      setEditing(null);
      personForm.reset({ isActive: true });
      invalidate();
    },
    onError: (error) => {
      if (error instanceof ApiRequestError) {
        for (const [field, message] of Object.entries(error.fieldErrors)) {
          personForm.setError(field as keyof PersonForm, { message });
        }
        toast.error('Could not save', error.message);
      }
    },
  });

  const addLedgerEntry = useMutation({
    mutationFn: (values: LedgerForm) =>
      personsApi.addLedgerEntry(selectedId!, {
        entryDate: values.entryDate,
        amount: values.amount,
        entryType: values.entryType,
        description: values.description || null,
        referenceNumber: values.referenceNumber || null,
      }),
    onSuccess: () => {
      toast.success('Ledger entry recorded');
      setLedgerDialogOpen(false);
      ledgerForm.reset({ entryDate: todayIso(), entryType: 1, amount: undefined as unknown as number });
      invalidate();
    },
    onError: (error) => {
      if (error instanceof ApiRequestError) {
        for (const [field, message] of Object.entries(error.fieldErrors)) {
          ledgerForm.setError(field as keyof LedgerForm, { message });
        }
        toast.error('Could not save', error.message);
      }
    },
  });

  const deleteEntry = useMutation({
    mutationFn: (entry: LedgerEntryDto) => personsApi.removeLedgerEntry(entry.personId, entry.id),
    onSuccess: () => {
      toast.success('Ledger entry removed');
      setEntryToDelete(null);
      invalidate();
    },
    onError: (error) => toast.error('Could not delete', (error as Error).message),
  });

  const openCreate = () => {
    setEditing(null);
    personForm.reset({ isActive: true, name: '' });
    setPersonDialogOpen(true);
  };

  const openEdit = (person: PersonDto) => {
    setEditing(person);
    personForm.reset({
      name: person.name,
      mobile: person.mobile ?? '',
      email: person.email ?? '',
      relationship: person.relationship ?? '',
      line1: person.address?.line1 ?? '',
      city: person.address?.city ?? '',
      state: person.address?.state ?? '',
      postalCode: person.address?.postalCode ?? '',
      matchKeywords: person.matchKeywords ?? '',
      notes: person.notes ?? '',
      isActive: person.isActive,
    });
    setPersonDialogOpen(true);
  };

  const ledgerColumns = React.useMemo<ColumnDef<LedgerEntryDto, unknown>[]>(
    () => [
      { id: 'EntryDate', header: 'Date', cell: ({ row }) => formatDate(row.original.entryDate) },
      {
        id: 'EntryType',
        header: 'Type',
        cell: ({ row }) => (
          <Badge variant={row.original.signedAmount >= 0 ? 'success' : 'warning'}>{row.original.entryType}</Badge>
        ),
      },
      {
        id: 'Amount',
        header: 'Amount',
        cell: ({ row }) => <span className="tabular font-medium">{formatCurrency(row.original.amount)}</span>,
      },
      {
        id: 'signed',
        header: 'Impact',
        enableSorting: false,
        cell: ({ row }) => (
          <span className={cn('tabular', row.original.signedAmount >= 0 ? 'text-success' : 'text-destructive')}>
            {formatCurrency(row.original.signedAmount)}
          </span>
        ),
      },
      {
        id: 'description',
        header: 'Description',
        enableSorting: false,
        cell: ({ row }) => row.original.description ?? '-',
      },
      {
        id: 'reference',
        header: 'Reference',
        enableSorting: false,
        cell: ({ row }) => row.original.referenceNumber ?? '-',
      },
      {
        id: 'instrument',
        header: 'Card / Account',
        enableSorting: false,
        cell: ({ row }) => row.original.creditCardName ?? row.original.bankAccountName ?? '-',
      },
      {
        id: 'actions',
        header: '',
        enableSorting: false,
        cell: ({ row }) =>
          canWrite ? (
            <Button variant="ghost" size="icon" onClick={() => setEntryToDelete(row.original)} aria-label="Delete entry">
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          ) : null,
      },
    ],
    [canWrite],
  );

  const selectedPerson = audit.data?.person;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Person Audit"
        description="Track every rupee given to and taken from friends, relatives and customers, with a running ledger and timeline."
        actions={
          canWrite ? (
            <Button onClick={openCreate}>
              <UserPlus className="h-4 w-4" />
              Add person
            </Button>
          ) : null
        }
      />

      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <Card className="h-fit">
          <CardHeader className="pb-3">
            <CardTitle>People</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <SearchBar value={keyword} onChange={setKeyword} placeholder="Search name, mobile or email" />

            {people.isLoading && <LoadingState label="Loading people..." />}

            {!people.isLoading && (people.data?.items.length ?? 0) === 0 && (
              <EmptyState title="No people yet" message="Add a person to start tracking money given and taken." />
            )}

            <ul className="scrollbar-thin max-h-[560px] space-y-1 overflow-y-auto" role="listbox">
              {people.data?.items.map((person) => (
                <li key={person.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedId(person.id);
                      setLedgerPage(1);
                    }}
                    className={cn(
                      'w-full rounded-md border px-3 py-2 text-left transition-colors',
                      selectedId === person.id ? 'border-primary bg-primary/5' : 'border-transparent hover:bg-accent',
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="truncate text-sm font-medium">{person.name}</span>
                      <span
                        className={cn(
                          'tabular shrink-0 text-xs font-medium',
                          person.outstandingBalance > 0 ? 'text-success' : person.outstandingBalance < 0 ? 'text-destructive' : 'text-muted-foreground',
                        )}
                      >
                        {formatCurrency(person.outstandingBalance)}
                      </span>
                    </div>
                    <p className="truncate text-xs text-muted-foreground">
                      {person.relationship ?? 'No relationship'} · {person.ledgerEntryCount} entries
                    </p>
                  </button>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>

        <div className="space-y-6">
          {!selectedId && <EmptyState title="Select a person" message="Choose someone from the list to see their audit." />}

          {selectedId && audit.isLoading && <LoadingState label="Loading audit..." />}

          {selectedPerson && audit.data && (
            <>
              <Card>
                <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-lg font-semibold">{selectedPerson.name}</h2>
                      {!selectedPerson.isActive && <Badge variant="secondary">Inactive</Badge>}
                      {selectedPerson.relationship && <Badge variant="outline">{selectedPerson.relationship}</Badge>}
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {[selectedPerson.mobile, selectedPerson.email].filter(Boolean).join(' · ') || 'No contact details'}
                    </p>
                    {selectedPerson.address?.line1 && (
                      <p className="text-sm text-muted-foreground">
                        {[selectedPerson.address.line1, selectedPerson.address.city, selectedPerson.address.state, selectedPerson.address.postalCode]
                          .filter(Boolean)
                          .join(', ')}
                      </p>
                    )}
                  </div>

                  {canWrite && (
                    <div className="flex gap-2">
                      <Button variant="outline" onClick={() => openEdit(selectedPerson)}>
                        Edit
                      </Button>
                      <Button onClick={() => setLedgerDialogOpen(true)}>
                        <Plus className="h-4 w-4" />
                        Add entry
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>

              <section aria-label="Person totals" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                <StatCard label="Total given" value={formatCurrency(audit.data.totalGiven)} tone="success" />
                <StatCard label="Total taken" value={formatCurrency(audit.data.totalTaken)} tone="destructive" />
                <StatCard
                  label="Balance"
                  value={formatCurrency(audit.data.balance)}
                  tone={audit.data.balance >= 0 ? 'success' : 'destructive'}
                  caption={audit.data.balance >= 0 ? 'Receivable from this person' : 'Payable to this person'}
                />
                <StatCard label="Linked transactions" value={formatNumber(audit.data.transactionCount)} />
              </section>

              <Card>
                <CardHeader className="pb-3">
                  <CardTitle>Timeline</CardTitle>
                </CardHeader>
                <CardContent>
                  {audit.data.timeline.length === 0 ? (
                    <EmptyState title="No activity" message="No ledger entries fall inside the selected date range." />
                  ) : (
                    <ol className="space-y-2">
                      {audit.data.timeline.map((point) => (
                        <li key={point.period} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3">
                          <span className="text-sm font-medium">{point.period}</span>
                          <div className="flex flex-wrap items-center gap-4 text-sm">
                            <span className="tabular text-success">Given {formatCurrency(point.given)}</span>
                            <span className="tabular text-destructive">Taken {formatCurrency(point.taken)}</span>
                            <span className="tabular font-medium">Running {formatCurrency(point.runningBalance)}</span>
                          </div>
                        </li>
                      ))}
                    </ol>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardContent className="grid gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="sm:col-span-2">
                    <DateRangePicker value={range} onChange={setRange} label="Ledger date range" />
                  </div>
                  <div className="space-y-2">
                    <Label>Entry type</Label>
                    <Select
                      value={entryTypeFilter === '' ? '' : String(entryTypeFilter)}
                      onChange={(event) => {
                        setEntryTypeFilter(event.target.value === '' ? '' : Number(event.target.value));
                        setLedgerPage(1);
                      }}
                      aria-label="Entry type"
                    >
                      <option value="">All types</option>
                      {ENTRY_TYPES.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </Select>
                  </div>
                </CardContent>
              </Card>

              <DataTable
                columns={ledgerColumns}
                data={ledger.data?.items ?? []}
                totalCount={ledger.data?.totalCount ?? 0}
                pageNumber={ledgerPage}
                pageSize={ledgerPageSize}
                onPageChange={setLedgerPage}
                onPageSizeChange={(size) => {
                  setLedgerPageSize(size);
                  setLedgerPage(1);
                }}
                isLoading={ledger.isLoading}
                getRowId={(row) => row.id}
                emptyMessage="No ledger entries for this person in the selected range."
              />
            </>
          )}
        </div>
      </div>

      <Dialog
        open={personDialogOpen}
        onClose={() => setPersonDialogOpen(false)}
        title={editing ? 'Edit person' : 'Add person'}
        description="Match keywords let the importer link statement narrations to this person automatically."
        className="max-w-2xl"
        footer={
          <>
            <Button variant="outline" onClick={() => setPersonDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={personForm.handleSubmit((values) => savePerson.mutate(values))} disabled={savePerson.isPending}>
              {savePerson.isPending ? 'Saving...' : 'Save'}
            </Button>
          </>
        }
      >
        <form className="grid gap-4 sm:grid-cols-2" onSubmit={(event) => event.preventDefault()}>
          <div className="sm:col-span-2">
            <Label htmlFor="person-name">Name</Label>
            <Input id="person-name" {...personForm.register('name')} />
            <FieldError message={personForm.formState.errors.name?.message} />
          </div>
          <div>
            <Label htmlFor="person-mobile">Mobile</Label>
            <Input id="person-mobile" {...personForm.register('mobile')} />
            <FieldError message={personForm.formState.errors.mobile?.message} />
          </div>
          <div>
            <Label htmlFor="person-email">Email</Label>
            <Input id="person-email" type="email" {...personForm.register('email')} />
            <FieldError message={personForm.formState.errors.email?.message} />
          </div>
          <div>
            <Label htmlFor="person-relationship">Relationship</Label>
            <Input id="person-relationship" placeholder="Friend, relative, customer..." {...personForm.register('relationship')} />
          </div>
          <div>
            <Label htmlFor="person-keywords">Match keywords</Label>
            <Input id="person-keywords" placeholder="RAVI|RAVIKUMAR" {...personForm.register('matchKeywords')} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="person-address">Address</Label>
            <Input id="person-address" {...personForm.register('line1')} />
          </div>
          <div>
            <Label htmlFor="person-city">City</Label>
            <Input id="person-city" {...personForm.register('city')} />
          </div>
          <div>
            <Label htmlFor="person-state">State</Label>
            <Input id="person-state" {...personForm.register('state')} />
          </div>
          <div>
            <Label htmlFor="person-postal">Postal code</Label>
            <Input id="person-postal" {...personForm.register('postalCode')} />
          </div>
          {editing && (
            <div className="flex items-end gap-2">
              <Checkbox id="person-active" {...personForm.register('isActive')} />
              <Label htmlFor="person-active">Active</Label>
            </div>
          )}
          <div className="sm:col-span-2">
            <Label htmlFor="person-notes">Notes</Label>
            <Textarea id="person-notes" {...personForm.register('notes')} />
          </div>
        </form>
      </Dialog>

      <Dialog
        open={ledgerDialogOpen}
        onClose={() => setLedgerDialogOpen(false)}
        title="Record a ledger entry"
        description="Given increases what this person owes you; taken decreases it."
        footer={
          <>
            <Button variant="outline" onClick={() => setLedgerDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={ledgerForm.handleSubmit((values) => addLedgerEntry.mutate(values))}
              disabled={addLedgerEntry.isPending}
            >
              {addLedgerEntry.isPending ? 'Saving...' : 'Save entry'}
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={(event) => event.preventDefault()}>
          <div>
            <Label htmlFor="entry-type">Entry type</Label>
            <Select id="entry-type" {...ledgerForm.register('entryType')}>
              {ENTRY_TYPES.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </div>
          <div>
            <Label htmlFor="entry-date">Date</Label>
            <Input id="entry-date" type="date" {...ledgerForm.register('entryDate')} />
            <FieldError message={ledgerForm.formState.errors.entryDate?.message} />
          </div>
          <div>
            <Label htmlFor="entry-amount">Amount</Label>
            <Input id="entry-amount" type="number" step="0.01" min="0.01" {...ledgerForm.register('amount')} />
            <FieldError message={ledgerForm.formState.errors.amount?.message} />
          </div>
          <div>
            <Label htmlFor="entry-description">Description</Label>
            <Input id="entry-description" {...ledgerForm.register('description')} />
          </div>
          <div>
            <Label htmlFor="entry-reference">Reference number</Label>
            <Input id="entry-reference" {...ledgerForm.register('referenceNumber')} />
          </div>
        </form>
      </Dialog>

      <ConfirmDialog
        open={Boolean(entryToDelete)}
        title="Delete ledger entry"
        message="The entry will be removed and the balance recalculated. This can be traced in the audit log."
        confirmLabel="Delete"
        destructive
        busy={deleteEntry.isPending}
        onCancel={() => setEntryToDelete(null)}
        onConfirm={() => entryToDelete && deleteEntry.mutate(entryToDelete)}
      />
    </div>
  );
}

function StatCard({
  label,
  value,
  tone,
  caption,
}: {
  label: string;
  value: string;
  tone?: 'success' | 'destructive';
  caption?: string;
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
        {caption && <p className="mt-1 text-xs text-muted-foreground">{caption}</p>}
      </CardContent>
    </Card>
  );
}
