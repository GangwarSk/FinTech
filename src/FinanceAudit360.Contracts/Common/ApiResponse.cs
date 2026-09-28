namespace FinanceAudit360.Contracts.Common;

/// <summary>Uniform envelope returned by every endpoint so the client has one shape to handle.</summary>
public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, ApiError? Error)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message, null);

    public static ApiResponse<T> Fail(ApiError error) => new(false, default, error.Message, error);
}

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record LookupDto(Guid Id, string Name, string? Description = null);

public sealed record EnumOptionDto(int Value, string Name, string Label);

public sealed record IdResponse(Guid Id);

public sealed record BulkResultDto(int Requested, int Succeeded, int Failed, IReadOnlyList<string> Errors);
