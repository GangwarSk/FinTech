using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class Bank : AuditableEntity, IAggregateRoot
{
    private readonly List<BankAccount> _accounts = [];
    private readonly List<CreditCard> _creditCards = [];

    private Bank()
    {
    }

    private Bank(string name, string shortName, BankCode code)
    {
        Name = name;
        ShortName = shortName;
        Code = code;
    }

    public string Name { get; private set; } = string.Empty;

    public string ShortName { get; private set; } = string.Empty;

    public BankCode Code { get; private set; }

    public string? Ifsc { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? Website { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Pipe-separated tokens used by the parser factory to recognise this bank inside PDF text.</summary>
    public string? StatementKeywords { get; private set; }

    public IReadOnlyCollection<BankAccount> Accounts => _accounts.AsReadOnly();

    public IReadOnlyCollection<CreditCard> CreditCards => _creditCards.AsReadOnly();

    public static Bank Create(string name, string shortName, BankCode code, string? statementKeywords = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("bank.name_required", "Bank name is required.");
        }

        return new Bank(name.NormalizeText(), shortName.NormalizeText(), code)
        {
            StatementKeywords = statementKeywords.NormalizeOrNull()
        };
    }

    public void Update(string name, string shortName, BankCode code, string? ifsc, string? website, string? logoUrl, string? statementKeywords)
    {
        Name = name.NormalizeText();
        ShortName = shortName.NormalizeText();
        Code = code;
        Ifsc = ifsc.NormalizeOrNull()?.ToUpperInvariant();
        Website = website.NormalizeOrNull();
        LogoUrl = logoUrl.NormalizeOrNull();
        StatementKeywords = statementKeywords.NormalizeOrNull();
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public IEnumerable<string> KeywordTokens =>
        (StatementKeywords ?? string.Empty)
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
