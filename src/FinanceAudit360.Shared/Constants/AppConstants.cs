namespace FinanceAudit360.Shared.Constants;

public static class AppConstants
{
    public const string ApplicationName = "FinanceAudit360";
    public const string DefaultCurrency = "INR";
    public const string CorsPolicyName = "FinanceAudit360Cors";
    public const string RateLimitPolicyGlobal = "global";
    public const string RateLimitPolicyAuth = "auth";
    public const string RateLimitPolicyUpload = "upload";
    public const long MaxUploadBytes = 25L * 1024 * 1024;
    public const string CorrelationIdHeader = "X-Correlation-Id";
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Auditor = "Auditor";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Administrator, Auditor, Viewer];
}

public static class Permissions
{
    public const string TransactionsRead = "transactions.read";
    public const string TransactionsWrite = "transactions.write";
    public const string StatementsUpload = "statements.upload";
    public const string StatementsRead = "statements.read";

    /// <summary>Deleting an uploaded file, and emptying the recycle bin, is an administrator-only action.</summary>
    public const string StatementsDelete = "statements.delete";

    public const string PersonsRead = "persons.read";
    public const string PersonsWrite = "persons.write";
    public const string MastersRead = "masters.read";
    public const string MastersWrite = "masters.write";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string ReportsRead = "reports.read";

    public static readonly IReadOnlyList<string> All =
    [
        TransactionsRead, TransactionsWrite, StatementsUpload, StatementsRead, StatementsDelete,
        PersonsRead, PersonsWrite, MastersRead, MastersWrite, UsersManage,
        SettingsManage, ReportsRead
    ];

    public static readonly IReadOnlyList<string> ReadOnly =
    [
        TransactionsRead, StatementsRead, PersonsRead, MastersRead, ReportsRead
    ];

    public static readonly IReadOnlyList<string> Auditor =
    [
        TransactionsRead, TransactionsWrite, StatementsUpload, StatementsRead,
        PersonsRead, PersonsWrite, MastersRead, ReportsRead
    ];
}

public static class AuthClaims
{
    public const string UserId = "uid";
    public const string Permission = "perm";
    public const string SessionId = "sid";
    public const string TokenType = "ttype";
    public const string FullName = "name";
}
