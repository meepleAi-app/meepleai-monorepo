using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// e5 instruction prefix on the wire (issue #3737).
/// </summary>
/// <remarks>
/// The embedding service applies <c>"query: "</c> or <c>"passage: "</c> according to the
/// <c>purpose</c> field of the request body, and both sides go through this one provider.
/// These tests assert on the serialised body, because that field is the entire contract: a
/// purpose that never leaves the process changes nothing.
/// </remarks>
public sealed class HttpEmbeddingProviderPurposeTests
{
    // EmbeddingPurpose is internal, so it cannot appear in a public [Theory] signature
    // (CS0051). One [Fact] per side instead — which also names each expectation.

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithQueryPurpose_SendsQuery()
    {
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(
            new[] { "how do I set up Catan?" }, "en", EmbeddingPurpose.Query);

        Assert.Equal("query", capture.Purpose);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithPassagePurpose_SendsPassage()
    {
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(
            new[] { "Setup: place the hexes." }, "en", EmbeddingPurpose.Passage);

        Assert.Equal("passage", capture.Purpose);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithoutPurpose_SendsPassage()
    {
        // The indexing path uses this overload and must keep the pre-#3737 behaviour, so no
        // already-indexed chunk is invalidated by the change.
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(new[] { "Setup: place the hexes." });

        Assert.Equal("passage", capture.Purpose);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithLanguageButNoPurpose_SendsPassage()
    {
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(new[] { "Preparazione: disponi gli esagoni." }, "it");

        Assert.Equal("passage", capture.Purpose);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithPurpose_StillSendsTheLanguage()
    {
        // Purpose is additive: it must not displace the language hint the caller passed.
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(
            new[] { "Come si prepara Catan?" }, "it", EmbeddingPurpose.Query);

        Assert.Equal("it", capture.Language);
        Assert.Equal("query", capture.Purpose);
    }

    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_NoLanguageOverload_KeepsSendingEnglish()
    {
        // Pre-#3737 this overload hard-coded "en" in its own request-building block. The three
        // overloads now share one, so this pins that the merge did not change the request.
        var capture = new BodyCapture();
        IEmbeddingProvider provider = CreateProvider(capture);

        await provider.GenerateBatchEmbeddingsAsync(new[] { "chunk" });

        Assert.Equal("en", capture.Language);
    }

    [Fact]
    public void ToWireValue_MapsEachPurposeToItsServiceToken()
    {
        // These two tokens are what apps/embedding-service/main.py validates against; a rename
        // of an enum member must not silently change them.
        Assert.Equal("query", EmbeddingPurpose.Query.ToWireValue());
        Assert.Equal("passage", EmbeddingPurpose.Passage.ToWireValue());
    }

    private static HttpEmbeddingProvider CreateProvider(BodyCapture capture)
    {
        var handler = new DelegatingHandlerStub(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var root = JsonDocument.Parse(body).RootElement;
            capture.Language = root.GetProperty("language").GetString();
            capture.Purpose = root.TryGetProperty("purpose", out var p) ? p.GetString() : null;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        embeddings = new[] { new[] { 0.1f, 0.2f } },
                        model = "intfloat/multilingual-e5-base"
                    }),
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
        var config = new EmbeddingConfiguration
        {
            LocalServiceUrl = "http://localhost:8000",
            Model = "intfloat/multilingual-e5-base",
            Dimensions = 2
        };

        return new HttpEmbeddingProvider(httpClient, NullLogger<HttpEmbeddingProvider>.Instance, config);
    }

    private sealed class BodyCapture
    {
        public string? Language { get; set; }
        public string? Purpose { get; set; }
    }
}
