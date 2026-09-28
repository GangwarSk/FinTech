using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>
/// A friend/relative/customer I lend to or borrow from. Owns the money ledger so that the
/// running balance can only ever be changed through the aggregate.
/// </summary>
public class Person : AuditableEntity, IAggregateRoot
{
    private readonly List<MoneyLedger> _ledgerEntries = [];

    private Person()
    {
    }

    private Person(string name, string normalizedName)
    {
        Name = name;
        NormalizedName = normalizedName;
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Mobile { get; private set; }

    public string? Email { get; private set; }

    public Address Address { get; private set; } = Address.Empty;

    public string? Relationship { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Total money I have given to this person.</summary>
    public decimal TotalGiven { get; private set; }

    /// <summary>Total money I have taken from this person.</summary>
    public decimal TotalTaken { get; private set; }

    /// <summary>Positive means the person owes me; negative means I owe the person.</summary>
    public decimal OutstandingBalance { get; private set; }

    public DateTime? LastActivityOn { get; private set; }

    /// <summary>Pipe-separated tokens matched against statement narrations to auto-link this person.</summary>
    public string? MatchKeywords { get; private set; }

    public IReadOnlyCollection<MoneyLedger> LedgerEntries => _ledgerEntries.AsReadOnly();

    public static Person Create(string name, string? mobile = null, string? email = null, string? relationship = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("person.name_required", "Person name is required.");
        }

        var normalized = name.NormalizeText();
        return new Person(normalized, normalized.ToUpperInvariant())
        {
            Mobile = PhoneNumber.CreateOrNull(mobile)?.Value,
            Email = EmailAddress.CreateOrNull(email)?.Value,
            Relationship = relationship.NormalizeOrNull()
        };
    }

    public void Update(
        string name,
        string? mobile,
        string? email,
        Address address,
        string? relationship,
        string? notes,
        string? matchKeywords)
    {
        Name = name.NormalizeText();
        NormalizedName = Name.ToUpperInvariant();
        Mobile = PhoneNumber.CreateOrNull(mobile)?.Value;
        Email = EmailAddress.CreateOrNull(email)?.Value;
        Address = address;
        Relationship = relationship.NormalizeOrNull();
        Notes = notes.NormalizeOrNull();
        MatchKeywords = matchKeywords.NormalizeOrNull();
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public MoneyLedger RecordEntry(
        DateTime entryDate,
        decimal amount,
        LedgerEntryType entryType,
        string? description,
        Guid? transactionId = null,
        Guid? bankAccountId = null,
        Guid? creditCardId = null,
        string? referenceNumber = null)
    {
        if (amount <= 0m)
        {
            throw new DomainException("ledger.amount_invalid", "Ledger amount must be greater than zero.");
        }

        var entry = MoneyLedger.Create(Id, entryDate, amount, entryType, description, transactionId, bankAccountId, creditCardId, referenceNumber);
        _ledgerEntries.Add(entry);
        Recalculate();
        return entry;
    }

    public void RemoveEntry(Guid ledgerEntryId, string? deletedBy, DateTime utcNow)
    {
        var entry = _ledgerEntries.FirstOrDefault(e => e.Id == ledgerEntryId)
                    ?? throw new EntityNotFoundException(nameof(MoneyLedger), ledgerEntryId);

        entry.MarkDeleted(deletedBy, utcNow);
        Recalculate();
    }

    /// <summary>
    /// Derives the aggregate totals from the surviving ledger entries. Given and settlements-paid
    /// increase what the person owes me; taken and settlements-received reduce it.
    /// </summary>
    public void Recalculate()
    {
        var live = _ledgerEntries.Where(e => !e.IsDeleted).ToList();

        TotalGiven = live.Where(e => e.EntryType is LedgerEntryType.Given).Sum(e => e.Amount);
        TotalTaken = live.Where(e => e.EntryType is LedgerEntryType.Taken).Sum(e => e.Amount);

        var settlementsReceived = live.Where(e => e.EntryType is LedgerEntryType.SettlementReceived).Sum(e => e.Amount);
        var settlementsPaid = live.Where(e => e.EntryType is LedgerEntryType.SettlementPaid).Sum(e => e.Amount);

        OutstandingBalance = TotalGiven - settlementsReceived - (TotalTaken - settlementsPaid);
        LastActivityOn = live.Count == 0 ? null : live.Max(e => e.EntryDate);
    }

    public IEnumerable<string> KeywordTokens =>
        (MatchKeywords ?? string.Empty)
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
