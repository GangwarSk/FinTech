namespace FinanceAudit360.Domain.Enums;

public enum BankCode
{
    Unknown = 0,
    Hdfc = 1,
    Icici = 2,
    Sbi = 3,
    Axis = 4,
    Kotak = 5,
    IndusInd = 6,
    StandardChartered = 7,
    AmericanExpress = 8,
    Citi = 9,
    Yes = 10,
    Rbl = 11,
    IdfcFirst = 12,
    SouthIndian = 13,
    UniCard = 14,
    Slice = 15,
    PunjabNational = 16,
    UttarPradeshGramin = 17,
    AirtelPayments = 18,
    PaytmPostPaid = 19,
    Other = 99
}

public enum StatementKind
{
    CreditCard = 1,
    BankAccount = 2,
    Wallet = 3,
    Unknown = 99
}

public enum UploadStatus
{
    Pending = 0,
    AwaitingPassword = 1,
    Processing = 2,
    Parsed = 3,
    Completed = 4,
    Failed = 5,
    Duplicate = 6,
    Cancelled = 7
}

public enum StatementStatus
{
    Draft = 0,
    Imported = 1,
    Reconciled = 2,
    Archived = 3
}

public enum AccountType
{
    Savings = 1,
    Current = 2,
    Salary = 3,
    Nre = 4,
    Nro = 5,
    Wallet = 6,
    Fixed = 7,
    Other = 99
}

public enum CardNetwork
{
    Unknown = 0,
    Visa = 1,
    Mastercard = 2,
    Rupay = 3,
    Amex = 4,
    Diners = 5
}

/// <summary>Direction of a person-to-person money movement in the ledger.</summary>
public enum LedgerEntryType
{
    /// <summary>Money I gave to the person - they owe me.</summary>
    Given = 1,

    /// <summary>Money I received from the person - I owe them.</summary>
    Taken = 2,

    /// <summary>Settlement of a previously given amount.</summary>
    SettlementReceived = 3,

    /// <summary>Settlement of a previously taken amount.</summary>
    SettlementPaid = 4
}

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Login = 4,
    LoginFailed = 5,
    Logout = 6,
    Upload = 7,
    Export = 8,
    PermissionDenied = 9
}

public enum SettingDataType
{
    String = 1,
    Number = 2,
    Boolean = 3,
    Json = 4,
    Date = 5
}
