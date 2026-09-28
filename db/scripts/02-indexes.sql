/*
 * FinanceAudit360 - performance and maintenance indexes (PostgreSQL).
 *
 * Run AFTER 01-schema.sql. Everything here is additive and idempotent, so it is safe
 * to re-run after every deployment.
 */

/* ---------------------------------------------------------------------------
 * Covering index for the Transaction Explorer's default view: filter and sort
 * by date, then return the columns the grid always shows.
 * --------------------------------------------------------------------------- */
CREATE INDEX IF NOT EXISTS "IX_Transactions_Explorer_Covering"
	ON fin."Transactions" ("IsDeleted", "TransactionDate" DESC)
	INCLUDE ("Amount", "CreditAmount", "DebitAmount", "SignedAmount", "Direction",
			 "TransactionType", "Description", "ReferenceNumber", "Currency",
			 "VendorId", "CategoryId", "BankId", "BankAccountId", "CreditCardId",
			 "PersonId", "StatementId");

/* Keyword search scans the normalised description; a dedicated index keeps it sargable
   for prefix matches and reduces the scan width for LIKE 'prefix%' queries. */
CREATE INDEX IF NOT EXISTS "IX_Transactions_NormalizedDescription"
	ON fin."Transactions" ("NormalizedDescription")
	INCLUDE ("TransactionDate", "Amount", "Direction")
	WHERE "IsDeleted" = false;

/* Dashboard aggregates group by month over a date window. */
CREATE INDEX IF NOT EXISTS "IX_Transactions_Dashboard_Aggregate"
	ON fin."Transactions" ("TransactionDate")
	INCLUDE ("CreditAmount", "DebitAmount", "VendorId", "PersonId", "CreditCardId", "BankId")
	WHERE "IsDeleted" = false;

/* Person ledger paging. */
CREATE INDEX IF NOT EXISTS "IX_MoneyLedgers_Person_Date_Covering"
	ON fin."MoneyLedgers" ("PersonId", "EntryDate" DESC)
	INCLUDE ("Amount", "EntryType", "Description", "ReferenceNumber", "IsSettled")
	WHERE "IsDeleted" = false;

/* Audit log search is almost always "recent first, filtered by entity or user". */
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Timestamp_Covering"
	ON fin."AuditLogs" ("TimestampUtc" DESC)
	INCLUDE ("Action", "EntityName", "EntityId", "UserId", "UserName", "Endpoint", "StatusCode");

/* Expired refresh-token cleanup. */
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_Expiry"
	ON fin."RefreshTokens" ("ExpiresOnUtc")
	INCLUDE ("UserId", "RevokedOnUtc");

DO $$
BEGIN
	RAISE NOTICE 'FinanceAudit360 performance indexes verified.';
END $$;
