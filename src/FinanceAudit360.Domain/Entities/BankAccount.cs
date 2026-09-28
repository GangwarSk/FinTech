using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class BankAccount : AuditableEntity, IAggregateRoot
{
    private readonly List<Transaction> _transactions = [];

    private BankAccount()
    {
    }

    private BankAccount(Guid bankId, string accountNumberMasked, string accountHolderName, AccountType accountType)
    {
        BankId = bankId;
        AccountNumberMasked = accountNumberMasked;
        AccountNumberLast4 = accountNumberMasked.Last4();
        AccountHolderName = accountHolderName;
        AccountType = accountType;
    }

    public Guid BankId { get; private set; }

    public Bank? Bank { get; private set; }

    public string AccountNumberMasked { get; private set; } = string.Empty;

    public string AccountNumberLast4 { get; private set; } = string.Empty;

    public string AccountHolderName { get; private set; } = string.Empty;

    public AccountType AccountType { get; private set; }

    public string? Nickname { get; private set; }

    public string? Ifsc { get; private set; }

    public string? BranchName { get; private set; }

    public string Currency { get; private set; } = AppConstants.DefaultCurrency;

    public decimal CurrentBalance { get; private set; }

    public DateTime? BalanceAsOfUtc { get; private set; }

    public DateTime? OpenedOn { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsPrimary { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public string DisplayName => string.IsNullOrWhiteSpace(Nickname)
        ? $"{Bank?.ShortName ?? "Bank"} ****{AccountNumberLast4}"
        : Nickname;

    public static BankAccount Create(
        Guid bankId,
        string accountNumber,
        string accountHolderName,
        AccountType accountType,
        string? nickname = null,
        string? ifsc = null,
        string? branchName = null,
        string currency = "INR")
    {
        if (bankId == Guid.Empty)
        {
            throw new DomainException("bankaccount.bank_required", "A bank must be supplied.");
        }

        var digits = accountNumber.DigitsOnly();
        if (digits.Length < 4)
        {
            throw new DomainException("bankaccount.number_invalid", "Account number must contain at least 4 digits.");
        }

        return new BankAccount(bankId, accountNumber.MaskCardNumber(), accountHolderName.NormalizeText(), accountType)
        {
            Nickname = nickname.NormalizeOrNull(),
            Ifsc = ifsc.NormalizeOrNull()?.ToUpperInvariant(),
            BranchName = branchName.NormalizeOrNull(),
            Currency = currency.ToUpperInvariantSafe()
        };
    }

    public void Update(
        string accountHolderName,
        AccountType accountType,
        string? nickname,
        string? ifsc,
        string? branchName,
        DateTime? openedOn,
        string? notes)
    {
        AccountHolderName = accountHolderName.NormalizeText();
        AccountType = accountType;
        Nickname = nickname.NormalizeOrNull();
        Ifsc = ifsc.NormalizeOrNull()?.ToUpperInvariant();
        BranchName = branchName.NormalizeOrNull();
        OpenedOn = openedOn;
        Notes = notes.NormalizeOrNull();
    }

    public void SyncBalance(decimal balance, DateTime asOfUtc)
    {
        CurrentBalance = balance;
        BalanceAsOfUtc = asOfUtc;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
