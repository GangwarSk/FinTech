import { api } from '@/core/api/client';
import type { AuthResponse, CurrentUser } from '@/shared/types/auth';
import type {
  DashboardPeriod,
  DashboardSummaryDto,
  CreateLedgerEntryRequest,
  CreatePersonRequest,
  LedgerEntryDto,
  PersonAuditDto,
  PersonDto,
  PersonFilterRequest,
  PersonLedgerFilterRequest,
  UpdatePersonRequest,
} from '@/shared/types/dashboard';
import type {
  BulkResultDto,
  EnumCatalog,
  IdResponse,
  LookupDto,
  PagedResponse,
  PaginationRequest,
} from '@/shared/types/api';
import type {
  BulkAssignRequest,
  CreateTransactionRequest,
  TransactionDto,
  TransactionFilterRequest,
  TransactionSearchResult,
} from '@/shared/types/transactions';
import type {
  BankAccountDto,
  BankDto,
  CategoryDto,
  CreditCardDto,
  MasterFilterRequest,
  ProcessStatementRequest,
  RoleDto,
  SettingDto,
  StatementFileDetailDto,
  StatementFileFilterRequest,
  StatementFileImpactDto,
  StatementFileLookupDto,
  StatementFileSummaryDto,
  StatementProcessingResultDto,
  UploadHistoryDto,
  UploadHistoryFilterRequest,
  UploadStatementResponse,
  UserDto,
  VendorDto,
} from '@/shared/types/masters';

export const authApi = {
  login: (userNameOrEmail: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { userNameOrEmail, password }),
  logout: (refreshToken: string) => api.post<void>('/auth/logout', { refreshToken }),
  me: () => api.get<CurrentUser>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>('/auth/change-password', { currentPassword, newPassword }),
};

export const dashboardApi = {
  summary: (period: DashboardPeriod, from?: string, to?: string) => {
    const params = new URLSearchParams({ period });
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    return api.get<DashboardSummaryDto>(`/dashboard/summary?${params.toString()}`);
  },
};

export const transactionsApi = {
  search: (filter: TransactionFilterRequest) =>
    api.post<TransactionSearchResult>('/transactions/search', filter),
  getById: (id: string) => api.get(`/transactions/${id}`),
  create: (request: CreateTransactionRequest) => api.post<IdResponse>('/transactions', request),
  update: (id: string, request: unknown) => api.put<void>(`/transactions/${id}`, request),
  remove: (id: string) => api.delete<void>(`/transactions/${id}`),
  bulkAssign: (request: BulkAssignRequest) => api.post<BulkResultDto>('/transactions/bulk-assign', request),
  exportCsv: (filter: TransactionFilterRequest) => api.download('/transactions/export', filter),
};

export const personsApi = {
  search: (filter: PersonFilterRequest) => api.post<PagedResponse<PersonDto>>('/persons/search', filter),
  getById: (id: string) => api.get<PersonDto>(`/persons/${id}`),
  audit: (id: string, from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const query = params.toString();
    return api.get<PersonAuditDto>(`/persons/${id}/audit${query ? `?${query}` : ''}`);
  },
  ledger: (id: string, filter: PersonLedgerFilterRequest) =>
    api.post<PagedResponse<LedgerEntryDto>>(`/persons/${id}/ledger/search`, filter),
  create: (request: CreatePersonRequest) => api.post<IdResponse>('/persons', request),
  update: (id: string, request: UpdatePersonRequest) => api.put<void>(`/persons/${id}`, request),
  remove: (id: string) => api.delete<void>(`/persons/${id}`),
  addLedgerEntry: (id: string, request: CreateLedgerEntryRequest) =>
    api.post<IdResponse>(`/persons/${id}/ledger`, request),
  removeLedgerEntry: (id: string, entryId: string) => api.delete<void>(`/persons/${id}/ledger/${entryId}`),
};

export const statementsApi = {
  upload: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.upload<UploadStatementResponse>('/statements/upload', formData);
  },
  process: (request: ProcessStatementRequest) =>
    api.post<StatementProcessingResultDto>('/statements/process', request),
  history: (filter: UploadHistoryFilterRequest) =>
    api.post<PagedResponse<UploadHistoryDto>>('/statements/uploads/search', filter),
  remove: (id: string) => api.delete<void>(`/statements/${id}`),
  files: {
    search: (filter: StatementFileFilterRequest) =>
      api.post<PagedResponse<StatementFileSummaryDto>>('/statements/files/search', filter),
    detail: (id: string) => api.get<StatementFileDetailDto>(`/statements/files/${id}`),
    transactions: (id: string, filter: PaginationRequest) =>
      api.post<PagedResponse<TransactionDto>>(`/statements/files/${id}/transactions`, filter),
    impact: (id: string) => api.get<StatementFileImpactDto>(`/statements/files/${id}/impact`),
    remove: (id: string) => api.delete<void>(`/statements/files/${id}`),
    recycleBin: (filter: StatementFileFilterRequest) =>
      api.post<PagedResponse<StatementFileSummaryDto>>('/statements/files/recycle-bin/search', filter),
    restore: (id: string) => api.post<void>(`/statements/files/${id}/restore`, {}),
    purge: (id: string) => api.delete<void>(`/statements/files/${id}/purge`),
  },
};

export const mastersApi = {
  banks: {
    search: (filter: MasterFilterRequest) => api.post<PagedResponse<BankDto>>('/masters/banks/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/banks', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/banks/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/banks/${id}`),
  },
  accounts: {
    search: (filter: MasterFilterRequest) =>
      api.post<PagedResponse<BankAccountDto>>('/masters/accounts/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/accounts', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/accounts/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/accounts/${id}`),
  },
  cards: {
    search: (filter: MasterFilterRequest) => api.post<PagedResponse<CreditCardDto>>('/masters/cards/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/cards', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/cards/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/cards/${id}`),
  },
  categories: {
    search: (filter: MasterFilterRequest) =>
      api.post<PagedResponse<CategoryDto>>('/masters/categories/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/categories', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/categories/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/categories/${id}`),
  },
  vendors: {
    search: (filter: MasterFilterRequest) => api.post<PagedResponse<VendorDto>>('/masters/vendors/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/vendors', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/vendors/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/vendors/${id}`),
  },
  users: {
    search: (filter: MasterFilterRequest) => api.post<PagedResponse<UserDto>>('/masters/users/search', filter),
    create: (request: unknown) => api.post<IdResponse>('/masters/users', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/users/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/users/${id}`),
    resetPassword: (id: string, request: unknown) => api.post<void>(`/masters/users/${id}/reset-password`, request),
  },
  roles: {
    list: () => api.get<RoleDto[]>('/masters/roles'),
    permissions: () => api.get<string[]>('/masters/roles/permissions'),
    create: (request: unknown) => api.post<IdResponse>('/masters/roles', request),
    update: (id: string, request: unknown) => api.put<void>(`/masters/roles/${id}`, request),
    remove: (id: string) => api.delete<void>(`/masters/roles/${id}`),
  },
  settings: {
    list: (category?: string) => api.get<SettingDto[]>(`/masters/settings${category ? `?category=${category}` : ''}`),
    update: (key: string, value: string | null) => api.put<void>(`/masters/settings/${key}`, { value }),
  },
};

export const lookupsApi = {
  banks: () => api.get<LookupDto[]>('/lookups/banks'),
  accounts: () => api.get<LookupDto[]>('/lookups/accounts'),
  cards: () => api.get<LookupDto[]>('/lookups/cards'),
  categories: () => api.get<LookupDto[]>('/lookups/categories'),
  vendors: () => api.get<LookupDto[]>('/lookups/vendors'),
  statementFiles: (bankId?: string | null, year?: number | null) => {
    const params = new URLSearchParams();
    if (bankId) params.set('bankId', bankId);
    if (year) params.set('year', String(year));
    const query = params.toString();
    return api.get<StatementFileLookupDto[]>(`/lookups/statement-files${query ? `?${query}` : ''}`);
  },
  enums: () => api.get<EnumCatalog>('/lookups/enums'),
};
