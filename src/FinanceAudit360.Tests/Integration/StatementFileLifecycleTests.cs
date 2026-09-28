using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Features.Statements;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FinanceAudit360.Tests.Integration;

/// <summary>
/// Covers the uploaded-file lifecycle: soft delete cascades the whole import chain, restore refuses when a
/// newer import has claimed the dedupe keys, and purge clears every table plus the stored PDF.
/// </summary>
public class StatementFileLifecycleTests : IAsyncLifetime
{
    private DatabaseFixture _fixture = null!;
    private UnitOfWork _unitOfWork = null!;
    private Mock<IDateTimeProvider> _dateTime = null!;
    private Mock<IFileStorage> _fileStorage = null!;
    private Guid _bankId;
    private Guid _cardId;

    public async ValueTask InitializeAsync()
    {
        _fixture = new DatabaseFixture();
        _unitOfWork = new UnitOfWork(_fixture.Context, new Mock<IServiceProvider>().Object);

        _dateTime = new Mock<IDateTimeProvider>();
        _dateTime.SetupGet(d => d.UtcNow).Returns(() => DateTime.UtcNow);

        _fileStorage = new Mock<IFileStorage>();
        _fileStorage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (bank, card, _) = await _fixture.SeedMastersAsync();
        _bankId = bank.Id;
        _cardId = card.Id;
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task Delete_SoftDeletesTheWholeImportChain()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));

        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        // The global query filter hides every row without any caller remembering to ask.
        Assert.Null(await _fixture.Context.StatementFiles.FirstOrDefaultAsync(f => f.Id == imported.File.Id));
        Assert.Empty(await _fixture.Context.Statements.Where(s => s.StatementFileId == imported.File.Id).ToListAsync());
        Assert.Empty(await _fixture.Context.Transactions.Where(t => t.StatementId == imported.Statement.Id).ToListAsync());
        Assert.Empty(await _fixture.Context.UploadHistories.Where(u => u.StatementFileId == imported.File.Id).ToListAsync());

        // ...but the rows are still there, flagged, so they can be reviewed and restored.
        var file = await _fixture.Context.StatementFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == imported.File.Id);
        Assert.True(file.IsDeleted);
        Assert.Equal("tester", file.DeletedBy);
        Assert.NotNull(file.DeletedOnUtc);

        var transactions = await _fixture.Context.Transactions.IgnoreQueryFilters()
            .Where(t => t.StatementId == imported.Statement.Id)
            .ToListAsync();

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.True(t.IsDeleted));
    }

    [Fact]
    public async Task Delete_LeavesOtherFilesUntouched()
    {
        var target = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        var other = await SeedAsync("ICICI-Sep2025.pdf", new DateTime(2025, 9, 1), new DateTime(2025, 9, 30));

        await Delete(target.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        Assert.NotNull(await _fixture.Context.StatementFiles.FirstOrDefaultAsync(f => f.Id == other.File.Id));
        Assert.Equal(2, await _fixture.Context.Transactions.CountAsync(t => t.StatementId == other.Statement.Id));
    }

    [Fact]
    public async Task Delete_OnAnAlreadyDeletedFile_IsRejected()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Delete(imported.File.Id));

        Assert.Equal("statementfile.already_deleted", exception.Code);
    }

    [Fact]
    public async Task Restore_BringsBackTheWholeChain()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        await Restore(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        Assert.NotNull(await _fixture.Context.StatementFiles.FirstOrDefaultAsync(f => f.Id == imported.File.Id));
        Assert.Equal(1, await _fixture.Context.Statements.CountAsync(s => s.StatementFileId == imported.File.Id));
        Assert.Equal(2, await _fixture.Context.Transactions.CountAsync(t => t.StatementId == imported.Statement.Id));
        Assert.Equal(1, await _fixture.Context.UploadHistories.CountAsync(u => u.StatementFileId == imported.File.Id));
    }

    [Fact]
    public async Task Restore_IsRefusedWhenAnotherImportClaimedTheDedupeKey()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        // The same statement period is imported again from a different PDF while the first is in the bin.
        // Its dedupe key is unique only among live rows, so restoring would now violate that index.
        await SeedAsync("ICICI-Aug2025-corrected.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        _fixture.Context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Restore(imported.File.Id));

        Assert.Equal("statementfile.restore_conflict", exception.Code);
        Assert.Contains("already been imported again", exception.Message);

        // The refusal must leave the recycled file exactly as it was.
        var file = await _fixture.Context.StatementFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == imported.File.Id);
        Assert.True(file.IsDeleted);
    }

    [Fact]
    public async Task Purge_RemovesEveryRowAndTheStoredPdf()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        var relativePath = imported.File.RelativePath;

        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        await Purge(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        Assert.Empty(await _fixture.Context.StatementFiles.IgnoreQueryFilters()
            .Where(f => f.Id == imported.File.Id).ToListAsync());
        Assert.Empty(await _fixture.Context.Statements.IgnoreQueryFilters()
            .Where(s => s.StatementFileId == imported.File.Id).ToListAsync());
        Assert.Empty(await _fixture.Context.Transactions.IgnoreQueryFilters()
            .Where(t => t.StatementId == imported.Statement.Id).ToListAsync());
        Assert.Empty(await _fixture.Context.UploadHistories.IgnoreQueryFilters()
            .Where(u => u.StatementFileId == imported.File.Id).ToListAsync());

        _fileStorage.Verify(s => s.DeleteAsync(relativePath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Purge_ClearsLedgerReferencesRatherThanDeletingTheLedgerEntry()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));

        // A ledger entry can point at an imported transaction; the person's money record must survive the
        // purge with the reference cleared, because it is theirs and not the statement's.
        var person = Person.Create("Rahul");
        var entry = person.RecordEntry(
            new DateTime(2025, 8, 5),
            100m,
            LedgerEntryType.Given,
            "Split of a card purchase",
            imported.Transactions[0].Id);

        await _fixture.Context.Persons.AddAsync(person);
        await _fixture.Context.SaveChangesAsync();

        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();
        await Purge(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        var survivor = await _fixture.Context.MoneyLedgers.IgnoreQueryFilters().SingleAsync(l => l.Id == entry.Id);

        Assert.Null(survivor.TransactionId);
        Assert.Equal(100m, survivor.Amount);
    }

    [Fact]
    public async Task Purge_IsRefusedForAFileThatIsNotInTheRecycleBin()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Purge(imported.File.Id));

        Assert.Equal("statementfile.not_deleted", exception.Code);
        _fileStorage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecycleBinSearch_ReportsWhatTheDeletedFileStillHolds()
    {
        var imported = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31), 3);
        await SeedAsync("ICICI-Sep2025.pdf", new DateTime(2025, 9, 1), new DateTime(2025, 9, 30));

        await Delete(imported.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        var repository = new StatementFileRepository(_fixture.Context);
        var bin = await repository.SearchAsync(new StatementFileFilterRequest { DeletedOnly = true });

        var row = Assert.Single(bin.Items);
        Assert.Equal(imported.File.Id, row.File.Id);
        Assert.Equal(1, row.StatementCount);
        Assert.Equal(3, row.TransactionCount);
        Assert.Equal(new DateTime(2025, 8, 1), row.PeriodStart);
        Assert.Equal("HDFC Bank", row.BankName);

        // The live listing must not show it, and must still show the other file.
        var active = await repository.SearchAsync(new StatementFileFilterRequest());
        Assert.Single(active.Items);
        Assert.Equal("ICICI-Sep2025.pdf", active.Items[0].File.OriginalFileName);
    }

    [Fact]
    public async Task Lookup_OffersOnlyLiveFilesForTheChosenBank()
    {
        var deleted = await SeedAsync("ICICI-Aug2025.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31));
        await SeedAsync("ICICI-Sep2025.pdf", new DateTime(2025, 9, 1), new DateTime(2025, 9, 30));

        await Delete(deleted.File.Id);
        _fixture.Context.ChangeTracker.Clear();

        var repository = new StatementFileRepository(_fixture.Context);
        var options = await repository.GetLookupAsync(_bankId, null);

        var option = Assert.Single(options);
        Assert.Equal("ICICI-Sep2025.pdf", option.OriginalFileName);
        Assert.Contains("HDFC Bank", option.Label);
        Assert.Contains("01 Sep 2025 - 30 Sep 2025", option.Label);
        Assert.Equal(2, option.TransactionCount);

        Assert.Empty(await repository.GetLookupAsync(_bankId, 2024));
    }

    private Task<ImportedFile> SeedAsync(string fileName, DateTime from, DateTime to, int transactionCount = 2) =>
        _fixture.SeedImportedFileAsync(_bankId, _cardId, fileName, from, to, transactionCount);

    private Task Delete(Guid fileId) =>
        new DeleteStatementFileCommandHandler(
                _fixture.Context,
                _unitOfWork,
                _fixture.CurrentUser.Object,
                _dateTime.Object)
            .Handle(new DeleteStatementFileCommand(fileId), CancellationToken.None);

    private Task Restore(Guid fileId) =>
        new RestoreStatementFileCommandHandler(_fixture.Context, _unitOfWork)
            .Handle(new RestoreStatementFileCommand(fileId), CancellationToken.None);

    private Task Purge(Guid fileId) =>
        new PurgeStatementFileCommandHandler(_fixture.Context, _unitOfWork, _fileStorage.Object)
            .Handle(new PurgeStatementFileCommand(fileId), CancellationToken.None);
}
