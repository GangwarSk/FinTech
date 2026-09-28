using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceAudit360.Application.Common.Interfaces;

/// <summary>
/// Read model surface of the persistence layer. Query handlers depend on this rather than
/// the concrete DbContext so they remain testable and free of EF provider concerns.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Role> Roles { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Bank> Banks { get; }

    DbSet<BankAccount> BankAccounts { get; }

    DbSet<CreditCard> CreditCards { get; }

    DbSet<CreditCardStatement> Statements { get; }

    DbSet<StatementFile> StatementFiles { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<TransactionCategory> Categories { get; }

    DbSet<Vendor> Vendors { get; }

    DbSet<Person> Persons { get; }

    DbSet<MoneyLedger> MoneyLedgers { get; }

    DbSet<UploadHistory> UploadHistories { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Setting> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
