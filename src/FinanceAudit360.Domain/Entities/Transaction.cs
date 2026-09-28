using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Events;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>
/// The central fact table of the system. Every credit/debit line from a statement or a manual
/// entry lands here, linked to whichever of card/account/vendor/person applies.
/// </summary>
public class Transaction : AuditableEntity, IAggregateRoot
{
    private Transaction()
    {
    }

    private Transaction(
        DateTime transactionDate,
        decimal amount,
        TransactionDirection direction,
        TransactionType type,
        string description)
    {
        TransactionDate = transactionDate;
        PostingDate = transactionDate;
        Amount = Math.Abs(amount);
        Direction = direction;
        TransactionType = type;
        Description = description;
        ApplyDirection();
    }

    public DateTime TransactionDate { get; private set; }

    public DateTime PostingDate { get; private set; }

    /// <summary>Always a positive magnitude. Use <see cref="Direction"/> for the sign.</summary>
    public decimal Amount { get; private set; }

    public decimal CreditAmount { get; private set; }

    public decimal DebitAmount { get; private set; }

    /// <summary>Credit positive / debit negative. Persisted so SUM() answers "net amount" directly.</summary>
    public decimal SignedAmount { get; private set; }

    public string Currency { get; private set; } = AppConstants.DefaultCurrency;

    public TransactionDirection Direction { get; private set; }

    public TransactionType TransactionType { get; private set; }

    public TransactionSource Source { get; private set; } = TransactionSource.Manual;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Upper-cased, whitespace-collapsed description used for fast keyword search.</summary>
    public string NormalizedDescription { get; private set; } = string.Empty;

    public string? ReferenceNumber { get; private set; }

    public string? MerchantRawText { get; private set; }

    public string? Location { get; private set; }

    public string? Notes { get; private set; }

    public Guid? VendorId { get; private set; }

    public Vendor? Vendor { get; private set; }

    public Guid? CategoryId { get; private set; }

    public TransactionCategory? Category { get; private set; }

    public Guid? BankAccountId { get; private set; }

    public BankAccount? BankAccount { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    public Guid? BankId { get; private set; }

    public Bank? Bank { get; private set; }

    public Guid? PersonId { get; private set; }

    public Person? Person { get; private set; }

    public Guid? StatementId { get; private set; }

    public CreditCardStatement? Statement { get; private set; }

    public bool IsReconciled { get; private set; }

    public bool IsRecurring { get; private set; }

    public bool IsDisputed { get; private set; }

    /// <summary>Hash of date + amount + reference + instrument; blocks re-importing the same line.</summary>
    public string DedupeHash { get; private set; } = string.Empty;

    public static Transaction Create(
        DateTime transactionDate,
        decimal amount,
        TransactionDirection direction,
        TransactionType type,
        string description,
        string currency = AppConstants.DefaultCurrency)
    {
        if (amount == 0m)
        {
            throw new DomainException("transaction.amount_invalid", "Transaction amount must not be zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("transaction.description_required", "Description is required.");
        }

        var transaction = new Transaction(transactionDate, amount, direction, type, description.NormalizeText())
        {
            Currency = currency.ToUpperInvariantSafe()
        };

        transaction.NormalizedDescription = transaction.Description.ToUpperInvariant();
        transaction.RecomputeDedupeHash();
        transaction.Raise(new TransactionCreatedEvent(transaction.Id, transaction.Amount, transaction.Direction, transaction.TransactionDate));
        return transaction;
    }

    public void SetPostingDate(DateTime postingDate) => PostingDate = postingDate;

    public void SetSource(TransactionSource source) => Source = source;

    public void SetInstrument(Guid? bankId, Guid? bankAccountId, Guid? creditCardId)
    {
        if (bankAccountId.HasValue && creditCardId.HasValue)
        {
            throw new BusinessRuleViolationException(
                "transaction.instrument_ambiguous",
                "A transaction cannot belong to both a bank account and a credit card.");
        }

        BankId = bankId;
        BankAccountId = bankAccountId;
        CreditCardId = creditCardId;
        RecomputeDedupeHash();
    }

    public void SetStatement(Guid? statementId)
    {
        StatementId = statementId;
        RecomputeDedupeHash();
    }

    public void AssignVendor(Guid? vendorId) => VendorId = vendorId;

    public void AssignCategory(Guid? categoryId) => CategoryId = categoryId;

    public void AssignPerson(Guid? personId) => PersonId = personId;

    public void SetReference(string? referenceNumber, string? merchantRawText, string? location)
    {
        ReferenceNumber = referenceNumber.NormalizeOrNull();
        MerchantRawText = merchantRawText.NormalizeOrNull();
        Location = location.NormalizeOrNull();
        RecomputeDedupeHash();
    }

    public void UpdateDetails(
        DateTime transactionDate,
        DateTime postingDate,
        decimal amount,
        TransactionDirection direction,
        TransactionType type,
        string description,
        string? notes)
    {
        if (amount == 0m)
        {
            throw new DomainException("transaction.amount_invalid", "Transaction amount must not be zero.");
        }

        TransactionDate = transactionDate;
        PostingDate = postingDate;
        Amount = Math.Abs(amount);
        Direction = direction;
        TransactionType = type;
        Description = description.NormalizeText();
        NormalizedDescription = Description.ToUpperInvariant();
        Notes = notes.NormalizeOrNull();
        ApplyDirection();
        RecomputeDedupeHash();
    }

    public void MarkReconciled(bool reconciled) => IsReconciled = reconciled;

    public void MarkRecurring(bool recurring) => IsRecurring = recurring;

    public void MarkDisputed(bool disputed) => IsDisputed = disputed;

    private void ApplyDirection()
    {
        if (Direction == TransactionDirection.Credit)
        {
            CreditAmount = Amount;
            DebitAmount = 0m;
            SignedAmount = Amount;
        }
        else
        {
            CreditAmount = 0m;
            DebitAmount = Amount;
            SignedAmount = -Amount;
        }
    }

    private void RecomputeDedupeHash()
    {
        var instrument = CreditCardId?.ToString("N") ?? BankAccountId?.ToString("N") ?? "none";
        var raw = string.Join('|',
            TransactionDate.ToString("yyyyMMdd"),
            Amount.ToString("F2"),
            Direction.ToString(),
            instrument,
            ReferenceNumber ?? string.Empty,
            NormalizedDescription.Truncate(120));

        DedupeHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
    }
}
