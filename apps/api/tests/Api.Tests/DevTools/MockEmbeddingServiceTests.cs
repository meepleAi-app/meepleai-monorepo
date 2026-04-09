using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsStableVectorForSameInput()
    {
        var svc = new MockEmbeddingService();
        var r1 = await svc.GenerateEmbeddingAsync("hello world", CancellationToken.None);
        var r2 = await svc.GenerateEmbeddingAsync("hello world", CancellationToken.None);
        Assert.True(r1.Success);
        Assert.Equal(r1.Embeddings[0], r2.Embeddings[0]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_DifferentInputsDifferentVectors()
    {
        var svc = new MockEmbeddingService();
        var r1 = await svc.GenerateEmbeddingAsync("foo", CancellationToken.None);
        var r2 = await svc.GenerateEmbeddingAsync("bar", CancellationToken.None);
        Assert.NotEqual(r1.Embeddings[0], r2.Embeddings[0]);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_VectorIsNormalized()
    {
        var svc = new MockEmbeddingService();
        var r = await svc.GenerateEmbeddingAsync("test", CancellationToken.None);
        var norm = Math.Sqrt(r.Embeddings[0].Sum(v => (double)v * v));
        Assert.InRange(norm, 0.99, 1.01);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Returns768Dimensions()
    {
        var svc = new MockEmbeddingService();
        var r = await svc.GenerateEmbeddingAsync("any text", CancellationToken.None);
        Assert.Equal(768, r.Embeddings[0].Length);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_BatchReturnsOneVectorPerText()
    {
        var svc = new MockEmbeddingService();
        var texts = new System.Collections.Generic.List<string> { "alpha", "beta", "gamma" };
        var r = await svc.GenerateEmbeddingsAsync(texts, CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(3, r.Embeddings.Count);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_LanguageAffectsVector()
    {
        var svc = new MockEmbeddingService();
        var rEn = await svc.GenerateEmbeddingAsync("hello", "en", CancellationToken.None);
        var rIt = await svc.GenerateEmbeddingAsync("hello", "it", CancellationToken.None);
        Assert.NotEqual(rEn.Embeddings[0], rIt.Embeddings[0]);
    }

    [Fact]
    public void ReportsCorrectDimensionsAndModel()
    {
        var svc = new MockEmbeddingService();
        Assert.Equal(768, svc.GetEmbeddingDimensions());
        Assert.Equal("mock-e5-base", svc.GetModelName());
    }
}
