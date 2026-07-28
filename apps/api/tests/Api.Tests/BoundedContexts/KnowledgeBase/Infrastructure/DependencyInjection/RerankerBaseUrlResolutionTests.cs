using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;

/// <summary>
/// Guards <see cref="KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl"/> — the seam that decides
/// which URL the cross-encoder reranker HttpClient targets.
/// <para>
/// Regression context: the DI wiring read only <c>Reranking:BaseUrl</c> and fell back to
/// <c>localhost:8003</c>. Every deployed compose file sets <c>RERANKER_URL</c> (not
/// <c>Reranking:BaseUrl</c>), so in staging/prod the client silently targeted itself, reranking failed,
/// and every RAG query degraded to the vector-dominated fusion fallback. The <c>RERANKER_URL</c>
/// fallback restores reachability while keeping <c>Reranking:BaseUrl</c> as an explicit override.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RerankerBaseUrlResolutionTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal);
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void ResolveRerankerBaseUrl_UsesRerankerUrlEnvVar_WhenBaseUrlKeyAbsent()
    {
        // The staging/prod shape: only RERANKER_URL is set (as compose.{staging,prod}.yml do).
        var config = Config(("RERANKER_URL", "http://reranker-service:8003"));

        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config)
            .Should().Be("http://reranker-service:8003");
    }

    [Fact]
    public void ResolveRerankerBaseUrl_PrefersExplicitBaseUrlKey_OverRerankerUrl()
    {
        var config = Config(
            ("Reranking:BaseUrl", "http://explicit-override:9000"),
            ("RERANKER_URL", "http://reranker-service:8003"));

        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config)
            .Should().Be("http://explicit-override:9000");
    }

    [Fact]
    public void ResolveRerankerBaseUrl_FallsBackToLocalhost_WhenNeitherSet()
    {
        var config = Config();

        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config)
            .Should().Be("http://localhost:8003");
    }

    [Fact]
    public void ResolveRerankerBaseUrl_ReturnsParsableUri()
    {
        // The DI lambda feeds the result straight into new Uri(...); a malformed value would throw at
        // container-build time. Assert the resolved value round-trips through Uri for each source.
        foreach (var config in new[]
                 {
                     Config(("RERANKER_URL", "http://reranker-service:8003")),
                     Config(("Reranking:BaseUrl", "http://explicit-override:9000")),
                     Config(),
                 })
        {
            var act = () => new Uri(KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config));
            act.Should().NotThrow();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRerankerBaseUrl_TreatsBlankBaseUrlAsAbsent_FallsThroughToRerankerUrl(string blank)
    {
        // A present-but-blank Reranking:BaseUrl (e.g. env override Reranking__BaseUrl=) must NOT be
        // treated as an explicit value — otherwise new Uri("") throws at container build. It falls
        // through to RERANKER_URL.
        var config = Config(
            ("Reranking:BaseUrl", blank),
            ("RERANKER_URL", "http://reranker-service:8003"));

        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config)
            .Should().Be("http://reranker-service:8003");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRerankerBaseUrl_TreatsBlankRerankerUrlAsAbsent_FallsBackToLocalhost(string blank)
    {
        // Blanking RERANKER_URL (a common "disable via empty value" pattern) must fall back to
        // localhost, not produce new Uri("").
        var config = Config(("RERANKER_URL", blank));

        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(config)
            .Should().Be("http://localhost:8003");
    }

    [Fact]
    public void NoAppsettingsFile_DefinesRerankingBaseUrl_SoRerankerUrlFallbackFiresInEveryEnvironment()
    {
        // Regression guard for #3334: the original fix was defeated because appsettings.json defined
        // Reranking:BaseUrl="http://localhost:8003", which is loaded in every environment and shadowed
        // the RERANKER_URL fallback (config["Reranking:BaseUrl"] was never null → the ?? branch was dead
        // code → the reranker kept targeting localhost). A sweep also found the SAME shadow in
        // appsettings.Integration.json. This guard scans ALL appsettings*.json shipped next to the Api
        // assembly and asserts none reintroduces a shadowing Reranking:BaseUrl, in any environment.
        var appsettingsFiles = Directory
            .GetFiles(AppContext.BaseDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly);

        // Api's appsettings*.json are copied next to Api.dll and flow into the test output. If the build
        // layout ever changes, fail loudly rather than silently skip — this guard is the whole point.
        appsettingsFiles.Should().NotBeEmpty(
            $"appsettings*.json must be present in {AppContext.BaseDirectory}; the regression guard cannot run without them.");

        foreach (var file in appsettingsFiles)
        {
            var config = new ConfigurationBuilder().AddJsonFile(file, optional: false).Build();
            string.IsNullOrWhiteSpace(config["Reranking:BaseUrl"]).Should().BeTrue(
                $"{Path.GetFileName(file)} must not define Reranking:BaseUrl — a non-blank value shadows the "
                + "RERANKER_URL env fallback in that environment (#3334).");
        }

        // End-to-end: the base appsettings.json layered under a RERANKER_URL env value must resolve to
        // the env value, exactly as staging/prod do.
        var layered = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["RERANKER_URL"] = "http://reranker-service:8003",
            })
            .Build();
        KnowledgeBaseServiceExtensions.ResolveRerankerBaseUrl(layered)
            .Should().Be("http://reranker-service:8003");
    }
}
