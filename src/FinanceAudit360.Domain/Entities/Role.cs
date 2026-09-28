using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Exceptions;
using FinanceAudit360.Shared.Extensions;

namespace FinanceAudit360.Domain.Entities;

public class Role : AuditableEntity, IAggregateRoot
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    private Role(string name, string? description)
    {
        Name = name;
        NormalizedName = name.ToUpperInvariantSafe();
        Description = description;
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystemRole { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public static Role Create(string name, string? description = null, bool isSystemRole = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("role.name_required", "Role name is required.");
        }

        return new Role(name.NormalizeText(), description.NormalizeOrNull()) { IsSystemRole = isSystemRole };
    }

    public void Rename(string name)
    {
        if (IsSystemRole)
        {
            throw new BusinessRuleViolationException("role.system_immutable", "System roles cannot be renamed.");
        }

        Name = name.NormalizeText();
        NormalizedName = Name.ToUpperInvariantSafe();
    }

    public void UpdateDescription(string? description) => Description = description.NormalizeOrNull();

    public void GrantPermission(string permission)
    {
        var normalized = permission.NormalizeText().ToLowerInvariant();
        if (_permissions.Any(p => p.Permission == normalized))
        {
            return;
        }

        _permissions.Add(RolePermission.Create(Id, normalized));
    }

    public void RevokePermission(string permission)
    {
        var normalized = permission.NormalizeText().ToLowerInvariant();
        _permissions.RemoveAll(p => p.Permission == normalized);
    }

    public void ReplacePermissions(IEnumerable<string> permissions)
    {
        _permissions.Clear();
        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            GrantPermission(permission);
        }
    }
}

public class RolePermission : Entity
{
    private RolePermission()
    {
    }

    private RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid RoleId { get; private set; }

    public Role? Role { get; private set; }

    public string Permission { get; private set; } = string.Empty;

    internal static RolePermission Create(Guid roleId, string permission) => new(roleId, permission);
}

public class UserRole : Entity
{
    private UserRole()
    {
    }

    private UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedOnUtc = DateTime.UtcNow;
    }

    public Guid UserId { get; private set; }

    public User? User { get; private set; }

    public Guid RoleId { get; private set; }

    public Role? Role { get; private set; }

    public DateTime AssignedOnUtc { get; private set; }

    internal static UserRole Create(Guid userId, Guid roleId) => new(userId, roleId);
}
