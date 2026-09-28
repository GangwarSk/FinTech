using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceAudit360.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.ModifiedBy).HasMaxLength(100);
        builder.Property(u => u.DeletedBy).HasMaxLength(100);
        builder.Ignore(u => u.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Users_UserName");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Users_Email");
        builder.HasIndex(u => u.IsDeleted).HasDatabaseName("IX_Users_IsDeleted");

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.UserRoles).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(u => u.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.CreatedBy).HasMaxLength(100);
        builder.Property(r => r.ModifiedBy).HasMaxLength(100);
        builder.Property(r => r.DeletedBy).HasMaxLength(100);
        builder.Ignore(r => r.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(r => r.NormalizedName).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Roles_Name");

        builder.HasMany(r => r.Permissions)
            .WithOne(p => p.Role)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(r => r.UserRoles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Permission).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => new { p.RoleId, p.Permission }).IsUnique().HasDatabaseName("UX_RolePermissions_Role_Permission");
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => ur.Id);

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique().HasDatabaseName("UX_UserRoles_User_Role");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.UserAgent).HasMaxLength(500);
        builder.Property(t => t.RevokedReason).HasMaxLength(200);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");
        builder.HasIndex(t => new { t.UserId, t.ExpiresOnUtc }).HasDatabaseName("IX_RefreshTokens_User_Expiry");
    }
}
