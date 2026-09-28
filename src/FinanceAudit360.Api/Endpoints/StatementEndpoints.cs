using FinanceAudit360.Application.Features.Statements;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class StatementEndpoints
{
    public static IEndpointRouteBuilder MapStatementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statements").WithTags("Statements");

        group.MapPost("/upload", async (
                IFormFile file,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await using var stream = file.OpenReadStream();
                var result = await sender.Send(
                    new UploadStatementCommand(stream, Path.GetFileName(file.FileName), file.Length),
                    cancellationToken);

                return ApiResults.Ok(result, result.RequiresPassword
                    ? "This statement is password protected. Supply the password to continue."
                    : "Upload accepted. Call /process to import the transactions.");
            })
            .RequireAuthorization(Permissions.StatementsUpload)
            .RequireRateLimiting(AppConstants.RateLimitPolicyUpload)
            // Bearer tokens are not sent automatically by the browser, so form posts are not CSRF-able here.
            .DisableAntiforgery()
            .WithName("UploadStatement")
            .WithSummary("Stores the PDF and reports whether a password is required.");

        group.MapPost("/process", async (
                [FromBody] ProcessStatementRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ProcessStatementCommand(request), cancellationToken);
                return ApiResults.Ok(result, result.ErrorCode is null ? "Statement imported." : result.ErrorMessage);
            })
            .RequireAuthorization(Permissions.StatementsUpload)
            .RequireRateLimiting(AppConstants.RateLimitPolicyUpload)
            .WithName("ProcessStatement")
            .WithSummary("Decrypts, parses and imports a previously uploaded statement.");

        group.MapPost("/search", async (
                [FromBody] StatementFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SearchStatementsQuery(filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("SearchStatements");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetStatementByIdQuery(id), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("GetStatementById");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteStatementCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope("Statement and its transactions were removed.");
            })
            .RequireAuthorization(Permissions.StatementsUpload)
            .WithName("DeleteStatement");

        group.MapPost("/uploads/search", async (
                [FromBody] UploadHistoryFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetUploadHistoryQuery(filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("SearchUploadHistory");

        MapStatementFileEndpoints(group);

        return app;
    }

    /// <summary>
    /// The uploaded PDF is the lifecycle root: deleting it takes its upload history, statement and imported
    /// transactions with it, recoverably, until it is purged from the recycle bin.
    /// </summary>
    private static void MapStatementFileEndpoints(RouteGroupBuilder group)
    {
        var files = group.MapGroup("/files");

        files.MapPost("/search", async (
                [FromBody] StatementFileFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SearchStatementFilesQuery(filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("SearchStatementFiles")
            .WithSummary("Lists uploaded files with the statement and transaction counts they produced.");

        files.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetStatementFileDetailQuery(id), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("GetStatementFile")
            .WithSummary("Everything one uploaded file produced, including when it is in the recycle bin.");

        files.MapPost("/{id:guid}/transactions", async (
                Guid id,
                [FromBody] StatementFileTransactionFilter filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetStatementFileTransactionsQuery(id, filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.StatementsRead)
            .WithName("GetStatementFileTransactions")
            .WithSummary("Transactions imported from one file, so an upload can be verified row by row.");

        files.MapGet("/{id:guid}/impact", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetStatementFileImpactQuery(id), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.StatementsDelete)
            .WithName("GetStatementFileImpact")
            .WithSummary("Counts shown in the delete confirmation before anything is removed.");

        files.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteStatementFileCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope(
                    "The file and its imported data were moved to the recycle bin.");
            })
            .RequireAuthorization(Permissions.StatementsDelete)
            .WithName("DeleteStatementFile")
            .WithSummary("Soft-deletes the file, its upload history, its statement and its transactions.");

        files.MapPost("/recycle-bin/search", async (
                [FromBody] StatementFileFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                filter.DeletedOnly = true;
                var result = await sender.Send(new SearchStatementFilesQuery(filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.StatementsDelete)
            .WithName("SearchRecycleBin");

        files.MapPost("/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new RestoreStatementFileCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope("The file and its imported data were restored.");
            })
            .RequireAuthorization(Permissions.StatementsDelete)
            .WithName("RestoreStatementFile");

        files.MapDelete("/{id:guid}/purge", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new PurgeStatementFileCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope("The file and its data were permanently deleted.");
            })
            .RequireAuthorization(Permissions.StatementsDelete)
            .WithName("PurgeStatementFile")
            .WithSummary("Removes the file from every table and deletes the stored PDF. Cannot be undone.");
    }
}
