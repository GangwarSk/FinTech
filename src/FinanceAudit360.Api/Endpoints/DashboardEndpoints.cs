using FinanceAudit360.Application.Features.Dashboard;
using FinanceAudit360.Application.Features.Transactions;
using FinanceAudit360.Contracts.Dashboard;
using FinanceAudit360.Contracts.Transactions;
using FinanceAudit360.Shared.Constants;
using FinanceAudit360.Shared.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(Permissions.ReportsRead);

        group.MapGet("/summary", async (
                [FromQuery] DashboardPeriod period,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetDashboardSummaryQuery(period, from, to), cancellationToken);
                return ApiResults.Ok(result);
            })
            .WithName("GetDashboardSummary")
            .WithSummary("KPI cards for the selected period plus the previous-period comparison.");

        return app;
    }
}

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").WithTags("Transactions");

        group.MapPost("/search", async (
                [FromBody] TransactionFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SearchTransactionsQuery(filter), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.TransactionsRead)
            .WithName("SearchTransactions")
            .WithSummary("Server-side filtered, sorted, grouped and paged transaction search.");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTransactionByIdQuery(id), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.TransactionsRead)
            .WithName("GetTransactionById");

        group.MapPost("/", async (
                [FromBody] CreateTransactionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new CreateTransactionCommand(request), cancellationToken);
                return ApiResults.Created($"/api/transactions/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.TransactionsWrite)
            .WithName("CreateTransaction");

        group.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateTransactionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new UpdateTransactionCommand(id, request), cancellationToken);
                return ApiResults.NoContentEnvelope("Transaction updated.");
            })
            .RequireAuthorization(Permissions.TransactionsWrite)
            .WithName("UpdateTransaction");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteTransactionCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope("Transaction deleted.");
            })
            .RequireAuthorization(Permissions.TransactionsWrite)
            .WithName("DeleteTransaction");

        group.MapPost("/bulk-assign", async (
                [FromBody] BulkAssignRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new BulkAssignTransactionsCommand(request), cancellationToken);
                return ApiResults.Ok(result, "Bulk assignment completed.");
            })
            .RequireAuthorization(Permissions.TransactionsWrite)
            .WithName("BulkAssignTransactions");

        group.MapPost("/export", async (
                [FromBody] TransactionFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var bytes = await sender.Send(new ExportTransactionsQuery(filter), cancellationToken);
                return Results.File(bytes, "text/csv", $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            })
            .RequireAuthorization(Permissions.TransactionsRead)
            .WithName("ExportTransactions")
            .WithSummary("Exports the filtered result set as CSV with formula injection neutralised.");

        return app;
    }
}
