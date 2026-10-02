using System.Linq.Expressions;
using System.Reflection;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Common;
using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceAudit360.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Bank> Banks => Set<Bank>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<CreditCard> CreditCards => Set<CreditCard>();

    public DbSet<CreditCardStatement> Statements => Set<CreditCardStatement>();

    public DbSet<StatementFile> StatementFiles => Set<StatementFile>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionCategory> Categories => Set<TransactionCategory>();

    public DbSet<Vendor> Vendors => Set<Vendor>();

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<MoneyLedger> MoneyLedgers => Set<MoneyLedger>();

    public DbSet<UploadHistory> UploadHistories => Set<UploadHistory>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Setting> Settings => Set<Setting>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fin");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplySoftDeleteQueryFilters(modelBuilder);
        ApplyDecimalPrecision(modelBuilder);
        ApplyUtcDateTimeConversion(modelBuilder);
        ApplyClientGeneratedKeys(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// The domain assigns every identifier itself (sequential GUID v7). Telling EF the key is never
    /// store-generated is what makes it treat a child added through a navigation as an INSERT
    /// rather than an UPDATE.
    /// </summary>
    private static void ApplyClientGeneratedKeys(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key is null || key.Properties.Count != 1)
            {
                continue;
            }

            var property = key.Properties[0];
            if (property.ClrType == typeof(Guid))
            {
                property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }
    }

    /// <summary>
    /// Adds "WHERE IsDeleted = 0" to every soft-deletable root so no query has to remember it.
    /// Use IgnoreQueryFilters() explicitly when an administrator needs to see deleted rows.
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || !typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    private static void ApplyDecimalPrecision(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }

    /// <summary>
    /// PostgreSQL maps <see cref="DateTime"/> to "timestamp with time zone", and Npgsql refuses to write a
    /// value whose <see cref="DateTimeKind"/> is not UTC. Domain values arrive as Unspecified (business dates
    /// parsed from statements and request payloads), so every DateTime property is converted to UTC on the way
    /// in and materialised back as a UTC value on the way out.
    /// </summary>
    private static void ApplyUtcDateTimeConversion(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var nullableConverter = new ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue
                ? (value.Value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
                : value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(converter);
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(nullableConverter);
            }
        }
    }
}

/// <summary>Enables "dotnet ef migrations add" without spinning up the API host.</summary>
public sealed class ApplicationDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FINANCEAUDIT360_CONNECTION")
                               ?? "Host=localhost;Port=5433;Database=FinTech;Username=postgres;Password=grsk@12345;Include Error Detail=true";

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseNpgsql(connectionString, sql =>
            sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name));

        return new ApplicationDbContext(builder.Options);
    }
}
