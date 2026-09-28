using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Shared.Models;
using FluentValidation;
using MediatR;

namespace FinanceAudit360.Application.Features.Reports;

public sealed record GetMonthlyActivityQuery(DateTime From, DateTime To) : IRequest<IReadOnlyList<MonthlyActivityDto>>;

public sealed record GetSpendByVendorQuery(ReportFilterRequest Filter) : IRequest<IReadOnlyList<SpendByEntityDto>>;

public sealed record GetSpendByCategoryQuery(ReportFilterRequest Filter) : IRequest<IReadOnlyList<SpendByEntityDto>>;

public sealed record GetCardPaymentSummaryQuery(ReportFilterRequest Filter) : IRequest<IReadOnlyList<CardPaymentSummaryDto>>;

public sealed record GetPersonOutstandingQuery(int TopN) : IRequest<IReadOnlyList<PersonOutstandingDto>>;

public sealed record SearchAuditLogsQuery(AuditLogFilterRequest Filter) : IRequest<PagedResult<AuditLogDto>>;

public sealed class GetMonthlyActivityQueryHandler(ITransactionRepository repository)
    : IRequestHandler<GetMonthlyActivityQuery, IReadOnlyList<MonthlyActivityDto>>
{
    public Task<IReadOnlyList<MonthlyActivityDto>> Handle(GetMonthlyActivityQuery request, CancellationToken cancellationToken) =>
        repository.GetMonthlyActivityAsync(request.From, request.To, cancellationToken);
}

public sealed class GetSpendByVendorQueryHandler(ITransactionRepository repository)
    : IRequestHandler<GetSpendByVendorQuery, IReadOnlyList<SpendByEntityDto>>
{
    public Task<IReadOnlyList<SpendByEntityDto>> Handle(GetSpendByVendorQuery request, CancellationToken cancellationToken) =>
        repository.GetSpendByVendorAsync(request.Filter, cancellationToken);
}

public sealed class GetSpendByCategoryQueryHandler(ITransactionRepository repository)
    : IRequestHandler<GetSpendByCategoryQuery, IReadOnlyList<SpendByEntityDto>>
{
    public Task<IReadOnlyList<SpendByEntityDto>> Handle(GetSpendByCategoryQuery request, CancellationToken cancellationToken) =>
        repository.GetSpendByCategoryAsync(request.Filter, cancellationToken);
}

public sealed class GetCardPaymentSummaryQueryHandler(ITransactionRepository repository)
    : IRequestHandler<GetCardPaymentSummaryQuery, IReadOnlyList<CardPaymentSummaryDto>>
{
    public Task<IReadOnlyList<CardPaymentSummaryDto>> Handle(GetCardPaymentSummaryQuery request, CancellationToken cancellationToken) =>
        repository.GetCardPaymentSummaryAsync(request.Filter, cancellationToken);
}

public sealed class GetPersonOutstandingQueryHandler(IPersonRepository repository)
    : IRequestHandler<GetPersonOutstandingQuery, IReadOnlyList<PersonOutstandingDto>>
{
    public Task<IReadOnlyList<PersonOutstandingDto>> Handle(GetPersonOutstandingQuery request, CancellationToken cancellationToken) =>
        repository.GetOutstandingAsync(request.TopN, cancellationToken);
}

public sealed class SearchAuditLogsQueryHandler(IAuditLogRepository repository)
    : IRequestHandler<SearchAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public Task<PagedResult<AuditLogDto>> Handle(SearchAuditLogsQuery request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request.Filter, cancellationToken);
}

public sealed class GetMonthlyActivityQueryValidator : AbstractValidator<GetMonthlyActivityQuery>
{
    public GetMonthlyActivityQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty().GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x)
            .Must(x => (x.To - x.From).TotalDays <= 3660)
            .WithName("Range")
            .WithMessage("The reporting range cannot exceed 10 years.");
    }
}

public sealed class ReportFilterRequestValidator : AbstractValidator<ReportFilterRequest>
{
    public ReportFilterRequestValidator()
    {
        RuleFor(x => x.TopN).InclusiveBetween(1, 100);
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithName("DateRange")
            .WithMessage("The start date must not be after the end date.");
    }
}

public sealed class GetSpendByVendorQueryValidator : AbstractValidator<GetSpendByVendorQuery>
{
    public GetSpendByVendorQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new ReportFilterRequestValidator());
}

public sealed class GetSpendByCategoryQueryValidator : AbstractValidator<GetSpendByCategoryQuery>
{
    public GetSpendByCategoryQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new ReportFilterRequestValidator());
}

public sealed class GetCardPaymentSummaryQueryValidator : AbstractValidator<GetCardPaymentSummaryQuery>
{
    public GetCardPaymentSummaryQueryValidator() => RuleFor(x => x.Filter).NotNull().SetValidator(new ReportFilterRequestValidator());
}

public sealed class GetPersonOutstandingQueryValidator : AbstractValidator<GetPersonOutstandingQuery>
{
    public GetPersonOutstandingQueryValidator() => RuleFor(x => x.TopN).InclusiveBetween(1, 200);
}

public sealed class SearchAuditLogsQueryValidator : AbstractValidator<SearchAuditLogsQuery>
{
    public SearchAuditLogsQueryValidator()
    {
        RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Filter.Keyword).MaximumLength(200);
        RuleFor(x => x.Filter.EntityName).MaximumLength(100);
    }
}
