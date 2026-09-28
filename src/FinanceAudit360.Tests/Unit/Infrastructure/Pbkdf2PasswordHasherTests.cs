using FinanceAudit360.Infrastructure.Identity;

namespace FinanceAudit360.Tests.Unit.Infrastructure;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ProducesTheDocumentedFormat()
    {
        var hash = _hasher.Hash("Correct#Horse9");
        var parts = hash.Split('.');

        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 210_000);
    }

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently() =>
        Assert.NotEqual(_hasher.Hash("Correct#Horse9"), _hasher.Hash("Correct#Horse9"));

    [Fact]
    public void Verify_AcceptsTheCorrectPassword() =>
        Assert.True(_hasher.Verify("Correct#Horse9", _hasher.Hash("Correct#Horse9")));

    [Fact]
    public void Verify_RejectsTheWrongPassword() =>
        Assert.False(_hasher.Verify("Wrong#Horse9", _hasher.Hash("Correct#Horse9")));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v1.notanumber.abc.def")]
    [InlineData("v2.210000.YWJj.ZGVm")]
    public void Verify_RejectsMalformedHashesWithoutThrowing(string hash) =>
        Assert.False(_hasher.Verify("anything", hash));

    [Fact]
    public void NeedsRehash_IsFalseForACurrentHash() =>
        Assert.False(_hasher.NeedsRehash(_hasher.Hash("Correct#Horse9")));

    [Fact]
    public void NeedsRehash_IsTrueForAWeakerIterationCount() =>
        Assert.True(_hasher.NeedsRehash("v1.1000.YWJjZGVmZ2hpamtsbW5vcA==.ZGVmZ2hpamtsbW5vcHFyc3R1dg=="));
}
