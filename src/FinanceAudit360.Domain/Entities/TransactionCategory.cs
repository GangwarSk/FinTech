using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class TransactionCategory : AuditableEntity, IAggregateRoot
{
    private readonly List<TransactionCategory> _children = [];

    private TransactionCategory()
    {
    }

    private TransactionCategory(string name, string? code)
    {
        Name = name;
        Code = code;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public string? Description { get; private set; }

    public string? ColorHex { get; private set; }

    public string? Icon { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    public TransactionCategory? ParentCategory { get; private set; }

    public bool IsSystemCategory { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int DisplayOrder { get; private set; }

    /// <summary>Pipe-separated tokens auto-matched against a transaction description during import.</summary>
    public string? MatchKeywords { get; private set; }

    public IReadOnlyCollection<TransactionCategory> Children => _children.AsReadOnly();

    public static TransactionCategory Create(
        string name,
        string? code = null,
        Guid? parentCategoryId = null,
        string? matchKeywords = null,
        bool isSystemCategory = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("category.name_required", "Category name is required.");
        }

        return new TransactionCategory(name.NormalizeText(), code.NormalizeOrNull()?.ToUpperInvariant())
        {
            ParentCategoryId = parentCategoryId,
            MatchKeywords = matchKeywords.NormalizeOrNull(),
            IsSystemCategory = isSystemCategory
        };
    }

    public void Update(
        string name,
        string? code,
        string? description,
        string? colorHex,
        string? icon,
        Guid? parentCategoryId,
        string? matchKeywords,
        int displayOrder)
    {
        if (parentCategoryId == Id)
        {
            throw new BusinessRuleViolationException("category.self_parent", "A category cannot be its own parent.");
        }

        Name = name.NormalizeText();
        Code = code.NormalizeOrNull()?.ToUpperInvariant();
        Description = description.NormalizeOrNull();
        ColorHex = colorHex.NormalizeOrNull();
        Icon = icon.NormalizeOrNull();
        ParentCategoryId = parentCategoryId;
        MatchKeywords = matchKeywords.NormalizeOrNull();
        DisplayOrder = displayOrder;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public IEnumerable<string> KeywordTokens =>
        (MatchKeywords ?? string.Empty)
        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
