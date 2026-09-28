import type { PaginationRequest } from './api';

export type TransactionDirectionName = 'Credit' | 'Debit';

export interface TransactionDto {
  id: string;
  transactionDate: string;
  postingDate: string;
  amount: number;
  creditAmount: number;
  debitAmount: number;
  signedAmount: number;
  currency: string;
  direction: TransactionDirectionName;
  transactionType: string;
  source: string;
  description: string;
  referenceNumber?: string | null;
  merchantRawText?: string | null;
  location?: string | null;
  notes?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  bankId?: string | null;
  bankName?: string | null;
  bankAccountId?: string | null;
  bankAccountName?: string | null;
  creditCardId?: string | null;
  creditCardName?: string | null;
  personId?: string | null;
  personName?: string | null;
  statementId?: string | null;
  statementPeriod?: string | null;
  statementFileId?: string | null;
  sourceFileName?: string | null;
  isReconciled: boolean;
  isRecurring: boolean;
  isDisputed: boolean;
  createdOnUtc: string;
  createdBy?: string | null;
  modifiedOnUtc?: string | null;
  modifiedBy?: string | null;
}

export interface TransactionGroupDto {
  key: string;
  label: string;
  count: number;
  totalCredit: number;
  totalDebit: number;
  net: number;
}

export interface TransactionSearchResult {
  items: TransactionDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  totalCredit: number;
  totalDebit: number;
  netAmount: number;
  groups: TransactionGroupDto[];
}

export interface TransactionFilterRequest extends PaginationRequest {
  dateFrom?: string | null;
  dateTo?: string | null;
  postingDateFrom?: string | null;
  postingDateTo?: string | null;
  amountFrom?: number | null;
  amountTo?: number | null;
  creditOnly?: boolean | null;
  debitOnly?: boolean | null;
  bankIds?: string[] | null;
  creditCardIds?: string[] | null;
  bankAccountIds?: string[] | null;
  vendorIds?: string[] | null;
  personIds?: string[] | null;
  categoryIds?: string[] | null;
  statementIds?: string[] | null;
  /** Narrows to the transactions imported from specific uploaded PDFs. */
  statementFileIds?: string[] | null;
  transactionTypes?: number[] | null;
  keyword?: string | null;
  referenceNumber?: string | null;
  isReconciled?: boolean | null;
  isDisputed?: boolean | null;
  isRecurring?: boolean | null;
  groupBy?: string | null;
}

export interface CreateTransactionRequest {
  transactionDate: string;
  postingDate?: string | null;
  amount: number;
  direction: number;
  transactionType: number;
  description: string;
  referenceNumber?: string | null;
  notes?: string | null;
  bankId?: string | null;
  bankAccountId?: string | null;
  creditCardId?: string | null;
  vendorId?: string | null;
  categoryId?: string | null;
  personId?: string | null;
  currency?: string | null;
}

export interface BulkAssignRequest {
  transactionIds: string[];
  vendorId?: string | null;
  categoryId?: string | null;
  personId?: string | null;
}
