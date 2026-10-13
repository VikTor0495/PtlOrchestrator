using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PtlOrchestrator.Api.Security;

namespace PtlOrchestrator.Api.Tests;

/// <summary>
/// Verifica la validazione delle chiavi senza avviare un host: così l'esito non dipende
/// dal modo in cui un avvio fallito viene distrutto.
/// </summary>
public class ApiKeyOptionsValidationTests
{
    private const string ValidStationKey = "station-key-0123456789";
    private const string ValidAdminKey = "admin-key-0123456789ab";

    private static ApiKeyOptions ResolveOptions(string? stationKey, string? adminKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:StationKey"] = stationKey,
                ["ApiKeys:AdminKey"] = adminKey
            })
            .Build();

        var services = new ServiceCollection().AddApiKeySecurity(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
    }

    [Fact]
    public void ValidKeys_AreAccepted()
    {
        var options = ResolveOptions(ValidStationKey, ValidAdminKey);

        Assert.Equal(ValidStationKey, options.StationKey);
        Assert.Equal(ValidAdminKey, options.AdminKey);
    }

    [Theory]
    [InlineData(null, ValidAdminKey, "StationKey")]
    [InlineData("", ValidAdminKey, "StationKey")]
    [InlineData("troppo-corta", ValidAdminKey, "StationKey")]
    [InlineData(ValidStationKey, null, "AdminKey")]
    [InlineData(ValidStationKey, "", "AdminKey")]
    [InlineData(ValidStationKey, "troppo-corta", "AdminKey")]
    public void MissingOrShortKeys_AreRejected(string? stationKey, string? adminKey, string expectedKeyInMessage)
    {
        var exception = Assert.Throws<OptionsValidationException>(() => ResolveOptions(stationKey, adminKey));

        Assert.Contains(expectedKeyInMessage, exception.Message);
    }

    [Fact]
    public void IdenticalKeys_AreRejected()
    {
        var exception = Assert.Throws<OptionsValidationException>(
            () => ResolveOptions(ValidStationKey, ValidStationKey));

        Assert.Contains("diverse", exception.Message);
    }
}
