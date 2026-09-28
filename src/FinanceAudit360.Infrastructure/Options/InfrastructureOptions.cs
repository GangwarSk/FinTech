namespace FinanceAudit360.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "FinanceAudit360";

    public string Audience { get; set; } = "FinanceAudit360.Client";

    /// <summary>Must be at least 32 bytes. Supply it through user secrets or an environment variable.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 30;

    public int RefreshTokenDays { get; set; } = 14;

    public int ClockSkewSeconds { get; set; } = 30;
}

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "App_Data/storage";

    public string StatementsFolder { get; set; } = "statements";

    public string ExtractedTextFolder { get; set; } = "extracted";

    public long MaxFileSizeBytes { get; set; } = 25L * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } = [".pdf"];
}

/// <summary>OCR for scanned (image-only) statements.</summary>
public sealed class OcrOptions
{
    public const string SectionName = "Ocr";

    public bool Enabled { get; set; } = true;

    /// <summary>Folder holding <c>&lt;language&gt;.traineddata</c>; relative paths resolve against the app folder.</summary>
    public string TessDataPath { get; set; } = "tessdata";

    public string Language { get; set; } = "eng";

    /// <summary>Render scale over 72 dpi. 3 gives ~216 dpi, which reads statement fonts reliably.</summary>
    public double RenderScale { get; set; } = 3d;

    /// <summary>Pages with fewer extractable words than this are treated as scanned images.</summary>
    public int MinWordsPerPage { get; set; } = 5;

    public int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>Upload and processing both read the file; caching avoids recognising it twice.</summary>
    public int CacheMinutes { get; set; } = 30;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string[] AllowedCorsOrigins { get; set; } = ["http://localhost:5173"];

    public int GlobalRateLimitPerMinute { get; set; } = 240;

    public int AuthRateLimitPerMinute { get; set; } = 10;

    public int UploadRateLimitPerMinute { get; set; } = 20;

    /// <summary>Password used to create the bootstrap administrator on an empty database.</summary>
    public string SeedAdminPassword { get; set; } = string.Empty;

    public bool EnableRequestAuditLogging { get; set; } = true;
}
