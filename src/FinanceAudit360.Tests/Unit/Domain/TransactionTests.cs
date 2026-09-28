using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;

namespace FinanceAudit360.Tests.Unit.Domain;

public class TransactionTests
{
    [Fact]
    public void Create_Credit_PopulatesCreditColumnsAndPositiveSignedAmount()
    {
        var transaction = Transaction.Create(
            new DateTime(2024, 5, 2),
            1500.50m,
            TransactionDirection.Credit,
            TransactionType.Payment,
            "Payment received - thank you");

        Assert.Equal(1500.50m, transaction.Amount);
        Assert.Equal(1500.50m, transaction.CreditAmount);
        Assert.Equal(0m, transaction.DebitAmount);
        Assert.Equal(1500.50m, transaction.SignedAmount);
    }

    [Fact]
    public void Create_Debit_PopulatesDebitColumnsAndNegativeSignedAmount()
    {
        var transaction = Transaction.Create(
            new DateTime(2024, 5, 2),
            999.99m,
            TransactionDirection.Debit,
            TransactionType.Debit,
            "AMAZON RETAIL");

        Assert.Equal(0m, transaction.CreditAmount);
        Assert.Equal(999.99m, transaction.DebitAmount);
        Assert.Equal(-999.99m, transaction.SignedAmount);
    }

    [Fact]
    public void Create_NegativeAmount_StoresAbsoluteMagnitude()
    {
        var transaction = Transaction.Create(
            new DateTime(2024, 5, 2),
            -250m,
            TransactionDirection.Debit,
            TransactionType.Charges,
            "Late fee");

        Assert.Equal(250m, transaction.Amount);
        Assert.Equal(250m, transaction.DebitAmount);
    }

    [Fact]
    public void Create_ZeroAmount_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Transaction.Create(
            DateTime.UtcNow,
            0m,
            TransactionDirection.Debit,
            TransactionType.Debit,
            "Anything"));

        Assert.Equal("transaction.amount_invalid", exception.Code);
    }

    [Fact]
    public void Create_EmptyDescription_Throws() =>
        Assert.Throws<DomainException>(() => Transaction.Create(
            DateTime.UtcNow,
            10m,
            TransactionDirection.Debit,
            TransactionType.Debit,
            "   "));

    [Fact]
    public void SetInstrument_WithBothAccountAndCard_Throws()
    {
        var transaction = NewTransaction();

        Assert.Throws<BusinessRuleViolationException>(() =>
            transaction.SetInstrument(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()));
    }

    [Fact]
    public void DedupeHash_IsStableForIdenticalLines()
    {
        var cardId = Guid.CreateVersion7();

        var first = NewTransaction();
        first.SetInstrument(null, null, cardId);
        first.SetReference("REF12345678", null, null);

        var second = NewTransaction();
        second.SetInstrument(null, null, cardId);
        second.SetReference("REF12345678", null, null);

        Assert.Equal(first.DedupeHash, second.DedupeHash);
    }

    [Fact]
    public void DedupeHash_DiffersWhenAmountDiffers()
    {
        var first = NewTransaction();
        var second = Transaction.Create(
            new DateTime(2024, 5, 2),
            101m,
            TransactionDirection.Debit,
            TransactionType.Debit,
            "SWIGGY BANGALORE");

        Assert.NotEqual(first.DedupeHash, second.DedupeHash);
    }

    [Fact]
    public void UpdateDetails_FlippingDirection_RecalculatesColumns()
    {
        var transaction = NewTransaction();

        transaction.UpdateDetails(
            new DateTime(2024, 6, 1),
            new DateTime(2024, 6, 2),
            75m,
            TransactionDirection.Credit,
            TransactionType.Refund,
            "Refund for order",
            "Refunded by merchant");

        Assert.Equal(75m, transaction.CreditAmount);
        Assert.Equal(0m, transaction.DebitAmount);
        Assert.Equal(75m, transaction.SignedAmount);
        Assert.Equal("REFUND FOR ORDER", transaction.NormalizedDescription);
    }

    [Fact]
    public void Create_RaisesTransactionCreatedEvent()
    {
        var transaction = NewTransaction();

        Assert.Single(transaction.DomainEvents);
        Assert.Contains(transaction.DomainEvents, e => e is FinanceAudit360.Domain.Events.TransactionCreatedEvent);
    }

    private static Transaction NewTransaction() => Transaction.Create(
        new DateTime(2024, 5, 2),
        100m,
        TransactionDirection.Debit,
        TransactionType.Debit,
        "SWIGGY BANGALORE");
}
