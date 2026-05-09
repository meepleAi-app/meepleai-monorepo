using Api.Infrastructure.Health.Extensions;
using Api.Infrastructure.Health.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Infrastructure.Health.Extensions;

/// <summary>
/// Verifies that AddComprehensiveHealthChecks registers optional providers conditionally.
/// Goal: avoid global /health Degraded status when an optional provider is intentionally
/// not deployed (Provider=Docnet, OLLAMA_URL missing). Conditional registration replaces
/// the previous "self-skip with Degraded" anti-pattern that polluted the aggregate signal.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class HealthCheckServiceExtensionsTests
{
    private static ICollection<HealthCheckRegistration> RegisterAndCollect(
        Dictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient();
        services.AddLogging();

        services.AddHealthChecks().AddComprehensiveHealthChecks(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        return options.Registrations;
    }

    [Theory]
    [InlineData("Orchestrator", true, true)]
    [InlineData("orchestrator", true, true)] // case-insensitive
    [InlineData("Unstructured", true, false)]
    [InlineData("SmolDocling", false, true)]
    [InlineData("Docnet", false, false)]
    [InlineData("Unknown", false, false)]
    public void PdfProvider_Selects_Which_Extractor_Health_Checks_Are_Registered(
        string provider,
        bool expectUnstructured,
        bool expectSmolDocling)
    {
        var registrations = RegisterAndCollect(new()
        {
            ["PdfProcessing:Extractor:Provider"] = provider
        });

        var names = registrations.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        names.Contains("unstructured").Should().Be(expectUnstructured,
            because: $"Provider={provider} should {(expectUnstructured ? "" : "NOT ")}register the Unstructured check");
        names.Contains("smoldocling").Should().Be(expectSmolDocling,
            because: $"Provider={provider} should {(expectSmolDocling ? "" : "NOT ")}register the SmolDocling check");
    }

    [Fact]
    public void PdfProvider_Defaults_To_Orchestrator_When_Unset()
    {
        var registrations = RegisterAndCollect(new());

        registrations.Should().Contain(r => r.Name == "unstructured");
        registrations.Should().Contain(r => r.Name == "smoldocling");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("http://ollama:11434", true)]
    public void Ollama_Is_Registered_Only_When_Url_Is_Set(string? url, bool expected)
    {
        var registrations = RegisterAndCollect(new()
        {
            ["OLLAMA_URL"] = url,
            ["PdfProcessing:Extractor:Provider"] = "Docnet" // isolate Ollama from PDF provider noise
        });

        registrations.Any(r => r.Name == "ollama").Should().Be(expected);
    }

    [Fact]
    public void Optional_Tag_Is_Applied_To_Conditionally_Registered_Checks()
    {
        var registrations = RegisterAndCollect(new()
        {
            ["PdfProcessing:Extractor:Provider"] = "Orchestrator",
            ["OLLAMA_URL"] = "http://ollama:11434"
        });

        var unstructured = registrations.Single(r => r.Name == "unstructured");
        var smoldocling = registrations.Single(r => r.Name == "smoldocling");
        var ollama = registrations.Single(r => r.Name == "ollama");

        unstructured.Tags.Should().Contain(HealthCheckTags.Optional);
        smoldocling.Tags.Should().Contain(HealthCheckTags.Optional);
        ollama.Tags.Should().Contain(HealthCheckTags.Optional);
    }

    [Fact]
    public void Always_On_Checks_Are_Registered_Regardless_Of_Optional_Provider_Config()
    {
        // With every optional provider disabled, the always-on AI checks must still be present.
        var registrations = RegisterAndCollect(new()
        {
            ["PdfProcessing:Extractor:Provider"] = "Docnet",
            ["OLLAMA_URL"] = ""
        });

        var names = registrations.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        names.Should().Contain(new[]
        {
            "openrouter", "embedding", "embedding-dimensions", "reranker", "orchestrator",
            "bggapi", "oauth", "smtp", "grafana", "prometheus", "redis-rate-limiting",
            "s3storage", "slack_api", "slack_queue"
        });
        names.Should().NotContain(new[] { "unstructured", "smoldocling", "ollama" });
    }
}
