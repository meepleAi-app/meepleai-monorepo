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
}
