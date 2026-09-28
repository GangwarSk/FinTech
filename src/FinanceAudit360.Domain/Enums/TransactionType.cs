namespace FinanceAudit360.Domain.Enums;

/// <summary>Business classification of a transaction line extracted from a statement or entered manually.</summary>
public enum TransactionType
{
    Credit = 1,
    Debit = 2,
    Transfer = 3,
    Refund = 4,
    CashWithdrawal = 5,
    Payment = 6,
    Adjustment = 7,
    Reversal = 8,
    Charges = 9,
    Fees = 10,
    Interest = 11,
    Reward = 12,
    Loan = 13,
    Emi = 14,
    Other = 99
}

/// <summary>Money flow direction, independent of the business type.</summary>
public enum TransactionDirection
{
    Credit = 1,
    Debit = 2
}

public enum TransactionSource
{
    Manual = 1,
    StatementImport = 2,
    ApiImport = 3
}
