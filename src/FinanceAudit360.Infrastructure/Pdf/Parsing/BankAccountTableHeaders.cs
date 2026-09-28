namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

/// <summary>Column captions used by Indian savings/current account statements.</summary>
public static class BankAccountTableHeaders
{
    /// <summary>ICICI e-statement: "DATE | MODE** | PARTICULARS | DEPOSITS | WITHDRAWALS | BALANCE".</summary>
    public static BankAccountTableHeader IciciEStatement { get; } =
        new("DATE", "MODE", "PARTICULARS", "DEPOSITS", "WITHDRAWALS", "BALANCE");

    /// <summary>
    /// ICICI net-banking detailed statement: "S No. | Value Date | Transaction Date | Cheque Number |
    /// Transaction Remarks | Withdrawal Amount (INR) | Deposit Amount (INR) | Balance (INR)".
    /// </summary>
    public static BankAccountTableHeader IciciDetailed { get; } =
        new("Value", null, "Remarks", "Deposit", "Withdrawal", "Balance");

    public static IReadOnlyList<BankAccountTableHeader> Common { get; } =
    [
        IciciEStatement,
        IciciDetailed,
        new("Date", null, "Narration", "Deposit", "Withdrawal", "Balance"),
        new("Date", null, "Particulars", "Deposit", "Withdrawal", "Balance"),
        new("Date", null, "Particulars", "Credit", "Debit", "Balance"),
        new("Date", null, "Description", "Credit", "Debit", "Balance"),
        new("Date", null, "Details", "Credit", "Debit", "Balance"),
        new("Date", null, "Remarks", "Credit", "Debit", "Balance")
    ];
}
