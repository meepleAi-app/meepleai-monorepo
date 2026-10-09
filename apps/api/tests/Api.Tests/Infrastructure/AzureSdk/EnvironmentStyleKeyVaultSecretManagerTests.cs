using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class EnvironmentStyleKeyVaultSecretManagerTests
{
    [Theory]
    [InlineData("OPENROUTER-API-KEY", "OPENROUTER_API_KEY")]
    [InlineData("Embedding--Provider", "Embedding:Provider")]
    [InlineData("Seq--ApiKey", "Seq:ApiKey")]
    [InlineData("plain", "plain")]
    public void GetKey_MapsDoubleDashToSectionAndSingleDashToUnderscore(string secretName, string expectedKey)
    {
        // Convenzione (spec §4.2): `--` separa le sezioni, `-` sta per `_` nei nomi in stile variabile
        // d'ambiente. Una chiave CamelCase con trattino interno (es. Api-Key) NON è esprimibile: usare il
        // nome CamelCase senza trattini (Seq--ApiKey).
        var sut = new EnvironmentStyleKeyVaultSecretManager();

        sut.GetKey(new KeyVaultSecret(secretName, "v")).Should().Be(expectedKey);
    }
}
