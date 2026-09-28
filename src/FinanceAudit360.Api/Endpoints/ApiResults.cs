using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Shared.Models;

namespace FinanceAudit360.Api.Endpoints;

/// <summary>Wraps handler output in the shared response envelope so every endpoint looks the same.</summary>
public static class ApiResults
{
    public static IResult Ok<T>(T data, string? message = null) =>
        Results.Ok(ApiResponse<T>.Ok(data, message));

    public static IResult Created<T>(string location, T data) =>
        Results.Created(location, ApiResponse<T>.Ok(data));

    public static IResult NoContentEnvelope(string? message = null) =>
        Results.Ok(ApiResponse<object?>.Ok(null, message ?? "Completed."));

    public static IResult Paged<T>(PagedResult<T> page) =>
        Results.Ok(ApiResponse<PagedResponse<T>>.Ok(new PagedResponse<T>(
            page.Items,
            page.TotalCount,
            page.PageNumber,
            page.PageSize,
            page.TotalPages,
            page.HasPreviousPage,
            page.HasNextPage)));
}
