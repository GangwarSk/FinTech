import { useQuery } from '@tanstack/react-query';
import { lookupsApi } from '@/core/api/endpoints';
import type { EnumCatalog, LookupDto } from '@/shared/types/api';
import type { StatementFileLookupDto } from '@/shared/types/masters';

/** Lookups change rarely, so they are cached aggressively and shared by every filter panel. */
const LOOKUP_STALE_TIME = 10 * 60 * 1000;

export function useBankLookup() {
  return useQuery<LookupDto[]>({
    queryKey: ['lookups', 'banks'],
    queryFn: lookupsApi.banks,
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function useAccountLookup() {
  return useQuery<LookupDto[]>({
    queryKey: ['lookups', 'accounts'],
    queryFn: lookupsApi.accounts,
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function useCardLookup() {
  return useQuery<LookupDto[]>({
    queryKey: ['lookups', 'cards'],
    queryFn: lookupsApi.cards,
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function useCategoryLookup() {
  return useQuery<LookupDto[]>({
    queryKey: ['lookups', 'categories'],
    queryFn: lookupsApi.categories,
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function useVendorLookup() {
  return useQuery<LookupDto[]>({
    queryKey: ['lookups', 'vendors'],
    queryFn: lookupsApi.vendors,
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function useEnumCatalog() {
  return useQuery<EnumCatalog>({
    queryKey: ['lookups', 'enums'],
    queryFn: lookupsApi.enums,
    staleTime: Infinity,
  });
}

/**
 * Files are only fetched once a bank is chosen: the list is unbounded across all banks, and narrowing
 * by bank (then optionally year) is what keeps the dropdown usable as statements accumulate.
 */
export function useStatementFileLookup(bankId?: string | null, year?: number | null) {
  return useQuery<StatementFileLookupDto[]>({
    queryKey: ['lookups', 'statement-files', bankId ?? null, year ?? null],
    queryFn: () => lookupsApi.statementFiles(bankId, year),
    enabled: Boolean(bankId),
    staleTime: LOOKUP_STALE_TIME,
  });
}

export function toOptions(lookups: LookupDto[] | undefined) {
  return (lookups ?? []).map((lookup) => ({
    value: lookup.id,
    label: lookup.name,
    description: lookup.description,
  }));
}
