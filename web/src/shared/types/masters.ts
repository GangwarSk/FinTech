import type { PaginationRequest } from './api';
import type { AddressDto } from './dashboard';

export interface UploadStatementResponse {
  uploadId: string;
  statementFileId?: string | null;
  status: string;
  requiresPassword: boolean;
  detectedBank?: string | null;
  detectedKind?: string | null;
  message?: string | null;
}

export interface ProcessStatementRequest {
  uploadId: string;
  password?: string | null;
  overrideBankId?: string | null;
  allowDuplicate?: boolean;
}

export interface StatementProcessingResultDto {
  uploadId: string;
  statementId?: string | null;
  status: string;
  bankName?: string | null;
  statementKind?: string | null;
  parserName?: string | null;
  cardNumberMasked?: string | null;
  accountNumberMasked?: string | null;
  cardHolderName?: string | null;
  customerName?: string | null;
  customerEmail?: string | null;
  customerPhone?: string | null;
  billingAddress?: AddressDto | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  statementDate?: string | null;
  paymentDueDate?: string | null;
  openingBalance?: number | null;
  closingBalance?: number | null;
  minimumDue?: number | null;
  totalDue?: number | null;
  transactionsExtracted: number;
  transactionsImported: number;
  transactionsSkipped: number;
  durationMs: number;
  warnings: string[];
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface UploadHistoryDto {
  id: string;
  fileName: string;
  sizeInBytes: number;
  status: string;
  detectedBank?: string | null;
  detectedKind?: string | null;
  parserName?: string | null;
  requiresPassword: boolean;
  transactionsExtracted: number;
  transactionsImported: number;
  transactionsSkipped: number;
  startedOnUtc: string;
  completedOnUtc?: string | null;
  durationMs: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  statementId?: string | null;
  statementFileId?: string | null;
  createdBy?: string | null;
}

export interface UploadHistoryFilterRequest extends PaginationRequest {
  statuses?: number[] | null;
  from?: string | null;
  to?: string | null;
  keyword?: string | null;
}

/** An uploaded PDF together with everything it produced. Deleting it takes all of that with it. */
export interface StatementFileSummaryDto {
  id: string;
  originalFileName: string;
  displayName: string;
  sizeInBytes: number;
  bankId?: string | null;
  bankName?: string | null;
  detectedBank?: string | null;
  detectedKind?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  statementDate?: string | null;
  statementCount: number;
  transactionCount: number;
  uploadCount: number;
  uploadStatus?: string | null;
  uploadedOnUtc: string;
  uploadedBy?: string | null;
  isDeleted: boolean;
  deletedOnUtc?: string | null;
  deletedBy?: string | null;
}

export interface StatementFileDetailDto {
  file: StatementFileSummaryDto;
  isPasswordProtected: boolean;
  pageCount: number;
  contentHash: string;
  statements: StatementDto[];
  uploads: UploadHistoryDto[];
}

export interface StatementFileImpactDto {
  id: string;
  originalFileName: string;
  displayName: string;
  statementCount: number;
  transactionCount: number;
  uploadCount: number;
  isDeleted: boolean;
}

export interface StatementFileLookupDto {
  id: string;
  label: string;
  originalFileName: string;
  bankId?: string | null;
  bankName?: string | null;
  periodStart?: string | null;
  periodEnd?: string | null;
  transactionCount: number;
}

export interface StatementFileFilterRequest extends PaginationRequest {
  bankIds?: string[] | null;
  periodFrom?: string | null;
  periodTo?: string | null;
  keyword?: string | null;
  deletedOnly?: boolean;
}

export interface StatementDto {
  id: string;
  bankId: string;
  bankName: string;
  kind: string;
  status: string;
  statementNumber?: string | null;
  cardNumberMasked?: string | null;
  accountNumberMasked?: string | null;
  cardHolderName?: string | null;
  customerName?: string | null;
  periodStart: string;
  periodEnd: string;
  statementDate?: string | null;
  paymentDueDate?: string | null;
  openingBalance: number;
  closingBalance: number;
  minimumDue?: number | null;
  totalDue?: number | null;
  totalCredits: number;
  totalDebits: number;
  transactionCount: number;
  currency: string;
  parserName?: string | null;
  statementFileId?: string | null;
  originalFileName?: string | null;
  createdOnUtc: string;
}

export interface MasterFilterRequest extends PaginationRequest {
  keyword?: string | null;
  isActive?: boolean | null;
  bankId?: string | null;
}

export interface BankDto {
  id: string;
  name: string;
  shortName: string;
  code: number;
  codeName: string;
  ifsc?: string | null;
  website?: string | null;
  logoUrl?: string | null;
  statementKeywords?: string | null;
  isActive: boolean;
  accountCount: number;
  cardCount: number;
}

export interface BankAccountDto {
  id: string;
  bankId: string;
  bankName: string;
  accountNumberMasked: string;
  accountNumberLast4: string;
  accountHolderName: string;
  accountType: number;
  accountTypeName: string;
  nickname?: string | null;
  displayName: string;
  ifsc?: string | null;
  branchName?: string | null;
  currency: string;
  currentBalance: number;
  balanceAsOfUtc?: string | null;
  openedOn?: string | null;
  isActive: boolean;
  isPrimary: boolean;
  notes?: string | null;
  transactionCount: number;
}

export interface CreditCardDto {
  id: string;
  bankId: string;
  bankName: string;
  cardNumberMasked: string;
  cardNumberLast4: string;
  cardHolderName: string;
  nickname?: string | null;
  displayName: string;
  productName?: string | null;
  network: number;
  networkName: string;
  creditLimit?: number | null;
  cashLimit?: number | null;
  currentOutstanding: number;
  availableLimit?: number | null;
  statementDayOfMonth?: number | null;
  paymentDueDayOfMonth?: number | null;
  expiryDate?: string | null;
  currency: string;
  isActive: boolean;
  notes?: string | null;
  transactionCount: number;
  statementCount: number;
}

export interface CategoryDto {
  id: string;
  name: string;
  code?: string | null;
  description?: string | null;
  colorHex?: string | null;
  icon?: string | null;
  parentCategoryId?: string | null;
  parentCategoryName?: string | null;
  isSystemCategory: boolean;
  isActive: boolean;
  displayOrder: number;
  matchKeywords?: string | null;
  transactionCount: number;
}

export interface VendorDto {
  id: string;
  name: string;
  displayName?: string | null;
  category?: string | null;
  defaultCategoryId?: string | null;
  defaultCategoryName?: string | null;
  website?: string | null;
  matchKeywords?: string | null;
  notes?: string | null;
  isActive: boolean;
  transactionCount: number;
  totalSpend: number;
}

export interface UserDto {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginOnUtc?: string | null;
  createdOnUtc: string;
  roles: { id: string; name: string }[];
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string | null;
  isSystemRole: boolean;
  userCount: number;
  permissions: string[];
}

export interface SettingDto {
  id: string;
  key: string;
  value?: string | null;
  dataType: number;
  dataTypeName: string;
  category: string;
  description?: string | null;
  defaultValue?: string | null;
  isSystem: boolean;
  isSecret: boolean;
}
