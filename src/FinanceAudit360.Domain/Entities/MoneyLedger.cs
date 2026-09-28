using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>A single lend/borrow/settlement movement against a person. Part of the Person aggregate.</summary>
public class MoneyLedger : AuditableEntity
{
    private MoneyLedger()
    {
    }

    private MoneyLedger(Guid personId, DateTime entryDate, decimal amount, LedgerEntryType entryType)
    {
        PersonId = personId;
        EntryDate = entryDate;
        Amount = amount;
        EntryType = entryType;
    }

    public Guid PersonId { get; private set; }

    public Person? Person { get; private set; }

    public DateTime EntryDate { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = AppConstants.DefaultCurrency;

    public LedgerEntryType EntryType { get; private set; }

    public string? Description { get; private set; }

    public string? ReferenceNumber { get; private set; }

    public Guid? TransactionId { get; private set; }

    public Transaction? Transaction { get; private set; }

    public Guid? BankAccountId { get; private set; }

    public BankAccount? BankAccount { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    public DateTime? SettledOn { get; private set; }

    public bool IsSettled { get; private set; }

    /// <summary>Signed impact on the outstanding balance: positive means the person owes me more.</summary>
    public decimal SignedAmount => EntryType switch
    {
        LedgerEntryType.Given => Amount,
        LedgerEntryType.SettlementPaid => Amount,
        LedgerEntryType.Taken => -Amount,
        LedgerEntryType.SettlementReceived => -Amount,
        _ => 0m
    };

    internal static MoneyLedger Create(
        Guid personId,
        DateTime entryDate,
        decimal amount,
        LedgerEntryType entryType,
        string? description,
        Guid? transactionId,
        Guid? bankAccountId,
        Guid? creditCardId,
        string? referenceNumber) =>
        new(personId, entryDate, amount, entryType)
        {
            Description = description.NormalizeOrNull(),
            TransactionId = transactionId,
            BankAccountId = bankAccountId,
            CreditCardId = creditCardId,
            ReferenceNumber = referenceNumber.NormalizeOrNull()
        };

    public void Settle(DateTime settledOn)
    {
        IsSettled = true;
        SettledOn = settledOn;
    }

    public void UpdateDescription(string? description) => Description = description.NormalizeOrNull();
}
