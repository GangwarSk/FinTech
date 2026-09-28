using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class Vendor : AuditableEntity, IAggregateRoot
{
    private Vendor()
    {
    }

    private Vendor(string name, string normalizedName)
    {
        Name = name;
        NormalizedName = normalizedName;
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    public string? Category { get; private set; }

    public Guid? DefaultCategoryId { get; private set; }

    public TransactionCategory? DefaultCategory { get; private set; }

    public string? Website { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>Pipe-separated raw merchant strings seen on statements that resolve to this vendor.</summary>
    public string? MatchKeywords { get; private set; }

    public static Vendor Create(string name, string? matchKeywords = null, Guid? defaultCategoryId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("vendor.name_required", "Vendor name is required.");
        }

        var normalized = name.NormalizeText();
        return new Vendor(normalized, normalized.ToUpperInvariant())
        {
            MatchKeywords = matchKeywords.NormalizeOrNull(),
            DefaultCategoryId = defaultCategoryId
        };
    }

    public void Update(
        string name,
        string? displayName,
        string? category,
        Guid? defaultCategoryId,
        string? website,
        string? matchKeywords,
        string? notes)
    {
        Name = name.NormalizeText();
        NormalizedName = Name.ToUpperInvariant();
        DisplayName = displayName.NormalizeOrNull();
        Category = category.NormalizeOrNull();
        DefaultCategoryId = defaultCategoryId;
        Website = website.NormalizeOrNull();
        MatchKeywords = matchKeywords.NormalizeOrNull();
        Notes = notes.NormalizeOrNull();
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void AddKeyword(string keyword)
    {
        var token = keyword.NormalizeText().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(token) || KeywordTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        MatchKeywords = string.IsNullOrWhiteSpace(MatchKeywords) ? token : $"{MatchKeywords}|{token}";
    }

    public IEnumerable<string> KeywordTokens =>
        (MatchKeywords ?? string.Empty)
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
