using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Contracts.Masters;

public sealed record BankDto(
    Guid Id,
    string Name,
    string ShortName,
    int Code,
    string CodeName,
    string? Ifsc,
    string? Website,
    string? LogoUrl,
    string? StatementKeywords,
    bool IsActive,
    int AccountCount,
    int CardCount);

public sealed record SaveBankRequest(
    string Name,
    string ShortName,
    int Code,
    string? Ifsc,
    string? Website,
    string? LogoUrl,
    string? StatementKeywords,
    bool IsActive);

public sealed record BankAccountDto(
    Guid Id,
    Guid BankId,
    string BankName,
    string AccountNumberMasked,
    string AccountNumberLast4,
    string AccountHolderName,
    int AccountType,
    string AccountTypeName,
    string? Nickname,
    string DisplayName,
    string? Ifsc,
    string? BranchName,
    string Currency,
    decimal CurrentBalance,
    DateTime? BalanceAsOfUtc,
    DateTime? OpenedOn,
    bool IsActive,
    bool IsPrimary,
    string? Notes,
    int TransactionCount);

public sealed record SaveBankAccountRequest(
    Guid BankId,
    string AccountNumber,
    string AccountHolderName,
    int AccountType,
    string? Nickname,
    string? Ifsc,
    string? BranchName,
    string? Currency,
    DateTime? OpenedOn,
    string? Notes,
    bool IsActive,
    bool IsPrimary);

public sealed record CreditCardDto(
    Guid Id,
    Guid BankId,
    string BankName,
    string CardNumberMasked,
    string CardNumberLast4,
    string CardHolderName,
    string? Nickname,
    string DisplayName,
    string? ProductName,
    int Network,
    string NetworkName,
    decimal? CreditLimit,
    decimal? CashLimit,
    decimal CurrentOutstanding,
    decimal? AvailableLimit,
    int? StatementDayOfMonth,
    int? PaymentDueDayOfMonth,
    DateTime? ExpiryDate,
    string Currency,
    bool IsActive,
    string? Notes,
    int TransactionCount,
    int StatementCount);

public sealed record SaveCreditCardRequest(
    Guid BankId,
    string CardNumber,
    string CardHolderName,
    int Network,
    string? Nickname,
    string? ProductName,
    decimal? CreditLimit,
    decimal? CashLimit,
    int? StatementDayOfMonth,
    int? PaymentDueDayOfMonth,
    DateTime? ExpiryDate,
    string? Notes,
    bool IsActive);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    string? ColorHex,
    string? Icon,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    bool IsSystemCategory,
    bool IsActive,
    int DisplayOrder,
    string? MatchKeywords,
    int TransactionCount);

public sealed record SaveCategoryRequest(
    string Name,
    string? Code,
    string? Description,
    string? ColorHex,
    string? Icon,
    Guid? ParentCategoryId,
    string? MatchKeywords,
    int DisplayOrder,
    bool IsActive);

public sealed record VendorDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? Category,
    Guid? DefaultCategoryId,
    string? DefaultCategoryName,
    string? Website,
    string? MatchKeywords,
    string? Notes,
    bool IsActive,
    int TransactionCount,
    decimal TotalSpend);

public sealed record SaveVendorRequest(
    string Name,
    string? DisplayName,
    string? Category,
    Guid? DefaultCategoryId,
    string? Website,
    string? MatchKeywords,
    string? Notes,
    bool IsActive);

public sealed record UserDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginOnUtc,
    DateTime CreatedOnUtc,
    IReadOnlyList<LookupRoleDto> Roles);

public sealed record LookupRoleDto(Guid Id, string Name);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string FullName,
    string Password,
    string? PhoneNumber,
    IReadOnlyList<Guid> RoleIds);

public sealed record UpdateUserRequest(
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyList<Guid> RoleIds);

public sealed record ResetPasswordRequest(string NewPassword, bool MustChangePassword);

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    IReadOnlyList<string> Permissions);

public sealed record SaveRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record SettingDto(
    Guid Id,
    string Key,
    string? Value,
    int DataType,
    string DataTypeName,
    string Category,
    string? Description,
    string? DefaultValue,
    bool IsSystem,
    bool IsSecret);

public sealed record SaveSettingRequest(string Key, string? Value, int DataType, string Category, string? Description);

public sealed record UpdateSettingValueRequest(string? Value);

public sealed class MasterFilterRequest : PaginationRequest
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public Guid? BankId { get; set; }
}
