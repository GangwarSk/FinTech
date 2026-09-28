using System.Text.RegularExpressions;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Domain.Enums;

namespace FinanceAudit360.Api.Endpoints;

/// <summary>Projects the domain enums into UI-friendly option lists with humanised labels.</summary>
public static partial class EnumCatalog
{
    [GeneratedRegex(@"(?<!^)(?=[A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalBoundary();

    public static IReadOnlyDictionary<string, IReadOnlyList<EnumOptionDto>> Build() =>
        new Dictionary<string, IReadOnlyList<EnumOptionDto>>
        {
            ["transactionType"] = Options<TransactionType>(),
            ["transactionDirection"] = Options<TransactionDirection>(),
            ["transactionSource"] = Options<TransactionSource>(),
            ["bankCode"] = Options<BankCode>(),
            ["accountType"] = Options<AccountType>(),
            ["cardNetwork"] = Options<CardNetwork>(),
            ["statementKind"] = Options<StatementKind>(),
            ["statementStatus"] = Options<StatementStatus>(),
            ["uploadStatus"] = Options<UploadStatus>(),
            ["ledgerEntryType"] = Options<LedgerEntryType>(),
            ["settingDataType"] = Options<SettingDataType>(),
            ["auditAction"] = Options<AuditAction>()
        };

    private static IReadOnlyList<EnumOptionDto> Options<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => new EnumOptionDto(
                Convert.ToInt32(value),
                value.ToString()!,
                PascalBoundary().Replace(value.ToString()!, " ")))
            .ToList();
}
