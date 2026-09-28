using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Events;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>
/// A parsed statement (credit card or bank account) together with every field extracted
/// from the source PDF. Acts as the aggregate root for its transactions.
/// </summary>
public class CreditCardStatement : AuditableEntity, IAggregateRoot
{
    private readonly List<Transaction> _transactions = [];

    private CreditCardStatement()
    {
    }

    private CreditCardStatement(Guid bankId, StatementKind kind, DateRange period)
    {
        BankId = bankId;
        Kind = kind;
        PeriodStart = period.From;
        PeriodEnd = period.To;
    }

    public Guid BankId { get; private set; }

    public Bank? Bank { get; private set; }

    public Guid? CreditCardId { get; private set; }

    public CreditCard? CreditCard { get; private set; }

    public Guid? BankAccountId { get; private set; }

    public BankAccount? BankAccount { get; private set; }

    public Guid? StatementFileId { get; private set; }

    public StatementFile? StatementFile { get; private set; }

    public StatementKind Kind { get; private set; }

    public StatementStatus Status { get; private set; } = StatementStatus.Draft;

    public string? StatementNumber { get; private set; }

    public MaskedCardNumber? CardNumber { get; private set; }

    public string? AccountNumberMasked { get; private set; }

    public string? CardHolderName { get; private set; }

    public string? CustomerName { get; private set; }

    public string? CustomerEmail { get; private set; }

    public string? CustomerPhone { get; private set; }

    public Address BillingAddress { get; private set; } = Address.Empty;

    public DateTime PeriodStart { get; private set; }

    public DateTime PeriodEnd { get; private set; }

    public DateTime? StatementDate { get; private set; }

    public DateTime? PaymentDueDate { get; private set; }

    public decimal OpeningBalance { get; private set; }

    public decimal ClosingBalance { get; private set; }

    public decimal? MinimumDue { get; private set; }

    public decimal? TotalDue { get; private set; }

    public decimal? CreditLimit { get; private set; }

    public decimal? AvailableCreditLimit { get; private set; }

    public decimal TotalCredits { get; private set; }

    public decimal TotalDebits { get; private set; }

    public int TransactionCount { get; private set; }

    public string Currency { get; private set; } = AppConstants.DefaultCurrency;

    public string? ParserName { get; private set; }

    /// <summary>Stable hash of bank + account/card + period, used to reject duplicate uploads.</summary>
    public string DedupeKey { get; private set; } = string.Empty;

    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    public static CreditCardStatement CreateForCreditCard(
        Guid bankId,
        Guid creditCardId,
        MaskedCardNumber cardNumber,
        DateRange period)
    {
        var statement = new CreditCardStatement(bankId, StatementKind.CreditCard, period)
        {
            CreditCardId = creditCardId,
            CardNumber = cardNumber
        };

        statement.DedupeKey = BuildDedupeKey(bankId, cardNumber.Last4, period);
        return statement;
    }

    public static CreditCardStatement CreateForBankAccount(
        Guid bankId,
        Guid bankAccountId,
        string accountNumberMasked,
        DateRange period)
    {
        var statement = new CreditCardStatement(bankId, StatementKind.BankAccount, period)
        {
            BankAccountId = bankAccountId,
            AccountNumberMasked = accountNumberMasked
        };

        statement.DedupeKey = BuildDedupeKey(bankId, accountNumberMasked.Last4(), period);
        return statement;
    }

    public static string BuildDedupeKey(Guid bankId, string last4, DateRange period) =>
        $"{bankId:N}-{last4}-{period.From:yyyyMMdd}-{period.To:yyyyMMdd}";

    public void SetHolderDetails(string? cardHolderName, string? customerName, string? email, string? phone, Address address)
    {
        CardHolderName = cardHolderName.NormalizeOrNull();
        CustomerName = customerName.NormalizeOrNull();
        CustomerEmail = EmailAddress.CreateOrNull(email)?.Value;
        CustomerPhone = PhoneNumber.CreateOrNull(phone)?.Value;
        BillingAddress = address;
    }

    public void SetBalances(
        decimal openingBalance,
        decimal closingBalance,
        decimal? minimumDue,
        decimal? totalDue,
        decimal? creditLimit,
        decimal? availableCreditLimit)
    {
        OpeningBalance = openingBalance;
        ClosingBalance = closingBalance;
        MinimumDue = minimumDue;
        TotalDue = totalDue;
        CreditLimit = creditLimit;
        AvailableCreditLimit = availableCreditLimit;
    }

    public void SetDates(DateTime? statementDate, DateTime? paymentDueDate)
    {
        StatementDate = statementDate;
        PaymentDueDate = paymentDueDate;
    }

    public void SetSource(Guid statementFileId, string parserName, string? statementNumber, string currency)
    {
        StatementFileId = statementFileId;
        ParserName = parserName.NormalizeOrNull();
        StatementNumber = statementNumber.NormalizeOrNull();
        Currency = currency.ToUpperInvariantSafe();
    }

    public void AddTransaction(Transaction transaction)
    {
        if (transaction is null)
        {
            throw new DomainException("statement.transaction_required", "Transaction cannot be null.");
        }

        _transactions.Add(transaction);
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        TotalCredits = _transactions.Where(t => !t.IsDeleted).Sum(t => t.CreditAmount);
        TotalDebits = _transactions.Where(t => !t.IsDeleted).Sum(t => t.DebitAmount);
        TransactionCount = _transactions.Count(t => !t.IsDeleted);
    }

    public void MarkImported()
    {
        Status = StatementStatus.Imported;
        RecalculateTotals();
        Raise(new StatementImportedEvent(Id, BankId, CreditCardId, BankAccountId, TransactionCount, TotalCredits, TotalDebits));
    }

    public void MarkReconciled() => Status = StatementStatus.Reconciled;

    public void Archive() => Status = StatementStatus.Archived;
}
