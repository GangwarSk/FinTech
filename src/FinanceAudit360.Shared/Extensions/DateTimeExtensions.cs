namespace FinanceAudit360.Shared.Extensions;

public static class DateTimeExtensions
{
    public static DateTime StartOfDay(this DateTime value) => value.Date;

    public static DateTime EndOfDay(this DateTime value) => value.Date.AddDays(1).AddTicks(-1);

    public static DateTime StartOfMonth(this DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, value.Kind);

    public static DateTime EndOfMonth(this DateTime value) =>
        value.StartOfMonth().AddMonths(1).AddTicks(-1);

    public static (DateTime From, DateTime To) PreviousPeriodOf(DateTime from, DateTime to)
    {
        var span = to - from;
        var previousTo = from.AddTicks(-1);
        var previousFrom = previousTo - span;
        return (previousFrom, previousTo);
    }
}
