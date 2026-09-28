using System.Linq.Expressions;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.ValueObjects;

namespace FinanceAudit360.Application.Common.Mappings;

/// <summary>
/// Hand-written projections. Every expression here is EF-translatable, so grids are served by a
/// single SELECT with no entity materialisation and no lazy loading.
/// </summary>
public static class ProjectionMappings
{
    public static readonly Expression<Func<Transaction, TransactionDto>> TransactionProjection = t => new TransactionDto(
        t.Id,
        t.TransactionDate,
        t.PostingDate,
        t.Amount,
        t.CreditAmount,
        t.DebitAmount,
        t.SignedAmount,
        t.Currency,
        t.Direction.ToString(),
        t.TransactionType.ToString(),
        t.Source.ToString(),
        t.Description,
        t.ReferenceNumber,
        t.MerchantRawText,
        t.Location,
        t.Notes,
        t.VendorId,
        t.Vendor != null ? t.Vendor.Name : null,
        t.CategoryId,
        t.Category != null ? t.Category.Name : null,
        t.BankId,
        t.Bank != null ? t.Bank.Name : null,
        t.BankAccountId,
        t.BankAccount != null ? (t.BankAccount.Nickname ?? "A/c ****" + t.BankAccount.AccountNumberLast4) : null,
        t.CreditCardId,
        t.CreditCard != null ? (t.CreditCard.Nickname ?? "Card ****" + t.CreditCard.CardNumber.Last4) : null,
        t.PersonId,
        t.Person != null ? t.Person.Name : null,
        t.StatementId,
        t.Statement != null ? t.Statement.PeriodStart.ToString("yyyy-MM-dd") + " to " + t.Statement.PeriodEnd.ToString("yyyy-MM-dd") : null,
        t.Statement != null ? t.Statement.StatementFileId : null,
        t.Statement != null && t.Statement.StatementFile != null ? t.Statement.StatementFile.OriginalFileName : null,
        t.IsReconciled,
        t.IsRecurring,
        t.IsDisputed,
        t.CreatedOnUtc,
        t.CreatedBy,
        t.ModifiedOnUtc,
        t.ModifiedBy);

    public static readonly Expression<Func<MoneyLedger, LedgerEntryDto>> LedgerProjection = l => new LedgerEntryDto(
        l.Id,
        l.PersonId,
        l.EntryDate,
        l.Amount,
        l.EntryType == Domain.Enums.LedgerEntryType.Given || l.EntryType == Domain.Enums.LedgerEntryType.SettlementPaid
            ? l.Amount
            : -l.Amount,
        l.Currency,
        l.EntryType.ToString(),
        (int)l.EntryType,
        l.Description,
        l.ReferenceNumber,
        l.TransactionId,
        l.BankAccountId,
        l.BankAccount != null ? (l.BankAccount.Nickname ?? "A/c ****" + l.BankAccount.AccountNumberLast4) : null,
        l.CreditCardId,
        l.CreditCard != null ? (l.CreditCard.Nickname ?? "Card ****" + l.CreditCard.CardNumber.Last4) : null,
        l.IsSettled,
        l.SettledOn,
        l.CreatedOnUtc);

    public static readonly Expression<Func<Person, PersonDto>> PersonProjection = p => new PersonDto(
        p.Id,
        p.Name,
        p.Mobile,
        p.Email,
        new AddressDto(
            p.Address.Line1,
            p.Address.Line2,
            p.Address.City,
            p.Address.State,
            p.Address.PostalCode,
            p.Address.Country),
        p.Relationship,
        p.Notes,
        p.MatchKeywords,
        p.IsActive,
        p.TotalGiven,
        p.TotalTaken,
        p.OutstandingBalance,
        p.LastActivityOn,
        p.LedgerEntries.Count(e => !e.IsDeleted),
        p.CreatedOnUtc);

    public static readonly Expression<Func<Bank, BankDto>> BankProjection = b => new BankDto(
        b.Id,
        b.Name,
        b.ShortName,
        (int)b.Code,
        b.Code.ToString(),
        b.Ifsc,
        b.Website,
        b.LogoUrl,
        b.StatementKeywords,
        b.IsActive,
        b.Accounts.Count(a => !a.IsDeleted),
        b.CreditCards.Count(c => !c.IsDeleted));

    public static readonly Expression<Func<TransactionCategory, CategoryDto>> CategoryProjection = c => new CategoryDto(
        c.Id,
        c.Name,
        c.Code,
        c.Description,
        c.ColorHex,
        c.Icon,
        c.ParentCategoryId,
        c.ParentCategory != null ? c.ParentCategory.Name : null,
        c.IsSystemCategory,
        c.IsActive,
        c.DisplayOrder,
        c.MatchKeywords,
        0);

    public static AddressDto ToDto(this Address address) =>
        new(address.Line1, address.Line2, address.City, address.State, address.PostalCode, address.Country);

    public static Address ToValueObject(this AddressDto? dto) =>
        dto is null
            ? Address.Empty
            : Address.Create(dto.Line1, dto.Line2, dto.City, dto.State, dto.PostalCode, dto.Country ?? "India");

    public static UserDto ToDto(this User user, IReadOnlyList<LookupRoleDto> roles) => new(
        user.Id,
        user.UserName,
        user.Email,
        user.FullName,
        user.PhoneNumber,
        user.IsActive,
        user.MustChangePassword,
        user.LastLoginOnUtc,
        user.CreatedOnUtc,
        roles);

    public static RoleDto ToDto(this Role role, int userCount) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsSystemRole,
        userCount,
        role.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList());

    public static BankAccountDto ToDto(this BankAccount account, string bankName, int transactionCount) => new(
        account.Id,
        account.BankId,
        bankName,
        account.AccountNumberMasked,
        account.AccountNumberLast4,
        account.AccountHolderName,
        (int)account.AccountType,
        account.AccountType.ToString(),
        account.Nickname,
        account.Nickname ?? $"{bankName} ****{account.AccountNumberLast4}",
        account.Ifsc,
        account.BranchName,
        account.Currency,
        account.CurrentBalance,
        account.BalanceAsOfUtc,
        account.OpenedOn,
        account.IsActive,
        account.IsPrimary,
        account.Notes,
        transactionCount);

    public static CreditCardDto ToDto(this CreditCard card, string bankName, int transactionCount, int statementCount) => new(
        card.Id,
        card.BankId,
        bankName,
        card.CardNumber.Masked,
        card.CardNumber.Last4,
        card.CardHolderName,
        card.Nickname,
        card.Nickname ?? $"{bankName} ****{card.CardNumber.Last4}",
        card.ProductName,
        (int)card.Network,
        card.Network.ToString(),
        card.CreditLimit,
        card.CashLimit,
        card.CurrentOutstanding,
        card.AvailableLimit,
        card.StatementDayOfMonth,
        card.PaymentDueDayOfMonth,
        card.ExpiryDate,
        card.Currency,
        card.IsActive,
        card.Notes,
        transactionCount,
        statementCount);

    public static VendorDto ToDto(this Vendor vendor, string? defaultCategoryName, int transactionCount, decimal totalSpend) => new(
        vendor.Id,
        vendor.Name,
        vendor.DisplayName,
        vendor.Category,
        vendor.DefaultCategoryId,
        defaultCategoryName,
        vendor.Website,
        vendor.MatchKeywords,
        vendor.Notes,
        vendor.IsActive,
        transactionCount,
        totalSpend);

    public static Contracts.Statements.StatementDto ToDto(this CreditCardStatement statement, string bankName, string? originalFileName) => new(
        statement.Id,
        statement.BankId,
        bankName,
        statement.Kind.ToString(),
        statement.Status.ToString(),
        statement.StatementNumber,
        statement.CardNumber?.Masked,
        statement.AccountNumberMasked,
        statement.CardHolderName,
        statement.CustomerName,
        statement.PeriodStart,
        statement.PeriodEnd,
        statement.StatementDate,
        statement.PaymentDueDate,
        statement.OpeningBalance,
        statement.ClosingBalance,
        statement.MinimumDue,
        statement.TotalDue,
        statement.TotalCredits,
        statement.TotalDebits,
        statement.TransactionCount,
        statement.Currency,
        statement.ParserName,
        statement.StatementFileId,
        originalFileName,
        statement.CreatedOnUtc);

    public static Contracts.Statements.UploadHistoryDto ToDto(this UploadHistory history) => new(
        history.Id,
        history.FileName,
        history.SizeInBytes,
        history.Status.ToString(),
        history.DetectedBank.ToString(),
        history.DetectedKind.ToString(),
        history.ParserName,
        history.RequiresPassword,
        history.TransactionsExtracted,
        history.TransactionsImported,
        history.TransactionsSkipped,
        history.StartedOnUtc,
        history.CompletedOnUtc,
        history.DurationMs,
        history.ErrorCode,
        history.ErrorMessage,
        history.StatementId,
        history.StatementFileId,
        history.CreatedBy);
}
