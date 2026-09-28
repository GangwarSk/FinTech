using FinanceAudit360.Application.Features.Statements;

namespace FinanceAudit360.Tests.Unit.Domain;

public class StatementFileLabelTests
{
    [Fact]
    public void Build_CombinesBankFileNameAndPeriod()
    {
        var label = StatementFileLabel.Build(
            "ICICI Bank",
            "ICICI-Aug2025.pdf",
            new DateTime(2025, 8, 1),
            new DateTime(2025, 8, 31),
            new DateTime(2026, 9, 27));

        Assert.Equal("ICICI Bank - ICICI-Aug2025.pdf (01 Aug 2025 - 31 Aug 2025)", label);
    }

    [Fact]
    public void Build_FallsBackToTheUploadDateWhenNoStatementWasProduced()
    {
        // A failed or password-locked upload has no period, but still has to be identifiable in the bin.
        var label = StatementFileLabel.Build(
            "ICICI Bank",
            "ICICI-Aug2025.pdf",
            null,
            null,
            new DateTime(2026, 9, 27));

        Assert.Equal("ICICI Bank - ICICI-Aug2025.pdf (uploaded 27 Sep 2026)", label);
    }

    [Fact]
    public void Build_OmitsThePrefixWhenTheBankIsUnknown()
    {
        var label = StatementFileLabel.Build(
            null,
            "mystery.pdf",
            new DateTime(2025, 8, 1),
            new DateTime(2025, 8, 31),
            new DateTime(2026, 9, 27));

        Assert.Equal("mystery.pdf (01 Aug 2025 - 31 Aug 2025)", label);
    }

    [Fact]
    public void Build_DistinguishesTwoUploadsOfTheSameFileName()
    {
        var august = StatementFileLabel.Build("ICICI Bank", "statement.pdf", new DateTime(2025, 8, 1), new DateTime(2025, 8, 31), default);
        var september = StatementFileLabel.Build("ICICI Bank", "statement.pdf", new DateTime(2025, 9, 1), new DateTime(2025, 9, 30), default);

        Assert.NotEqual(august, september);
    }
}
