CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'fin') THEN
            CREATE SCHEMA fin;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."AuditLogs" (
        "Id" uuid NOT NULL,
        "Action" integer NOT NULL,
        "EntityName" character varying(150) NOT NULL,
        "EntityId" character varying(100),
        "UserId" uuid,
        "UserName" character varying(100),
        "TimestampUtc" timestamp with time zone NOT NULL,
        "IpAddress" character varying(64),
        "UserAgent" character varying(500),
        "CorrelationId" character varying(100),
        "HttpMethod" character varying(10),
        "Endpoint" character varying(500),
        "StatusCode" integer,
        "DurationMs" bigint,
        "OldValuesJson" text,
        "NewValuesJson" text,
        "AffectedColumns" character varying(2000),
        "Message" character varying(2000),
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Banks" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "ShortName" character varying(50) NOT NULL,
        "Code" integer NOT NULL,
        "Ifsc" character varying(20),
        "LogoUrl" character varying(500),
        "Website" character varying(300),
        "IsActive" boolean NOT NULL,
        "StatementKeywords" character varying(1000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Banks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Persons" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "NormalizedName" character varying(200) NOT NULL,
        "Mobile" character varying(20),
        "Email" character varying(256),
        "AddressLine1" character varying(250),
        "AddressLine2" character varying(250),
        "AddressCity" character varying(100),
        "AddressState" character varying(100),
        "AddressPostalCode" character varying(20),
        "AddressCountry" character varying(100),
        "Relationship" character varying(100),
        "Notes" character varying(2000),
        "IsActive" boolean NOT NULL,
        "TotalGiven" numeric(18,2) NOT NULL,
        "TotalTaken" numeric(18,2) NOT NULL,
        "OutstandingBalance" numeric(18,2) NOT NULL,
        "LastActivityOn" timestamp with time zone,
        "MatchKeywords" character varying(1000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Persons" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Roles" (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "NormalizedName" character varying(100) NOT NULL,
        "Description" character varying(500),
        "IsSystemRole" boolean NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Settings" (
        "Id" uuid NOT NULL,
        "Key" character varying(150) NOT NULL,
        "Value" character varying(4000),
        "DataType" integer NOT NULL,
        "Category" character varying(100) NOT NULL,
        "Description" character varying(500),
        "DefaultValue" character varying(4000),
        "IsSystem" boolean NOT NULL,
        "IsSecret" boolean NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."StatementFiles" (
        "Id" uuid NOT NULL,
        "OriginalFileName" character varying(260) NOT NULL,
        "StoredFileName" character varying(260) NOT NULL,
        "RelativePath" character varying(500) NOT NULL,
        "ContentType" character varying(150) NOT NULL,
        "SizeInBytes" bigint NOT NULL,
        "ContentHash" character varying(64) NOT NULL,
        "IsPasswordProtected" boolean NOT NULL,
        "PageCount" integer NOT NULL,
        "DetectedBank" integer NOT NULL,
        "DetectedKind" integer NOT NULL,
        "ExtractedTextPath" character varying(500),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_StatementFiles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."TransactionCategories" (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Code" character varying(50),
        "Description" character varying(500),
        "ColorHex" character varying(10),
        "Icon" character varying(50),
        "ParentCategoryId" uuid,
        "IsSystemCategory" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "MatchKeywords" character varying(1000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_TransactionCategories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TransactionCategories_TransactionCategories_ParentCategoryId" FOREIGN KEY ("ParentCategoryId") REFERENCES fin."TransactionCategories" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Users" (
        "Id" uuid NOT NULL,
        "UserName" character varying(100) NOT NULL,
        "NormalizedUserName" character varying(100) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "NormalizedEmail" character varying(256) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "PasswordHash" character varying(500) NOT NULL,
        "PhoneNumber" character varying(20),
        "IsActive" boolean NOT NULL,
        "MustChangePassword" boolean NOT NULL,
        "LastLoginOnUtc" timestamp with time zone,
        "AccessFailedCount" integer NOT NULL,
        "LockoutEndUtc" timestamp with time zone,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."BankAccounts" (
        "Id" uuid NOT NULL,
        "BankId" uuid NOT NULL,
        "AccountNumberMasked" character varying(50) NOT NULL,
        "AccountNumberLast4" character varying(4) NOT NULL,
        "AccountHolderName" character varying(200) NOT NULL,
        "AccountType" integer NOT NULL,
        "Nickname" character varying(100),
        "Ifsc" character varying(20),
        "BranchName" character varying(200),
        "Currency" character varying(3) NOT NULL,
        "CurrentBalance" numeric(18,2) NOT NULL,
        "BalanceAsOfUtc" timestamp with time zone,
        "OpenedOn" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        "IsPrimary" boolean NOT NULL,
        "Notes" character varying(2000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_BankAccounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_BankAccounts_Banks_BankId" FOREIGN KEY ("BankId") REFERENCES fin."Banks" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."CreditCards" (
        "Id" uuid NOT NULL,
        "BankId" uuid NOT NULL,
        "CardNumberMasked" character varying(25) NOT NULL,
        "CardNumberLast4" character varying(4) NOT NULL,
        "CardHolderName" character varying(200) NOT NULL,
        "Nickname" character varying(100),
        "ProductName" character varying(150),
        "Network" integer NOT NULL,
        "CreditLimit" numeric(18,2),
        "CashLimit" numeric(18,2),
        "CurrentOutstanding" numeric(18,2) NOT NULL,
        "OutstandingAsOfUtc" timestamp with time zone,
        "StatementDayOfMonth" integer,
        "PaymentDueDayOfMonth" integer,
        "ExpiryDate" timestamp with time zone,
        "Currency" character varying(3) NOT NULL,
        "IsActive" boolean NOT NULL,
        "Notes" character varying(2000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_CreditCards" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CreditCards_Banks_BankId" FOREIGN KEY ("BankId") REFERENCES fin."Banks" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."RolePermissions" (
        "Id" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "Permission" character varying(100) NOT NULL,
        CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES fin."Roles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Vendors" (
        "Id" uuid NOT NULL,
        "Name" character varying(250) NOT NULL,
        "NormalizedName" character varying(250) NOT NULL,
        "DisplayName" character varying(250),
        "Category" character varying(100),
        "DefaultCategoryId" uuid,
        "Website" character varying(300),
        "Notes" character varying(2000),
        "IsActive" boolean NOT NULL,
        "MatchKeywords" character varying(2000),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Vendors" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Vendors_TransactionCategories_DefaultCategoryId" FOREIGN KEY ("DefaultCategoryId") REFERENCES fin."TransactionCategories" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "ExpiresOnUtc" timestamp with time zone NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedByIp" character varying(64),
        "UserAgent" character varying(500),
        "RevokedOnUtc" timestamp with time zone,
        "RevokedReason" character varying(200),
        "ReplacedByTokenHash" character varying(128),
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES fin."Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."UserRoles" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "AssignedOnUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserRoles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES fin."Roles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES fin."Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."CreditCardStatements" (
        "Id" uuid NOT NULL,
        "BankId" uuid NOT NULL,
        "CreditCardId" uuid,
        "BankAccountId" uuid,
        "StatementFileId" uuid,
        "Kind" integer NOT NULL,
        "Status" integer NOT NULL,
        "StatementNumber" character varying(100),
        "CardNumberMasked" character varying(25),
        "CardNumberLast4" character varying(4),
        "AccountNumberMasked" character varying(50),
        "CardHolderName" character varying(200),
        "CustomerName" character varying(200),
        "CustomerEmail" character varying(256),
        "CustomerPhone" character varying(20),
        "AddressLine1" character varying(250),
        "AddressLine2" character varying(250),
        "AddressCity" character varying(100),
        "AddressState" character varying(100),
        "AddressPostalCode" character varying(20),
        "AddressCountry" character varying(100),
        "PeriodStart" timestamp with time zone NOT NULL,
        "PeriodEnd" timestamp with time zone NOT NULL,
        "StatementDate" timestamp with time zone,
        "PaymentDueDate" timestamp with time zone,
        "OpeningBalance" numeric(18,2) NOT NULL,
        "ClosingBalance" numeric(18,2) NOT NULL,
        "MinimumDue" numeric(18,2),
        "TotalDue" numeric(18,2),
        "CreditLimit" numeric(18,2),
        "AvailableCreditLimit" numeric(18,2),
        "TotalCredits" numeric(18,2) NOT NULL,
        "TotalDebits" numeric(18,2) NOT NULL,
        "TransactionCount" integer NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "ParserName" character varying(100),
        "DedupeKey" character varying(150) NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_CreditCardStatements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CreditCardStatements_BankAccounts_BankAccountId" FOREIGN KEY ("BankAccountId") REFERENCES fin."BankAccounts" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CreditCardStatements_Banks_BankId" FOREIGN KEY ("BankId") REFERENCES fin."Banks" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CreditCardStatements_CreditCards_CreditCardId" FOREIGN KEY ("CreditCardId") REFERENCES fin."CreditCards" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CreditCardStatements_StatementFiles_StatementFileId" FOREIGN KEY ("StatementFileId") REFERENCES fin."StatementFiles" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."Transactions" (
        "Id" uuid NOT NULL,
        "TransactionDate" timestamp with time zone NOT NULL,
        "PostingDate" timestamp with time zone NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "CreditAmount" numeric(18,2) NOT NULL,
        "DebitAmount" numeric(18,2) NOT NULL,
        "SignedAmount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Direction" integer NOT NULL,
        "TransactionType" integer NOT NULL,
        "Source" integer NOT NULL,
        "Description" character varying(1000) NOT NULL,
        "NormalizedDescription" character varying(1000) NOT NULL,
        "ReferenceNumber" character varying(100),
        "MerchantRawText" character varying(500),
        "Location" character varying(200),
        "Notes" character varying(2000),
        "VendorId" uuid,
        "CategoryId" uuid,
        "BankAccountId" uuid,
        "CreditCardId" uuid,
        "BankId" uuid,
        "PersonId" uuid,
        "StatementId" uuid,
        "IsReconciled" boolean NOT NULL,
        "IsRecurring" boolean NOT NULL,
        "IsDisputed" boolean NOT NULL,
        "DedupeHash" character varying(64) NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_Transactions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Transactions_BankAccounts_BankAccountId" FOREIGN KEY ("BankAccountId") REFERENCES fin."BankAccounts" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transactions_Banks_BankId" FOREIGN KEY ("BankId") REFERENCES fin."Banks" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transactions_CreditCardStatements_StatementId" FOREIGN KEY ("StatementId") REFERENCES fin."CreditCardStatements" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transactions_CreditCards_CreditCardId" FOREIGN KEY ("CreditCardId") REFERENCES fin."CreditCards" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Transactions_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES fin."Persons" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_Transactions_TransactionCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES fin."TransactionCategories" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_Transactions_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES fin."Vendors" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."UploadHistories" (
        "Id" uuid NOT NULL,
        "FileName" character varying(260) NOT NULL,
        "SizeInBytes" bigint NOT NULL,
        "Status" integer NOT NULL,
        "StatementFileId" uuid,
        "StatementId" uuid,
        "DetectedBank" integer NOT NULL,
        "DetectedKind" integer NOT NULL,
        "ParserName" character varying(100),
        "RequiresPassword" boolean NOT NULL,
        "TransactionsExtracted" integer NOT NULL,
        "TransactionsImported" integer NOT NULL,
        "TransactionsSkipped" integer NOT NULL,
        "StartedOnUtc" timestamp with time zone NOT NULL,
        "CompletedOnUtc" timestamp with time zone,
        "DurationMs" bigint NOT NULL,
        "ErrorCode" character varying(100),
        "ErrorMessage" character varying(2000),
        "WarningsJson" character varying(4000),
        "UploadedByIp" character varying(64),
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_UploadHistories" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UploadHistories_CreditCardStatements_StatementId" FOREIGN KEY ("StatementId") REFERENCES fin."CreditCardStatements" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_UploadHistories_StatementFiles_StatementFileId" FOREIGN KEY ("StatementFileId") REFERENCES fin."StatementFiles" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE TABLE fin."MoneyLedgers" (
        "Id" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "EntryDate" timestamp with time zone NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "EntryType" integer NOT NULL,
        "Description" character varying(1000),
        "ReferenceNumber" character varying(100),
        "TransactionId" uuid,
        "BankAccountId" uuid,
        "CreditCardId" uuid,
        "SettledOn" timestamp with time zone,
        "IsSettled" boolean NOT NULL,
        "CreatedOnUtc" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(100),
        "ModifiedOnUtc" timestamp with time zone,
        "ModifiedBy" character varying(100),
        "IsDeleted" boolean NOT NULL,
        "DeletedOnUtc" timestamp with time zone,
        "DeletedBy" character varying(100),
        CONSTRAINT "PK_MoneyLedgers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MoneyLedgers_BankAccounts_BankAccountId" FOREIGN KEY ("BankAccountId") REFERENCES fin."BankAccounts" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_MoneyLedgers_CreditCards_CreditCardId" FOREIGN KEY ("CreditCardId") REFERENCES fin."CreditCards" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_MoneyLedgers_Persons_PersonId" FOREIGN KEY ("PersonId") REFERENCES fin."Persons" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_MoneyLedgers_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES fin."Transactions" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_Entity" ON fin."AuditLogs" ("EntityName", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_Timestamp" ON fin."AuditLogs" ("TimestampUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_User" ON fin."AuditLogs" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_BankAccounts_IsActive" ON fin."BankAccounts" ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_BankAccounts_Bank_Last4" ON fin."BankAccounts" ("BankId", "AccountNumberLast4") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Banks_Code" ON fin."Banks" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Banks_Name" ON fin."Banks" ("Name") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_CreditCards_BankId" ON fin."CreditCards" ("BankId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_CreditCards_IsActive" ON fin."CreditCards" ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_CreditCards_Last4" ON fin."CreditCards" ("CardNumberLast4");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_CreditCardStatements_StatementFileId" ON fin."CreditCardStatements" ("StatementFileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Statements_Bank_Period" ON fin."CreditCardStatements" ("BankId", "PeriodStart", "PeriodEnd");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Statements_BankAccount" ON fin."CreditCardStatements" ("BankAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Statements_CreditCard" ON fin."CreditCardStatements" ("CreditCardId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Statements_DedupeKey" ON fin."CreditCardStatements" ("DedupeKey") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_MoneyLedgers_BankAccountId" ON fin."MoneyLedgers" ("BankAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_MoneyLedgers_CreditCardId" ON fin."MoneyLedgers" ("CreditCardId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_MoneyLedgers_EntryType" ON fin."MoneyLedgers" ("EntryType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_MoneyLedgers_Person_Date" ON fin."MoneyLedgers" ("PersonId", "EntryDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_MoneyLedgers_TransactionId" ON fin."MoneyLedgers" ("TransactionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Persons_Mobile" ON fin."Persons" ("Mobile");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Persons_Outstanding" ON fin."Persons" ("OutstandingBalance");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Persons_Name" ON fin."Persons" ("NormalizedName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_RefreshTokens_User_Expiry" ON fin."RefreshTokens" ("UserId", "ExpiresOnUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_RefreshTokens_TokenHash" ON fin."RefreshTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_RolePermissions_Role_Permission" ON fin."RolePermissions" ("RoleId", "Permission");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Roles_Name" ON fin."Roles" ("NormalizedName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Settings_Category" ON fin."Settings" ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Settings_Key" ON fin."Settings" ("Key") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_StatementFiles_ContentHash" ON fin."StatementFiles" ("ContentHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_TransactionCategories_ParentCategoryId" ON fin."TransactionCategories" ("ParentCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Categories_Name" ON fin."TransactionCategories" ("Name") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Account_Date" ON fin."Transactions" ("BankAccountId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Bank_Date" ON fin."Transactions" ("BankId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Card_Date" ON fin."Transactions" ("CreditCardId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Category_Date" ON fin."Transactions" ("CategoryId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Direction" ON fin."Transactions" ("Direction");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_IsDeleted" ON fin."Transactions" ("IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Person_Date" ON fin."Transactions" ("PersonId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_PostingDate" ON fin."Transactions" ("PostingDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Reference" ON fin."Transactions" ("ReferenceNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Statement" ON fin."Transactions" ("StatementId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_TransactionDate" ON fin."Transactions" ("TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Transactions_Vendor_Date" ON fin."Transactions" ("VendorId", "TransactionDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Transactions_DedupeHash" ON fin."Transactions" ("DedupeHash") WHERE "IsDeleted" = false AND "StatementId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_UploadHistories_StartedOn" ON fin."UploadHistories" ("StartedOnUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_UploadHistories_StatementFileId" ON fin."UploadHistories" ("StatementFileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_UploadHistories_StatementId" ON fin."UploadHistories" ("StatementId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_UploadHistories_Status" ON fin."UploadHistories" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_UserRoles_RoleId" ON fin."UserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_UserRoles_User_Role" ON fin."UserRoles" ("UserId", "RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Users_IsDeleted" ON fin."Users" ("IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Users_Email" ON fin."Users" ("NormalizedEmail") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Users_UserName" ON fin."Users" ("NormalizedUserName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE INDEX "IX_Vendors_DefaultCategoryId" ON fin."Vendors" ("DefaultCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    CREATE UNIQUE INDEX "UX_Vendors_Name" ON fin."Vendors" ("NormalizedName") WHERE "IsDeleted" = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926145328_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260926145328_InitialCreate', '10.0.12');
    END IF;
END $EF$;
COMMIT;

