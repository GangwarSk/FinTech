using FinanceAudit360.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceAudit360.Persistence.Configurations;

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Banks");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.ShortName).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Code).HasConversion<int>();
        builder.Property(b => b.Ifsc).HasMaxLength(20);
        builder.Property(b => b.LogoUrl).HasMaxLength(500);
        builder.Property(b => b.Website).HasMaxLength(300);
        builder.Property(b => b.StatementKeywords).HasMaxLength(1000);
        builder.Property(b => b.CreatedBy).HasMaxLength(100);
        builder.Property(b => b.ModifiedBy).HasMaxLength(100);
        builder.Property(b => b.DeletedBy).HasMaxLength(100);
        builder.Ignore(b => b.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(b => b.Name).IsUnique().HasFilter("\"IsDeleted\" = false").HasDatabaseName("UX_Banks_Name");
        builder.HasIndex(b => b.Code).HasDatabaseName("IX_Banks_Code");

        builder.HasMany(b => b.Accounts).WithOne(a => a.Bank).HasForeignKey(a => a.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(b => b.CreditCards).WithOne(c => c.Bank).HasForeignKey(c => c.BankId).OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(b => b.Accounts).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(b => b.CreditCards).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountNumberMasked).HasMaxLength(50).IsRequired();
        builder.Property(a => a.AccountNumberLast4).HasMaxLength(4).IsRequired();
        builder.Property(a => a.AccountHolderName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.AccountType).HasConversion<int>();
        builder.Property(a => a.Nickname).HasMaxLength(100);
        builder.Property(a => a.Ifsc).HasMaxLength(20);
        builder.Property(a => a.BranchName).HasMaxLength(200);
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.CreatedBy).HasMaxLength(100);
        builder.Property(a => a.ModifiedBy).HasMaxLength(100);
        builder.Property(a => a.DeletedBy).HasMaxLength(100);
        builder.Ignore(a => a.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(a => a.DisplayName);

        builder.HasIndex(a => new { a.BankId, a.AccountNumberLast4 })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("UX_BankAccounts_Bank_Last4");

        builder.HasIndex(a => a.IsActive).HasDatabaseName("IX_BankAccounts_IsActive");

        builder.HasMany(a => a.Transactions).WithOne(t => t.BankAccount).HasForeignKey(t => t.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("CreditCards");
        builder.HasKey(c => c.Id);

        builder.OwnsOne(c => c.CardNumber, owned =>
        {
            owned.Property(n => n.Masked).HasColumnName("CardNumberMasked").HasMaxLength(25).IsRequired();
            owned.Property(n => n.Last4).HasColumnName("CardNumberLast4").HasMaxLength(4).IsRequired();
            owned.HasIndex(n => n.Last4).HasDatabaseName("IX_CreditCards_Last4");
        });

        builder.Navigation(c => c.CardNumber).IsRequired();

        builder.Property(c => c.CardHolderName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Nickname).HasMaxLength(100);
        builder.Property(c => c.ProductName).HasMaxLength(150);
        builder.Property(c => c.Network).HasConversion<int>();
        builder.Property(c => c.Currency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.Property(c => c.CreatedBy).HasMaxLength(100);
        builder.Property(c => c.ModifiedBy).HasMaxLength(100);
        builder.Property(c => c.DeletedBy).HasMaxLength(100);
        builder.Ignore(c => c.RowVersion);
        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(c => c.DisplayName);
        builder.Ignore(c => c.AvailableLimit);

        builder.HasIndex(c => c.IsActive).HasDatabaseName("IX_CreditCards_IsActive");

        builder.HasMany(c => c.Statements).WithOne(s => s.CreditCard).HasForeignKey(s => s.CreditCardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Transactions).WithOne(t => t.CreditCard).HasForeignKey(t => t.CreditCardId).OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Statements).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
