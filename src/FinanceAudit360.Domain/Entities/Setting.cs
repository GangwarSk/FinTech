using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class Setting : AuditableEntity, IAggregateRoot
{
    private Setting()
    {
    }

    private Setting(string key, string? value, SettingDataType dataType, string category)
    {
        Key = key;
        Value = value;
        DataType = dataType;
        Category = category;
    }

    public string Key { get; private set; } = string.Empty;

    public string? Value { get; private set; }

    public SettingDataType DataType { get; private set; }

    public string Category { get; private set; } = "General";

    public string? Description { get; private set; }

    public string? DefaultValue { get; private set; }

    /// <summary>System settings are seeded and may be edited but never deleted.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Encrypted or sensitive values are never returned to non-admin callers.</summary>
    public bool IsSecret { get; private set; }

    public static Setting Create(
        string key,
        string? value,
        SettingDataType dataType = SettingDataType.String,
        string category = "General",
        string? description = null,
        bool isSystem = false,
        bool isSecret = false)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException("setting.key_required", "Setting key is required.");
        }

        return new Setting(key.NormalizeText(), value, dataType, category.NormalizeText())
        {
            Description = description.NormalizeOrNull(),
            DefaultValue = value,
            IsSystem = isSystem,
            IsSecret = isSecret
        };
    }

    public void SetValue(string? value) => Value = value;

    public void UpdateMetadata(string? description, string category)
    {
        Description = description.NormalizeOrNull();
        Category = category.NormalizeText();
    }

    public bool AsBoolean() => bool.TryParse(Value, out var result) && result;

    public int AsInt(int fallback = 0) => int.TryParse(Value, out var result) ? result : fallback;

    public decimal AsDecimal(decimal fallback = 0m) => Value.ToDecimalOrNull() ?? fallback;
}
