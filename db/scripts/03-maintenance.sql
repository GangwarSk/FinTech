/*
 * FinanceAudit360 - maintenance helpers (PostgreSQL).
 *
 * Optional. These objects support housekeeping and ad-hoc auditing; the application
 * itself does not depend on them.
 */

/* ---------------------------------------------------------------------------
 * Convenience view: every live transaction with its master data resolved.
 * --------------------------------------------------------------------------- */
CREATE OR REPLACE VIEW fin."vw_TransactionDetails"
AS
SELECT
	t."Id",
	t."TransactionDate",
	t."PostingDate",
	t."Amount",
	t."CreditAmount",
	t."DebitAmount",
	t."SignedAmount",
	t."Currency",
	t."Direction",
	t."TransactionType",
	t."Description",
	t."ReferenceNumber",
	b."Name"                AS "BankName",
	c."CardNumberLast4"     AS "CardLast4",
	c."Nickname"            AS "CardNickname",
	a."AccountNumberLast4"  AS "AccountLast4",
	a."Nickname"            AS "AccountNickname",
	v."Name"                AS "VendorName",
	cat."Name"              AS "CategoryName",
	p."Name"                AS "PersonName",
	s."PeriodStart",
	s."PeriodEnd",
	t."CreatedOnUtc",
	t."CreatedBy"
FROM fin."Transactions" t
LEFT JOIN fin."Banks"                 b   ON b."Id"   = t."BankId"        AND b."IsDeleted" = false
LEFT JOIN fin."CreditCards"           c   ON c."Id"   = t."CreditCardId"  AND c."IsDeleted" = false
LEFT JOIN fin."BankAccounts"          a   ON a."Id"   = t."BankAccountId" AND a."IsDeleted" = false
LEFT JOIN fin."Vendors"               v   ON v."Id"   = t."VendorId"      AND v."IsDeleted" = false
LEFT JOIN fin."TransactionCategories" cat ON cat."Id" = t."CategoryId"    AND cat."IsDeleted" = false
LEFT JOIN fin."Persons"               p   ON p."Id"   = t."PersonId"      AND p."IsDeleted" = false
LEFT JOIN fin."CreditCardStatements"  s   ON s."Id"   = t."StatementId"   AND s."IsDeleted" = false
WHERE t."IsDeleted" = false;

/* ---------------------------------------------------------------------------
 * Rebuilds the denormalised person balances from the ledger. Use after a bulk
 * import or if the hourly Quartz job has been disabled.
 * --------------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION fin."usp_RecalculatePersonBalances"()
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
	updated integer;
BEGIN
	WITH totals AS (
		SELECT
			l."PersonId",
			SUM(CASE WHEN l."EntryType" = 1 THEN l."Amount" ELSE 0 END) AS given_amount,
			SUM(CASE WHEN l."EntryType" = 2 THEN l."Amount" ELSE 0 END) AS taken_amount,
			SUM(CASE WHEN l."EntryType" = 3 THEN l."Amount" ELSE 0 END) AS settled_in,
			SUM(CASE WHEN l."EntryType" = 4 THEN l."Amount" ELSE 0 END) AS settled_out,
			MAX(l."EntryDate")                                          AS last_activity_on
		FROM fin."MoneyLedgers" l
		WHERE l."IsDeleted" = false
		GROUP BY l."PersonId"
	)
	UPDATE fin."Persons" p
	SET "TotalGiven"         = COALESCE(t.given_amount, 0),
		"TotalTaken"         = COALESCE(t.taken_amount, 0),
		"OutstandingBalance" = COALESCE(t.given_amount, 0) - COALESCE(t.settled_in, 0)
							   - (COALESCE(t.taken_amount, 0) - COALESCE(t.settled_out, 0)),
		"LastActivityOn"     = t.last_activity_on
	FROM totals t
	WHERE t."PersonId" = p."Id" AND p."IsDeleted" = false;

	GET DIAGNOSTICS updated = ROW_COUNT;
	RETURN updated;
END $$;

/* ---------------------------------------------------------------------------
 * Rebuilds credit card outstanding balances from transaction history.
 * --------------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION fin."usp_RecalculateCardOutstanding"()
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
	updated integer;
BEGIN
	WITH totals AS (
		SELECT
			t."CreditCardId",
			SUM(t."DebitAmount") - SUM(t."CreditAmount") AS net_amount
		FROM fin."Transactions" t
		WHERE t."IsDeleted" = false AND t."CreditCardId" IS NOT NULL
		GROUP BY t."CreditCardId"
	)
	UPDATE fin."CreditCards" c
	SET "CurrentOutstanding" = COALESCE(t.net_amount, 0),
		"OutstandingAsOfUtc" = (NOW() AT TIME ZONE 'utc')
	FROM totals t
	WHERE t."CreditCardId" = c."Id" AND c."IsDeleted" = false;

	GET DIAGNOSTICS updated = ROW_COUNT;
	RETURN updated;
END $$;

/* ---------------------------------------------------------------------------
 * Removes refresh tokens that expired more than 30 days ago.
 * --------------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION fin."usp_PurgeExpiredRefreshTokens"()
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
	purged integer;
BEGIN
	DELETE FROM fin."RefreshTokens"
	WHERE "ExpiresOnUtc" < (NOW() AT TIME ZONE 'utc') - INTERVAL '30 days';

	GET DIAGNOSTICS purged = ROW_COUNT;
	RETURN purged;
END $$;

/* ---------------------------------------------------------------------------
 * Finds transactions that share a dedupe hash - should always return zero rows.
 * Useful as a data-quality check after a manual import.
 * --------------------------------------------------------------------------- */
CREATE OR REPLACE VIEW fin."vw_DuplicateTransactions"
AS
SELECT
	t."DedupeHash",
	COUNT(*)                 AS "Occurrences",
	MIN(t."TransactionDate") AS "FirstSeen",
	MAX(t."TransactionDate") AS "LastSeen"
FROM fin."Transactions" t
WHERE t."IsDeleted" = false
GROUP BY t."DedupeHash"
HAVING COUNT(*) > 1;

DO $$
BEGIN
	RAISE NOTICE 'FinanceAudit360 maintenance objects created.';
END $$;
