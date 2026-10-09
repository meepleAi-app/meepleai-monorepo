using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AzureStorageConnectionStringTests
{
    [Fact]
    public void TryParse_DevelopmentStorage_ReturnsWellKnownAccount()
    {
        AzureStorageConnectionString.TryParse("UseDevelopmentStorage=true", out var name, out var key).Should().BeTrue();

        name.Should().Be("devstoreaccount1");
        key.Should().Be(AzureStorageConnectionString.DevelopmentStorageKey);
    }

    [Fact]
    public void TryParse_ExplicitPairs_ReturnsThem()
    {
        var cs = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=a2V5;BlobEndpoint=http://azurite:10000/devstoreaccount1;";

        AzureStorageConnectionString.TryParse(cs, out var name, out var key).Should().BeTrue();

        name.Should().Be("devstoreaccount1");
        key.Should().Be("a2V5");
    }

    [Theory]
    [InlineData("")]
    [InlineData("BlobEndpoint=http://x;")]
    [InlineData("AccountName=only;")]
    public void TryParse_MissingPairs_IsFalse(string cs)
    {
        AzureStorageConnectionString.TryParse(cs, out _, out _).Should().BeFalse();
    }
}
