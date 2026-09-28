using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Persistence;
using FinanceAudit360.Persistence.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceAudit360.Tests.Integration;

/// <summary>
/// Spins up a real relational database (SQLite in-memory) per test so repository queries are
/// exercised against an actual provider rather than LINQ-to-Objects.
/// </summary>
public sealed class DatabaseFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        CurrentUser = new Mock<ICurrentUser>();
        CurrentUser.SetupGet(u => u.UserName).Returns("tester");
        CurrentUser.SetupGet(u => u.UserId).Returns(Guid.CreateVersion7());

        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.SetupGet(d => d.UtcNow).Returns(() => DateTime.UtcNow);

        var publisher = new Mock<IPublisher>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(
                new AuditableEntityInterceptor(CurrentUser.Object, dateTime.Object),
                new DomainEventDispatchInterceptor(publisher.Object))
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;

        Context = new TestApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public ApplicationDbContext Context { get; }

    public Mock<ICurrentUser> CurrentUser { get; }

    public async Task<(Bank Bank, CreditCard Card, BankAccount Account)> SeedMastersAsync()
    {
        var bank = Bank.Create("HDFC Bank", "HDFC", BankCode.Hdfc, "HDFC BANK");
        await Context.Banks.AddAsync(bank);
        await Context.SaveChangesAsync();

        var card = CreditCard.Create(bank.Id, "4111111111114321", "Rajesh Sharma", CardNetwork.Visa, "HDFC Regalia", "Regalia", 300_000m);
        var account = BankAccount.Create(bank.Id, "50100123456789", "Rajesh Sharma", AccountType.Savings, "HDFC Savings");

        await Context.CreditCards.AddAsync(card);
        await Context.BankAccounts.AddAsync(account);
        await Context.SaveChangesAsync();

        return (bank, card, account);
    }

    /// <summary>
    /// Builds a complete import chain - stored PDF, upload history, statement and transactions - so the
    /// file-lifecycle tests operate on the same shape the importer produces.
    /// </summary>
    public async Task<ImportedFile> SeedImportedFileAsync(
        Guid bankId,
        Guid creditCardId,
        string fileName,
        DateTime periodStart,
        DateTime periodEnd,
        int transactionCount = 2,
        string? contentHash = null)
    {
        var file = StatementFile.Create(
            fileName,
            $"{Guid.CreateVersion7():N}.pdf",
            $"statements/{Guid.CreateVersion7():N}.pdf",
            1024,
            contentHash ?? Guid.CreateVersion7().ToString("N"));

        file.SetDetection(BankCode.Hdfc, StatementKind.CreditCard);
        await Context.StatementFiles.AddAsync(file);
        await Context.SaveChangesAsync();

        var statement = CreditCardStatement.CreateForCreditCard(
            bankId,
            creditCardId,
            MaskedCardNumber.Create("4111111111114321"),
            DateRange.Create(periodStart, periodEnd));

        statement.SetSource(file.Id, "HdfcCreditCardParser", "ST-001", "INR");

        var transactions = new List<Transaction>();
        for (var i = 0; i < transactionCount; i++)
        {
            var transaction = Transaction.Create(
                periodStart.AddDays(i + 1),
                100m * (i + 1),
                TransactionDirection.Debit,
                TransactionType.Debit,
                $"{fileName} PURCHASE {i + 1}");

            transaction.SetInstrument(bankId, null, creditCardId);

            // Going through the aggregate, as the importer does, is what keeps the statement's own
            // TransactionCount and totals in step with the rows.
            statement.AddTransaction(transaction);
            transactions.Add(transaction);
        }

        statement.MarkImported();

        await Context.Statements.AddAsync(statement);
        await Context.SaveChangesAsync();

        var upload = UploadHistory.Start(fileName, 1024);
        upload.AttachFile(file.Id);
        upload.MarkProcessing(BankCode.Hdfc, StatementKind.CreditCard, "HdfcCreditCardParser");
        upload.MarkCompleted(statement.Id, transactionCount, 0);
        await Context.UploadHistories.AddAsync(upload);

        await Context.SaveChangesAsync();

        return new ImportedFile(file, statement, transactions, upload);
    }

    public async Task<Transaction> AddTransactionAsync(        DateTime date,
        decimal amount,
        TransactionDirection direction,
        string description,
        Guid? bankId = null,
        Guid? cardId = null,
        Guid? accountId = null,
        Guid? vendorId = null,
        Guid? personId = null,
        Guid? categoryId = null,
        TransactionType type = TransactionType.Debit)
    {
        var transaction = Transaction.Create(date, amount, direction, type, description);
        transaction.SetInstrument(bankId, accountId, cardId);
        transaction.AssignVendor(vendorId);
        transaction.AssignPerson(personId);
        transaction.AssignCategory(categoryId);

        await Context.Transactions.AddAsync(transaction);
        await Context.SaveChangesAsync();

        return transaction;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed record ImportedFile(
    StatementFile File,
    CreditCardStatement Statement,
    IReadOnlyList<Transaction> Transactions,
    UploadHistory Upload);

/// <summary>
/// The production model maps a "Version" shadow property with <c>IsRowVersion</c>, which Npgsql resolves to
/// PostgreSQL's system <c>xmin</c> column. SQLite has no equivalent, so it would be created as a real
/// NOT NULL column that nothing ever writes and every insert would fail on. Dropping the property is the
/// only difference between the tested model and the production one.
/// </summary>
internal sealed class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindProperty("Version") is { } version)
            {
                entityType.RemoveProperty(version);
            }
        }
    }
}
