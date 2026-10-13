using PtlOrchestrator.Contracts.Security;

namespace PtlOrchestrator.Contracts.Tests.Security;

public class PinHasherTests
{
    [Fact]
    public void Verify_WithTheOriginalPin_Succeeds()
    {
        var hash = PinHasher.Hash("4821");

        Assert.True(PinHasher.Verify("4821", hash));
    }

    [Fact]
    public void Verify_WithAnotherPin_Fails()
    {
        var hash = PinHasher.Hash("4821");

        Assert.False(PinHasher.Verify("4822", hash));
    }

    [Fact]
    public void Hash_OfSamePinTwice_ProducesDifferentHashesBecauseOfSalt()
    {
        Assert.NotEqual(PinHasher.Hash("4821"), PinHasher.Hash("4821"));
    }

    [Fact]
    public void Hash_DoesNotContainThePinInClear()
    {
        Assert.DoesNotContain("4821", PinHasher.Hash("4821"));
    }

    [Fact]
    public void Hash_UsesTheDocumentedFormat()
    {
        var parts = PinHasher.Hash("4821").Split('.');

        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.True(int.Parse(parts[1]) > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Hash_WithBlankPin_Throws(string? pin)
    {
        Assert.ThrowsAny<ArgumentException>(() => PinHasher.Hash(pin!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v2.1.AAAA.AAAA")]
    [InlineData("v1.abc.AAAA.AAAA")]
    [InlineData("v1.100000.%%%.%%%")]
    [InlineData("v1.100000.AAAA.AAAA")]
    public void Verify_WithMalformedStoredHash_ReturnsFalseInsteadOfThrowing(string? stored)
    {
        Assert.False(PinHasher.Verify("4821", stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Verify_WithBlankPin_ReturnsFalse(string pin)
    {
        var hash = PinHasher.Hash("4821");

        Assert.False(PinHasher.Verify(pin, hash));
    }
}
