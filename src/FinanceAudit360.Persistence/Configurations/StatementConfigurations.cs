using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceAudit360.Persistence.Configurations;

public sealed class CreditCardStatementConfiguration : IEntityTypeConfiguration<CreditCardStatement>
{
    public void Configure(EntityTypeBuilder<CreditCardStatement> builder)
    {
        builder.ToTable("CreditCardStatements");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Kind).HasConversion<int>();
        builder.Property(s => s.Status).HasConversion<int>();
        builder.Property(s => s.StatementNumber).HasMaxLength(100);
        builder.Property(s => s.AccountNumberMasked).HasMaxLength(50);
        builder.Property(s => s.CardHolderName).HasMaxLength(200);
        builder.Property(s => s.CustomerName).HasMaxLength(200);
        builder.Property(s => s.CustomerEmail).HasMaxLength(256);
        builder.Property(s => s.CustomerPhone).HasMaxLength(20);
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.ParserName).HasMaxLength(100);
        builder.Property(s => s.DedupeKey).HasMaxLength(150).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(100);
        builder.Property(s => s.ModifiedBy).HasMaxLength(100);
        builder.Property(s => s.DeletedBy).HasMaxLength(100);
        builder.Ignore(s => s.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsOne(s => s.CardNumber, owned =>
        {
            owned.Property(n => n.Masked).HasColumnName("CardNumberMasked").HasMaxLength(25);
            owned.Property(n => n.Last4).HasColumnName("CardNumberLast4").HasMaxLength(4);
        });

        builder.OwnsOne(s => s.BillingAddress, owned =>
        {
            owned.Property(a => a.Line1).HasColumnName("AddressLine1").HasMaxLength(250);
            owned.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(250);
            owned.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(100);
            owned.Property(a => a.State).HasColumnName("AddressState").HasMaxLength(100);
            owned.Property(a => a.PostalCode).HasColumnName("AddressPostalCode").HasMaxLength(20);
            owned.Property(a => a.Country).HasColumnName("AddressCountry").HasMaxLength(100);
        });

        builder.Navigation(s => s.BillingAddress).IsRequired();

        builder.HasIndex(s => s.DedupeKey).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Statements_DedupeKey");
        builder.HasIndex(s => new { s.BankId, s.PeriodStart, s.PeriodEnd }).HasDatabaseName("IX_Statements_Bank_Period");
        builder.HasIndex(s => s.CreditCardId).HasDatabaseName("IX_Statements_CreditCard");
        builder.HasIndex(s => s.BankAccountId).HasDatabaseName("IX_Statements_BankAccount");

        builder.HasOne(s => s.Bank).WithMany().HasForeignKey(s => s.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.BankAccount).WithMany().HasForeignKey(s => s.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.StatementFile).WithMany().HasForeignKey(s => s.StatementFileId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Transactions).WithOne(t => t.Statement).HasForeignKey(t => t.StatementId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(s => s.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class StatementFileConfiguration : IEntityTypeConfiguration<StatementFile>
{
    public void Configure(EntityTypeBuilder<StatementFile> builder)
    {
        builder.ToTable("StatementFiles");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(f => f.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(f => f.RelativePath).HasMaxLength(500).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(f => f.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(f => f.ExtractedTextPath).HasMaxLength(500);
        builder.Property(f => f.DetectedBank).HasConversion<int>();
        builder.Property(f => f.DetectedKind).HasConversion<int>();
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.ModifiedBy).HasMaxLength(100);
        builder.Property(f => f.DeletedBy).HasMaxLength(100);
        builder.Ignore(f => f.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(f => f.ContentHash).HasDatabaseName("IX_StatementFiles_ContentHash");
    }
}

public sealed class UploadHistoryConfiguration : IEntityTypeConfiguration<UploadHistory>
{
    public void Configure(EntityTypeBuilder<UploadHistory> builder)
    {
        builder.ToTable("UploadHistories");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FileName).HasMaxLength(260).IsRequired();
        builder.Property(u => u.Status).HasConversion<int>();
        builder.Property(u => u.DetectedBank).HasConversion<int>();
        builder.Property(u => u.DetectedKind).HasConversion<int>();
        builder.Property(u => u.ParserName).HasMaxLength(100);
        builder.Property(u => u.ErrorCode).HasMaxLength(100);
        builder.Property(u => u.ErrorMessage).HasMaxLength(2000);
        builder.Property(u => u.WarningsJson).HasMaxLength(4000);
        builder.Property(u => u.UploadedByIp).HasMaxLength(64);
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.ModifiedBy).HasMaxLength(100);
        builder.Property(u => u.DeletedBy).HasMaxLength(100);
        builder.Ignore(u => u.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(u => u.Status).HasDatabaseName("IX_UploadHistories_Status");
        builder.HasIndex(u => u.StartedOnUtc).HasDatabaseName("IX_UploadHistories_StartedOn");

        builder.HasOne(u => u.StatementFile).WithMany().HasForeignKey(u => u.StatementFileId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(u => u.Statement).WithMany().HasForeignKey(u => u.StatementId).OnDelete(DeleteBehavior.SetNull);
    }
}
