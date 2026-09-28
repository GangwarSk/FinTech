using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>Tracks the full lifecycle of one PDF upload so the UI can resume or report failures.</summary>
public class UploadHistory : AuditableEntity, IAggregateRoot
{
    private UploadHistory()
    {
    }

    private UploadHistory(string fileName, long sizeInBytes)
    {
        FileName = fileName;
        SizeInBytes = sizeInBytes;
        Status = UploadStatus.Pending;
        StartedOnUtc = DateTime.UtcNow;
    }

    public string FileName { get; private set; } = string.Empty;

    public long SizeInBytes { get; private set; }

    public UploadStatus Status { get; private set; }

    public Guid? StatementFileId { get; private set; }

    public StatementFile? StatementFile { get; private set; }

    public Guid? StatementId { get; private set; }

    public CreditCardStatement? Statement { get; private set; }

    public BankCode DetectedBank { get; private set; } = BankCode.Unknown;

    public StatementKind DetectedKind { get; private set; } = StatementKind.Unknown;

    public string? ParserName { get; private set; }

    public bool RequiresPassword { get; private set; }

    public int TransactionsExtracted { get; private set; }

    public int TransactionsImported { get; private set; }

    public int TransactionsSkipped { get; private set; }

    public DateTime StartedOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    public long DurationMs { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>JSON array of non-fatal parser warnings surfaced to the user.</summary>
    public string? WarningsJson { get; private set; }

    public string? UploadedByIp { get; private set; }

    public static UploadHistory Start(string fileName, long sizeInBytes, string? uploadedByIp = null) =>
        new(fileName.NormalizeText(), sizeInBytes) { UploadedByIp = uploadedByIp };

    public void AttachFile(Guid statementFileId) => StatementFileId = statementFileId;

    public void MarkAwaitingPassword()
    {
        RequiresPassword = true;
        Status = UploadStatus.AwaitingPassword;
    }

    public void MarkProcessing(BankCode bank, StatementKind kind, string? parserName)
    {
        DetectedBank = bank;
        DetectedKind = kind;
        ParserName = parserName.NormalizeOrNull();
        Status = UploadStatus.Processing;
    }

    public void MarkParsed(int transactionsExtracted)
    {
        TransactionsExtracted = transactionsExtracted;
        Status = UploadStatus.Parsed;
    }

    public void MarkCompleted(Guid statementId, int imported, int skipped, string? warningsJson = null)
    {
        StatementId = statementId;
        TransactionsImported = imported;
        TransactionsSkipped = skipped;
        WarningsJson = warningsJson;
        Status = UploadStatus.Completed;
        Complete();
    }

    public void MarkDuplicate(Guid existingStatementId)
    {
        StatementId = existingStatementId;
        Status = UploadStatus.Duplicate;
        ErrorCode = "upload.duplicate";
        ErrorMessage = "This statement has already been imported.";
        Complete();
    }

    public void MarkFailed(string errorCode, string errorMessage)
    {
        Status = UploadStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage.Truncate(2000);
        Complete();
    }

    public void MarkCancelled()
    {
        Status = UploadStatus.Cancelled;
        Complete();
    }

    private void Complete()
    {
        CompletedOnUtc = DateTime.UtcNow;
        DurationMs = (long)(CompletedOnUtc.Value - StartedOnUtc).TotalMilliseconds;
    }
}
