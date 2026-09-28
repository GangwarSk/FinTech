import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { Save } from 'lucide-react';
import { mastersApi } from '@/core/api/endpoints';
import { useAuthStore } from '@/core/stores/authStore';
import { cn, formatCurrency, formatDate, formatNumber } from '@/shared/lib/utils';
import { Permissions } from '@/shared/types/auth';
import type {
  BankAccountDto,
  BankDto,
  CategoryDto,
  CreditCardDto,
  RoleDto,
  SettingDto,
  UserDto,
  VendorDto,
} from '@/shared/types/masters';
import { DataTable } from '@/shared/components/data-table';
import { EmptyState, LoadingState, PageHeader, SearchBar } from '@/shared/components/common';
import { toast } from '@/shared/components/toast';
import { Button } from '@/shared/components/ui/button';
import { Badge, Card, CardContent, CardHeader, CardTitle, Input, Label } from '@/shared/components/ui/primitives';

type TabKey = 'banks' | 'accounts' | 'cards' | 'categories' | 'vendors' | 'users' | 'roles' | 'settings';

const TABS: { key: TabKey; label: string; permission: string }[] = [
  { key: 'banks', label: 'Banks', permission: Permissions.MastersRead },
  { key: 'accounts', label: 'Accounts', permission: Permissions.MastersRead },
  { key: 'cards', label: 'Cards', permission: Permissions.MastersRead },
  { key: 'categories', label: 'Categories', permission: Permissions.MastersRead },
  { key: 'vendors', label: 'Vendors', permission: Permissions.MastersRead },
  { key: 'users', label: 'Users', permission: Permissions.UsersManage },
  { key: 'roles', label: 'Roles', permission: Permissions.UsersManage },
  { key: 'settings', label: 'Settings', permission: Permissions.MastersRead },
];

export function MasterManagementPage() {
  const can = useAuthStore((state) => state.can);
  const visibleTabs = TABS.filter((tab) => can(tab.permission));
  const [tab, setTab] = React.useState<TabKey>(visibleTabs[0]?.key ?? 'banks');

  return (
    <div className="space-y-6">
      <PageHeader
        title="Master Management"
        description="Reference data that drives the rest of the application: banks, cards, accounts, categories, vendors, users, roles and settings."
      />

      <div className="flex flex-wrap gap-2 border-b pb-2" role="tablist">
        {visibleTabs.map((item) => (
          <Button
            key={item.key}
            role="tab"
            aria-selected={tab === item.key}
            variant={tab === item.key ? 'default' : 'ghost'}
            size="sm"
            onClick={() => setTab(item.key)}
          >
            {item.label}
          </Button>
        ))}
      </div>

      {tab === 'banks' && <BanksTab />}
      {tab === 'accounts' && <AccountsTab />}
      {tab === 'cards' && <CardsTab />}
      {tab === 'categories' && <CategoriesTab />}
      {tab === 'vendors' && <VendorsTab />}
      {tab === 'users' && <UsersTab />}
      {tab === 'roles' && <RolesTab />}
      {tab === 'settings' && <SettingsTab />}
    </div>
  );
}

/** Shared paging + keyword state for every simple master grid. */
function useMasterGrid() {
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(25);
  const [keyword, setKeyword] = React.useState('');
  const [debounced, setDebounced] = React.useState('');

  React.useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(keyword);
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [keyword]);

  return { page, setPage, pageSize, setPageSize, keyword, setKeyword, debounced };
}

function GridShell({
  keyword,
  onKeywordChange,
  placeholder,
  children,
}: {
  keyword: string;
  onKeywordChange: (value: string) => void;
  placeholder: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-3">
      <SearchBar value={keyword} onChange={onKeywordChange} placeholder={placeholder} className="w-full sm:w-80" />
      {children}
    </div>
  );
}

function BanksTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'banks', grid.page, grid.pageSize, grid.debounced],
    queryFn: () => mastersApi.banks.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<BankDto, unknown>[]>(
    () => [
      { id: 'name', header: 'Name', accessorKey: 'name' },
      { id: 'shortName', header: 'Short name', accessorKey: 'shortName' },
      { id: 'code', header: 'Code', cell: ({ row }) => <Badge variant="secondary">{row.original.codeName}</Badge> },
      { id: 'ifsc', header: 'IFSC', cell: ({ row }) => row.original.ifsc ?? '-' },
      { id: 'accounts', header: 'Accounts', cell: ({ row }) => formatNumber(row.original.accountCount) },
      { id: 'cards', header: 'Cards', cell: ({ row }) => formatNumber(row.original.cardCount) },
      {
        id: 'keywords',
        header: 'Statement keywords',
        cell: ({ row }) => (
          <span className="block max-w-[320px] truncate text-xs text-muted-foreground" title={row.original.statementKeywords ?? ''}>
            {row.original.statementKeywords ?? '-'}
          </span>
        ),
      },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search banks">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function AccountsTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'accounts', grid.page, grid.pageSize, grid.debounced],
    queryFn: () =>
      mastersApi.accounts.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<BankAccountDto, unknown>[]>(
    () => [
      { id: 'displayName', header: 'Account', accessorKey: 'displayName' },
      { id: 'bank', header: 'Bank', accessorKey: 'bankName' },
      { id: 'number', header: 'Number', accessorKey: 'accountNumberMasked' },
      { id: 'holder', header: 'Holder', accessorKey: 'accountHolderName' },
      { id: 'type', header: 'Type', cell: ({ row }) => <Badge variant="secondary">{row.original.accountTypeName}</Badge> },
      { id: 'ifsc', header: 'IFSC', cell: ({ row }) => row.original.ifsc ?? '-' },
      {
        id: 'balance',
        header: 'Balance',
        cell: ({ row }) => <span className="tabular">{formatCurrency(row.original.currentBalance)}</span>,
      },
      { id: 'transactions', header: 'Transactions', cell: ({ row }) => formatNumber(row.original.transactionCount) },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search accounts">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function CardsTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'cards', grid.page, grid.pageSize, grid.debounced],
    queryFn: () => mastersApi.cards.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<CreditCardDto, unknown>[]>(
    () => [
      { id: 'displayName', header: 'Card', accessorKey: 'displayName' },
      { id: 'bank', header: 'Bank', accessorKey: 'bankName' },
      { id: 'number', header: 'Number', accessorKey: 'cardNumberMasked' },
      { id: 'holder', header: 'Holder', accessorKey: 'cardHolderName' },
      { id: 'network', header: 'Network', cell: ({ row }) => <Badge variant="secondary">{row.original.networkName}</Badge> },
      {
        id: 'limit',
        header: 'Credit limit',
        cell: ({ row }) => <span className="tabular">{formatCurrency(row.original.creditLimit)}</span>,
      },
      {
        id: 'outstanding',
        header: 'Outstanding',
        cell: ({ row }) => <span className="tabular text-destructive">{formatCurrency(row.original.currentOutstanding)}</span>,
      },
      {
        id: 'available',
        header: 'Available',
        cell: ({ row }) => <span className="tabular text-success">{formatCurrency(row.original.availableLimit)}</span>,
      },
      { id: 'statements', header: 'Statements', cell: ({ row }) => formatNumber(row.original.statementCount) },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search cards">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function CategoriesTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'categories', grid.page, grid.pageSize, grid.debounced],
    queryFn: () =>
      mastersApi.categories.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<CategoryDto, unknown>[]>(
    () => [
      {
        id: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <span className="inline-flex items-center gap-2">
            <span
              className="h-3 w-3 shrink-0 rounded-full border"
              style={{ backgroundColor: row.original.colorHex ?? 'transparent' }}
              aria-hidden="true"
            />
            {row.original.name}
          </span>
        ),
      },
      { id: 'code', header: 'Code', cell: ({ row }) => row.original.code ?? '-' },
      { id: 'parent', header: 'Parent', cell: ({ row }) => row.original.parentCategoryName ?? '-' },
      {
        id: 'keywords',
        header: 'Match keywords',
        cell: ({ row }) => (
          <span className="block max-w-[360px] truncate text-xs text-muted-foreground" title={row.original.matchKeywords ?? ''}>
            {row.original.matchKeywords ?? '-'}
          </span>
        ),
      },
      { id: 'transactions', header: 'Transactions', cell: ({ row }) => formatNumber(row.original.transactionCount) },
      { id: 'system', header: 'System', cell: ({ row }) => (row.original.isSystemCategory ? <Badge variant="secondary">System</Badge> : '-') },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search categories">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function VendorsTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'vendors', grid.page, grid.pageSize, grid.debounced],
    queryFn: () => mastersApi.vendors.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<VendorDto, unknown>[]>(
    () => [
      { id: 'name', header: 'Vendor', cell: ({ row }) => row.original.displayName ?? row.original.name },
      { id: 'category', header: 'Category', cell: ({ row }) => row.original.defaultCategoryName ?? row.original.category ?? '-' },
      {
        id: 'keywords',
        header: 'Match keywords',
        cell: ({ row }) => (
          <span className="block max-w-[360px] truncate text-xs text-muted-foreground" title={row.original.matchKeywords ?? ''}>
            {row.original.matchKeywords ?? '-'}
          </span>
        ),
      },
      { id: 'transactions', header: 'Transactions', cell: ({ row }) => formatNumber(row.original.transactionCount) },
      {
        id: 'spend',
        header: 'Total spend',
        cell: ({ row }) => <span className="tabular">{formatCurrency(row.original.totalSpend)}</span>,
      },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search vendors">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function UsersTab() {
  const grid = useMasterGrid();
  const query = useQuery({
    queryKey: ['masters', 'users', grid.page, grid.pageSize, grid.debounced],
    queryFn: () => mastersApi.users.search({ pageNumber: grid.page, pageSize: grid.pageSize, keyword: grid.debounced || null }),
  });

  const columns = React.useMemo<ColumnDef<UserDto, unknown>[]>(
    () => [
      { id: 'userName', header: 'User name', accessorKey: 'userName' },
      { id: 'fullName', header: 'Full name', accessorKey: 'fullName' },
      { id: 'email', header: 'Email', accessorKey: 'email' },
      {
        id: 'roles',
        header: 'Roles',
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-1">
            {row.original.roles.map((role) => (
              <Badge key={role.id} variant="secondary">
                {role.name}
              </Badge>
            ))}
          </div>
        ),
      },
      { id: 'lastLogin', header: 'Last sign-in', cell: ({ row }) => formatDate(row.original.lastLoginOnUtc) },
      { id: 'active', header: 'Active', cell: ({ row }) => <ActiveBadge active={row.original.isActive} /> },
    ],
    [],
  );

  return (
    <GridShell keyword={grid.keyword} onKeywordChange={grid.setKeyword} placeholder="Search users">
      <DataTable
        columns={columns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pageNumber={grid.page}
        pageSize={grid.pageSize}
        onPageChange={grid.setPage}
        onPageSizeChange={grid.setPageSize}
        isLoading={query.isLoading}
        getRowId={(row) => row.id}
      />
    </GridShell>
  );
}

function RolesTab() {
  const query = useQuery({ queryKey: ['masters', 'roles'], queryFn: mastersApi.roles.list });

  if (query.isLoading) return <LoadingState label="Loading roles..." />;
  if (!query.data?.length) return <EmptyState title="No roles" />;

  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {query.data.map((role: RoleDto) => (
        <Card key={role.id}>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between gap-2">
              <CardTitle>{role.name}</CardTitle>
              {role.isSystemRole && <Badge variant="secondary">System</Badge>}
            </div>
            {role.description && <p className="text-sm text-muted-foreground">{role.description}</p>}
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">{formatNumber(role.userCount)} users assigned</p>
            <div className="flex flex-wrap gap-1">
              {role.permissions.map((permission) => (
                <Badge key={permission} variant="outline">
                  {permission}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function SettingsTab() {
  const queryClient = useQueryClient();
  const canManage = useAuthStore((state) => state.can(Permissions.SettingsManage));
  const query = useQuery({ queryKey: ['masters', 'settings'], queryFn: () => mastersApi.settings.list() });
  const [drafts, setDrafts] = React.useState<Record<string, string>>({});

  const save = useMutation({
    mutationFn: ({ key, value }: { key: string; value: string }) => mastersApi.settings.update(key, value),
    onSuccess: (_, variables) => {
      toast.success('Setting saved', variables.key);
      setDrafts((prev) => {
        const next = { ...prev };
        delete next[variables.key];
        return next;
      });
      void queryClient.invalidateQueries({ queryKey: ['masters', 'settings'] });
    },
    onError: (error) => toast.error('Could not save setting', (error as Error).message),
  });

  if (query.isLoading) return <LoadingState label="Loading settings..." />;
  if (!query.data?.length) return <EmptyState title="No settings" />;

  const grouped = query.data.reduce<Record<string, SettingDto[]>>((acc, setting) => {
    (acc[setting.category] ??= []).push(setting);
    return acc;
  }, {});

  return (
    <div className="space-y-6">
      {Object.entries(grouped).map(([category, settings]) => (
        <Card key={category}>
          <CardHeader className="pb-3">
            <CardTitle>{category}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {settings.map((setting) => {
              const draft = drafts[setting.key];
              const dirty = draft !== undefined && draft !== (setting.value ?? '');

              return (
                <div key={setting.id} className="flex flex-wrap items-end gap-3 border-b pb-4 last:border-0 last:pb-0">
                  <div className="min-w-[240px] flex-1">
                    <Label htmlFor={`setting-${setting.id}`}>{setting.key}</Label>
                    {setting.description && <p className="mb-1 text-xs text-muted-foreground">{setting.description}</p>}
                    <Input
                      id={`setting-${setting.id}`}
                      value={draft ?? setting.value ?? ''}
                      disabled={!canManage || setting.isSecret}
                      onChange={(event) => setDrafts((prev) => ({ ...prev, [setting.key]: event.target.value }))}
                    />
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant="secondary">{setting.dataTypeName}</Badge>
                    {setting.isSystem && <Badge variant="outline">System</Badge>}
                    {canManage && (
                      <Button
                        size="sm"
                        disabled={!dirty || save.isPending}
                        onClick={() => save.mutate({ key: setting.key, value: draft ?? '' })}
                      >
                        <Save className="h-4 w-4" />
                        Save
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function ActiveBadge({ active }: { active: boolean }) {
  return <Badge variant={active ? 'success' : 'secondary'}>{active ? 'Active' : 'Inactive'}</Badge>;
}

export { cn };
