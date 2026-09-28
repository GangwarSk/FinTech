using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;

namespace FinanceAudit360.Domain.Entities;

/// <summary>Physical PDF kept on disk. The password is never persisted - only the fact that one was needed.</summary>
public class StatementFile : AuditableEntity, IAggregateRoot
{
    private StatementFile()
    {
    }

    private StatementFile(string originalFileName, string storedFileName, string relativePath, long sizeInBytes, string contentHash)
    {
        OriginalFileName = originalFileName;
        StoredFileName = storedFileName;
        RelativePath = relativePath;
        SizeInBytes = sizeInBytes;
        ContentHash = contentHash;
    }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StoredFileName { get; private set; } = string.Empty;

    public string RelativePath { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = "application/pdf";

    public long SizeInBytes { get; private set; }

    /// <summary>SHA-256 of the uploaded bytes; guarantees the same PDF is not imported twice.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public bool IsPasswordProtected { get; private set; }

    public int PageCount { get; private set; }

    public BankCode DetectedBank { get; private set; } = BankCode.Unknown;

    public StatementKind DetectedKind { get; private set; } = StatementKind.Unknown;

    public string? ExtractedTextPath { get; private set; }

    public static StatementFile Create(
        string originalFileName,
        string storedFileName,
        string relativePath,
        long sizeInBytes,
        string contentHash,
        string contentType = "application/pdf")
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new DomainException("statementfile.name_required", "Original file name is required.");
        }

        if (sizeInBytes <= 0)
        {
            throw new DomainException("statementfile.empty", "Uploaded file is empty.");
        }

        // Only the surrounding whitespace is trimmed: the stored name must match what the user uploaded,
        // so it can be shown back to them verbatim and matched against the file on their machine.
        return new StatementFile(originalFileName.Trim(), storedFileName, relativePath, sizeInBytes, contentHash)
        {
            ContentType = contentType
        };
    }

    public void SetPdfMetadata(bool isPasswordProtected, int pageCount)
    {
        IsPasswordProtected = isPasswordProtected;
        PageCount = pageCount;
    }

    public void SetDetection(BankCode bank, StatementKind kind)
    {
        DetectedBank = bank;
        DetectedKind = kind;
    }

    public void SetExtractedTextPath(string? path) => ExtractedTextPath = path;
}
