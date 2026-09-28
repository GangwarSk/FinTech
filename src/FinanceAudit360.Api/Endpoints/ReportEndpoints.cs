using FinanceAudit360.Application.Features.Reports;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports")
            .RequireAuthorization(Permissions.ReportsRead);

        group.MapGet("/monthly-activity", async (
                [FromQuery] DateTime from,
                [FromQuery] DateTime to,
                ISender sender,
                CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetMonthlyActivityQuery(from, to), ct)))
            .WithName("GetMonthlyActivity");

        group.MapPost("/spend-by-vendor", async ([FromBody] ReportFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetSpendByVendorQuery(filter), ct)))
            .WithName("GetSpendByVendor");

        group.MapPost("/spend-by-category", async ([FromBody] ReportFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetSpendByCategoryQuery(filter), ct)))
            .WithName("GetSpendByCategory");

        group.MapPost("/card-payments", async ([FromBody] ReportFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetCardPaymentSummaryQuery(filter), ct)))
            .WithName("GetCardPaymentSummary");

        group.MapGet("/person-outstanding", async ([FromQuery] int topN, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetPersonOutstandingQuery(topN <= 0 ? 10 : topN), ct)))
            .WithName("GetPersonOutstanding");

        group.MapPost("/audit-logs", async ([FromBody] AuditLogFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchAuditLogsQuery(filter), ct)))
            .RequireAuthorization(Permissions.UsersManage)
            .WithName("SearchAuditLogs");

        return app;
    }
}
