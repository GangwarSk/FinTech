using FinanceAudit360.Domain.Enums;
using FluentValidation;

namespace FinanceAudit360.Application.Features.Transactions;

public sealed class SearchTransactionsQueryValidator : AbstractValidator<SearchTransactionsQuery>
{
    private static readonly string[] AllowedGroupings =
        ["vendor", "person", "card", "account", "bank", "category", "month", "type"];

    public SearchTransactionsQueryValidator()
    {
        RuleFor(x => x.Filter).NotNull();
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Filter.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Filter.Keyword).MaximumLength(200);

        RuleFor(x => x.Filter)
            .Must(f => f.DateFrom is null || f.DateTo is null || f.DateFrom <= f.DateTo)
            .WithName("DateRange")
            .WithMessage("The start date must not be after the end date.");

        RuleFor(x => x.Filter)
            .Must(f => f.AmountFrom is null || f.AmountTo is null || f.AmountFrom <= f.AmountTo)
            .WithName("AmountRange")
            .WithMessage("The minimum amount must not exceed the maximum amount.");

        RuleFor(x => x.Filter)
            .Must(f => !(f.CreditOnly == true && f.DebitOnly == true))
            .WithName("Direction")
            .WithMessage("Credit only and debit only cannot both be selected.");

        RuleFor(x => x.Filter.GroupBy)
            .Must(g => string.IsNullOrWhiteSpace(g) || AllowedGroupings.Contains(g.ToLowerInvariant()))
            .WithMessage($"Grouping must be one of: {string.Join(", ", AllowedGroupings)}.");
    }
}

public sealed class ExportTransactionsQueryValidator : AbstractValidator<ExportTransactionsQuery>
{
    public ExportTransactionsQueryValidator() => RuleFor(x => x.Filter).NotNull();
}

public sealed class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Request.TransactionDate)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("The transaction date cannot be in the future.");

        RuleFor(x => x.Request.Amount)
            .NotEqual(0m).WithMessage("Amount must not be zero.")
            .LessThanOrEqualTo(100_000_000m);

        RuleFor(x => x.Request.Direction)
            .Must(BeValidDirection).WithMessage("Direction must be Credit (1) or Debit (2).");

        RuleFor(x => x.Request.TransactionType)
            .Must(BeValidType).WithMessage("Unknown transaction type.");

        RuleFor(x => x.Request.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Request.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Request.Notes).MaximumLength(2000);
        RuleFor(x => x.Request.Currency).Length(3).When(x => !string.IsNullOrWhiteSpace(x.Request.Currency));

        RuleFor(x => x.Request)
            .Must(r => !(r.BankAccountId.HasValue && r.CreditCardId.HasValue))
            .WithName("Instrument")
            .WithMessage("A transaction cannot belong to both a bank account and a credit card.");
    }

    internal static bool BeValidDirection(int value) => Enum.IsDefined(typeof(TransactionDirection), value);

    internal static bool BeValidType(int value) => Enum.IsDefined(typeof(TransactionType), value);
}

public sealed class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.TransactionDate).NotEmpty();
        RuleFor(x => x.Request.Amount).NotEqual(0m).LessThanOrEqualTo(100_000_000m);
        RuleFor(x => x.Request.Direction).Must(CreateTransactionCommandValidator.BeValidDirection);
        RuleFor(x => x.Request.TransactionType).Must(CreateTransactionCommandValidator.BeValidType);
        RuleFor(x => x.Request.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Request.ReferenceNumber).MaximumLength(100);
        RuleFor(x => x.Request.Notes).MaximumLength(2000);

        RuleFor(x => x.Request)
            .Must(r => !(r.BankAccountId.HasValue && r.CreditCardId.HasValue))
            .WithName("Instrument")
            .WithMessage("A transaction cannot belong to both a bank account and a credit card.");
    }
}

public sealed class DeleteTransactionCommandValidator : AbstractValidator<DeleteTransactionCommand>
{
    public DeleteTransactionCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class BulkAssignTransactionsCommandValidator : AbstractValidator<BulkAssignTransactionsCommand>
{
    public BulkAssignTransactionsCommandValidator()
    {
        RuleFor(x => x.Request.TransactionIds)
            .NotEmpty()
            .Must(ids => ids.Count <= 1000).WithMessage("At most 1000 transactions can be updated at once.");

        RuleFor(x => x.Request)
            .Must(r => r.VendorId.HasValue || r.CategoryId.HasValue || r.PersonId.HasValue)
            .WithName("Assignment")
            .WithMessage("Specify at least one of vendor, category or person.");
    }
}
