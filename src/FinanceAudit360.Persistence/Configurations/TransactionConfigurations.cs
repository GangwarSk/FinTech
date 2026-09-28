using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceAudit360.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description).HasMaxLength(1000).IsRequired();
        builder.Property(t => t.NormalizedDescription).HasMaxLength(1000).IsRequired();
        builder.Property(t => t.ReferenceNumber).HasMaxLength(100);
        builder.Property(t => t.MerchantRawText).HasMaxLength(500);
        builder.Property(t => t.Location).HasMaxLength(200);
        builder.Property(t => t.Notes).HasMaxLength(2000);
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.Direction).HasConversion<int>();
        builder.Property(t => t.TransactionType).HasConversion<int>();
        builder.Property(t => t.Source).HasConversion<int>();
        builder.Property(t => t.DedupeHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.ModifiedBy).HasMaxLength(100);
        builder.Property(t => t.DeletedBy).HasMaxLength(100);
        builder.Ignore(t => t.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        // The explorer grid always filters/sorts by date first, so it leads every covering index.
        builder.HasIndex(t => t.TransactionDate).HasDatabaseName("IX_Transactions_TransactionDate");
        builder.HasIndex(t => t.PostingDate).HasDatabaseName("IX_Transactions_PostingDate");
        builder.HasIndex(t => new { t.CreditCardId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Card_Date");
        builder.HasIndex(t => new { t.BankAccountId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Account_Date");
        builder.HasIndex(t => new { t.PersonId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Person_Date");
        builder.HasIndex(t => new { t.VendorId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Vendor_Date");
        builder.HasIndex(t => new { t.CategoryId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Category_Date");
        builder.HasIndex(t => new { t.BankId, t.TransactionDate }).HasDatabaseName("IX_Transactions_Bank_Date");
        builder.HasIndex(t => t.StatementId).HasDatabaseName("IX_Transactions_Statement");
        builder.HasIndex(t => t.ReferenceNumber).HasDatabaseName("IX_Transactions_Reference");
        builder.HasIndex(t => t.Direction).HasDatabaseName("IX_Transactions_Direction");
        builder.HasIndex(t => t.IsDeleted).HasDatabaseName("IX_Transactions_IsDeleted");

        builder.HasIndex(t => t.DedupeHash)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"StatementId\" IS NOT NULL")
            .HasDatabaseName("UX_Transactions_DedupeHash");

        builder.HasOne(t => t.Vendor).WithMany().HasForeignKey(t => t.VendorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(t => t.Bank).WithMany().HasForeignKey(t => t.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Person).WithMany().HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class TransactionCategoryConfiguration : IEntityTypeConfiguration<TransactionCategory>
{
    public void Configure(EntityTypeBuilder<TransactionCategory> builder)
    {
        builder.ToTable("TransactionCategories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Code).HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.ColorHex).HasMaxLength(10);
        builder.Property(c => c.Icon).HasMaxLength(50);
        builder.Property(c => c.MatchKeywords).HasMaxLength(1000);
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.ModifiedBy).HasMaxLength(100);
        builder.Property(c => c.DeletedBy).HasMaxLength(100);
        builder.Ignore(c => c.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(c => c.Name).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Categories_Name");

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Children).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).HasMaxLength(250).IsRequired();
        builder.Property(v => v.NormalizedName).HasMaxLength(250).IsRequired();
        builder.Property(v => v.DisplayName).HasMaxLength(250);
        builder.Property(v => v.Category).HasMaxLength(100);
        builder.Property(v => v.Website).HasMaxLength(300);
        builder.Property(v => v.MatchKeywords).HasMaxLength(2000);
        builder.Property(v => v.Notes).HasMaxLength(2000);
        builder.Property(v => v.CreatedBy).HasMaxLength(100);
        builder.Property(v => v.ModifiedBy).HasMaxLength(100);
        builder.Property(v => v.DeletedBy).HasMaxLength(100);
        builder.Ignore(v => v.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(v => v.NormalizedName).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Vendors_Name");

        builder.HasOne(v => v.DefaultCategory).WithMany().HasForeignKey(v => v.DefaultCategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Mobile).HasMaxLength(20);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.Relationship).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.Property(p => p.MatchKeywords).HasMaxLength(1000);
        builder.Property(p => p.CreatedBy).HasMaxLength(100);
        builder.Property(p => p.ModifiedBy).HasMaxLength(100);
        builder.Property(p => p.DeletedBy).HasMaxLength(100);
        builder.Ignore(p => p.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsOne(p => p.Address, owned =>
        {
            owned.Property(a => a.Line1).HasColumnName("AddressLine1").HasMaxLength(250);
            owned.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(250);
            owned.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(100);
            owned.Property(a => a.State).HasColumnName("AddressState").HasMaxLength(100);
            owned.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(20);
            owned.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(100);
        });

        builder.Navigation(p => p.Address).IsRequired();

        builder.HasIndex(p => p.NormalizedName).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Persons_Name");
        builder.HasIndex(p => p.Mobile).HasDatabaseName("IX_Persons_Mobile");
        builder.HasIndex(p => p.OutstandingBalance).HasDatabaseName("IX_Persons_Outstanding");

        builder.HasMany(p => p.LedgerEntries).WithOne(l => l.Person).HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.LedgerEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class MoneyLedgerConfiguration : IEntityTypeConfiguration<MoneyLedger>
{
    public void Configure(EntityTypeBuilder<MoneyLedger> builder)
    {
        builder.ToTable("MoneyLedgers");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.EntryType).HasConversion<int>();
        builder.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(1000);
        builder.Property(l => l.ReferenceNumber).HasMaxLength(100);
        builder.Property(l => l.CreatedBy).HasMaxLength(100);
        builder.Property(l => l.ModifiedBy).HasMaxLength(100);
        builder.Property(l => l.DeletedBy).HasMaxLength(100);
        builder.Ignore(l => l.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(l => l.SignedAmount);

        builder.HasIndex(l => new { l.PersonId, l.EntryDate }).HasDatabaseName("IX_MoneyLedgers_Person_Date");
        builder.HasIndex(l => l.EntryType).HasDatabaseName("IX_MoneyLedgers_EntryType");

        builder.HasOne(l => l.Transaction).WithMany().HasForeignKey(l => l.TransactionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(l => l.BankAccount).WithMany().HasForeignKey(l => l.BankAccountId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(l => l.CreditCard).WithMany().HasForeignKey(l => l.CreditCardId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.EntityName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.UserName).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.HttpMethod).HasMaxLength(10);
        builder.Property(a => a.Endpoint).HasMaxLength(500);
        builder.Property(a => a.AffectedColumns).HasMaxLength(2000);
        builder.Property(a => a.Message).HasMaxLength(2000);

        // Left untyped so SQL Server maps it to nvarchar(max) while SQLite (used by the tests) maps it to TEXT.
        builder.Property(a => a.OldValuesJson);
        builder.Property(a => a.NewValuesJson);

        builder.HasIndex(a => a.TimestampUtc).HasDatabaseName("IX_AuditLogs_Timestamp");
        builder.HasIndex(a => new { a.EntityName, a.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
        builder.HasIndex(a => a.UserId).HasDatabaseName("IX_AuditLogs_User");
    }
}

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Value).HasMaxLength(4000);
        builder.Property(s => s.DefaultValue).HasMaxLength(4000);
        builder.Property(s => s.DataType).HasConversion<int>();
        builder.Property(s => s.Category).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.ModifiedBy).HasMaxLength(100);
        builder.Property(s => s.DeletedBy).HasMaxLength(100);
        builder.Ignore(s => s.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(s => s.Key).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Settings_Key");
        builder.HasIndex(s => s.Category).HasDatabaseName("IX_Settings_Category");
    }
}
