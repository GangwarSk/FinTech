namespace FinanceAudit360.Shared.Models;

/// <summary>Base for every server-side paged/sorted request. Page size is clamped to protect the database.</summary>
public abstract class PaginationRequest
{
    public const int MaxPageSize = 500;
    public const int DefaultPageSize = 25;

    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public string? SortBy { get; set; }

    public SortDirection SortDirection { get; set; } = SortDirection.Descending;

    public int Skip => (PageNumber - 1) * PageSize;
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}
