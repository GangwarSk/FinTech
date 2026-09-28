using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class CreditCard : AuditableEntity, IAggregateRoot
{
    private readonly List<CreditCardStatement> _statements = [];
    private readonly List<Transaction> _transactions = [];

    private CreditCard()
    {
    }

    private CreditCard(Guid bankId, MaskedCardNumber cardNumber, string cardHolderName)
    {
        BankId = bankId;
        CardNumber = cardNumber;
        CardHolderName = cardHolderName;
    }

    public Guid BankId { get; private set; }

    public Bank? Bank { get; private set; }

    public MaskedCardNumber CardNumber { get; private set; } = null!;

    public string CardHolderName { get; private set; } = string.Empty;

    public string? Nickname { get; private set; }

    public string? ProductName { get; private set; }

    public CardNetwork Network { get; private set; } = CardNetwork.Unknown;

    public decimal? CreditLimit { get; private set; }

    public decimal? CashLimit { get; private set; }

    public decimal CurrentOutstanding { get; private set; }

    public DateTime? OutstandingAsOfUtc { get; private set; }

    public int? StatementDayOfMonth { get; private set; }

    public int? PaymentDueDayOfMonth { get; private set; }

    public DateTime? ExpiryDate { get; private set; }

    public string Currency { get; private set; } = AppConstants.DefaultCurrency;

    public bool IsActive { get; private set; } = true;

    public string? Notes { get; private set; }

    public IReadOnlyCollection<CreditCardStatement> Statements => _statements.AsReadOnly();

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public string DisplayName => string.IsNullOrWhiteSpace(Nickname)
        ? $"{Bank?.ShortName ?? "Card"} ****{CardNumber.Last4}"
        : Nickname;

    public decimal? AvailableLimit => CreditLimit.HasValue ? CreditLimit.Value - CurrentOutstanding : null;

    public static CreditCard Create(
        Guid bankId,
        string cardNumber,
        string cardHolderName,
        CardNetwork network = CardNetwork.Unknown,
        string? nickname = null,
        string? productName = null,
        decimal? creditLimit = null)
    {
        if (bankId == Guid.Empty)
        {
            throw new DomainException("creditcard.bank_required", "A bank must be supplied.");
        }

        if (creditLimit is < 0)
        {
            throw new DomainException("creditcard.limit_invalid", "Credit limit cannot be negative.");
        }

        return new CreditCard(bankId, MaskedCardNumber.Create(cardNumber), cardHolderName.NormalizeText())
        {
            Network = network,
            Nickname = nickname.NormalizeOrNull(),
            ProductName = productName.NormalizeOrNull(),
            CreditLimit = creditLimit
        };
    }

    public void Update(
        string cardHolderName,
        CardNetwork network,
        string? nickname,
        string? productName,
        decimal? creditLimit,
        decimal? cashLimit,
        int? statementDayOfMonth,
        int? paymentDueDayOfMonth,
        DateTime? expiryDate,
        string? notes)
    {
        if (statementDayOfMonth is < 1 or > 31)
        {
            throw new DomainException("creditcard.statement_day_invalid", "Statement day must be between 1 and 31.");
        }

        if (paymentDueDayOfMonth is < 1 or > 31)
        {
            throw new DomainException("creditcard.due_day_invalid", "Payment due day must be between 1 and 31.");
        }

        CardHolderName = cardHolderName.NormalizeText();
        Network = network;
        Nickname = nickname.NormalizeOrNull();
        ProductName = productName.NormalizeOrNull();
        CreditLimit = creditLimit;
        CashLimit = cashLimit;
        StatementDayOfMonth = statementDayOfMonth;
        PaymentDueDayOfMonth = paymentDueDayOfMonth;
        ExpiryDate = expiryDate;
        Notes = notes.NormalizeOrNull();
    }

    public void SyncOutstanding(decimal outstanding, DateTime asOfUtc)
    {
        CurrentOutstanding = outstanding;
        OutstandingAsOfUtc = asOfUtc;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
