using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Observability;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Observability")]
public sealed class OtlpExportTests
{
    private static IConfiguration Config(string? endpoint, string? profile = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint, ["MEEPLEAI_PROFILE"] = profile })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsEnabled_AbsentOrBlank_IsFalse(string? endpoint)
    {
        OtlpExport.IsEnabled(Config(endpoint)).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithEndpoint_IsTrueAndParsesUri()
    {
        var config = Config("http://aspire-dashboard:18889");

        OtlpExport.IsEnabled(config).Should().BeTrue();
        OtlpExport.Endpoint(config).Should().Be(new Uri("http://aspire-dashboard:18889"));
    }

    [Fact]
    public void Endpoint_Invalid_Throws()
    {
        var act = () => OtlpExport.Endpoint(Config("nope"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*OTEL_EXPORTER_OTLP_ENDPOINT*");
    }

    [Fact]
    public void ProfileName_DefaultsToDefault()
    {
        OtlpExport.ProfileName(Config(null)).Should().Be("default");
        OtlpExport.ProfileName(Config(null, "azure-local")).Should().Be("azure-local");
    }
}
