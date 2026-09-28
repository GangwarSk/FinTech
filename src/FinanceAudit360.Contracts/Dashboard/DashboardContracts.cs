namespace FinanceAudit360.Contracts.Dashboard;

public enum DashboardPeriod
{
    Today = 1,
    Last7Days = 2,
    Last30Days = 3,
    Last90Days = 4,
    Custom = 99
}

public sealed record DashboardQueryRequest
{
    public DashboardPeriod Period { get; init; } = DashboardPeriod.Last30Days;

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

/// <summary>A single KPI tile: current value, delta against the equivalent previous period and a trend flag.</summary>
public sealed record KpiCardDto(
    string Key,
    string Title,
    decimal Value,
    string ValueFormat,
    decimal PreviousValue,
    decimal Difference,
    decimal? PercentChange,
    string Trend,
    string? Caption = null);

public sealed record TopEntityDto(Guid? Id, string Name, decimal Amount, int TransactionCount);

public sealed record DashboardSummaryDto(
    DateTime From,
    DateTime To,
    DateTime PreviousFrom,
    DateTime PreviousTo,
    string Currency,
    IReadOnlyList<KpiCardDto> Cards,
    TopEntityDto? TopSpendingCard,
    TopEntityDto? TopSpendingVendor,
    TopEntityDto? MostActiveBank,
    TopEntityDto? MostActivePerson);
