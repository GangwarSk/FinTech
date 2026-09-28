export interface ApiError {
  code: string;
  message: string;
  validationErrors?: Record<string, string[]> | null;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message?: string | null;
  error?: ApiError | null;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface LookupDto {
  id: string;
  name: string;
  description?: string | null;
}

export interface EnumOptionDto {
  value: number;
  name: string;
  label: string;
}

export type EnumCatalog = Record<string, EnumOptionDto[]>;

export interface IdResponse {
  id: string;
}

export interface BulkResultDto {
  requested: number;
  succeeded: number;
  failed: number;
  errors: string[];
}

export type SortDirection = 0 | 1;

export interface PaginationRequest {
  pageNumber: number;
  pageSize: number;
  sortBy?: string | null;
  sortDirection?: SortDirection;
}
