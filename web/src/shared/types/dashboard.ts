import type { PaginationRequest } from './api';

export type DashboardPeriod = 'Today' | 'Last7Days' | 'Last30Days' | 'Last90Days' | 'Custom';

export interface KpiCardDto {
  key: string;
  title: string;
  value: number;
  valueFormat: 'currency' | 'number';
  previousValue: number;
  difference: number;
  percentChange?: number | null;
  trend: 'up' | 'down' | 'flat';
  caption?: string | null;
}

export interface TopEntityDto {
  id?: string | null;
  name: string;
  amount: number;
  transactionCount: number;
}

export interface DashboardSummaryDto {
  from: string;
  to: string;
  previousFrom: string;
  previousTo: string;
  currency: string;
  cards: KpiCardDto[];
  topSpendingCard?: TopEntityDto | null;
  topSpendingVendor?: TopEntityDto | null;
  mostActiveBank?: TopEntityDto | null;
  mostActivePerson?: TopEntityDto | null;
}

export interface AddressDto {
  line1?: string | null;
  line2?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
}

export interface PersonDto {
  id: string;
  name: string;
  mobile?: string | null;
  email?: string | null;
  address: AddressDto;
  relationship?: string | null;
  notes?: string | null;
  matchKeywords?: string | null;
  isActive: boolean;
  totalGiven: number;
  totalTaken: number;
  outstandingBalance: number;
  lastActivityOn?: string | null;
  ledgerEntryCount: number;
  createdOnUtc: string;
}

export interface PersonFilterRequest extends PaginationRequest {
  keyword?: string | null;
  isActive?: boolean | null;
  hasOutstanding?: boolean | null;
  minOutstanding?: number | null;
  maxOutstanding?: number | null;
}

export interface LedgerEntryDto {
  id: string;
  personId: string;
  entryDate: string;
  amount: number;
  signedAmount: number;
  currency: string;
  entryType: string;
  entryTypeValue: number;
  description?: string | null;
  referenceNumber?: string | null;
  transactionId?: string | null;
  bankAccountId?: string | null;
  bankAccountName?: string | null;
  creditCardId?: string | null;
  creditCardName?: string | null;
  isSettled: boolean;
  settledOn?: string | null;
  createdOnUtc: string;
}

export interface PersonLedgerFilterRequest extends PaginationRequest {
  dateFrom?: string | null;
  dateTo?: string | null;
  amountFrom?: number | null;
  amountTo?: number | null;
  entryTypes?: number[] | null;
  bankAccountId?: string | null;
  creditCardId?: string | null;
  keyword?: string | null;
}

export interface PersonTimelinePointDto {
  period: string;
  given: number;
  taken: number;
  runningBalance: number;
}

export interface PersonAuditDto {
  person: PersonDto;
  totalGiven: number;
  totalTaken: number;
  balance: number;
  settlementsReceived: number;
  settlementsPaid: number;
  transactionCount: number;
  recentEntries: LedgerEntryDto[];
  timeline: PersonTimelinePointDto[];
}

export interface CreatePersonRequest {
  name: string;
  mobile?: string | null;
  email?: string | null;
  address?: AddressDto | null;
  relationship?: string | null;
  notes?: string | null;
  matchKeywords?: string | null;
}

export interface UpdatePersonRequest extends CreatePersonRequest {
  isActive: boolean;
}

export interface CreateLedgerEntryRequest {
  entryDate: string;
  amount: number;
  entryType: number;
  description?: string | null;
  referenceNumber?: string | null;
  transactionId?: string | null;
  bankAccountId?: string | null;
  creditCardId?: string | null;
}
