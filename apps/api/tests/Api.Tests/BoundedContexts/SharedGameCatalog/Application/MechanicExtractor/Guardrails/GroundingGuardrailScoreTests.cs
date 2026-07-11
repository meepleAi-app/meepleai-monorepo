using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GroundingGuardrailScoreTests
{
    private sealed class FixedEmbeddings : IEmbeddingService
    {
        private readonly float[] _vec;
        public FixedEmbeddings(float[] vec) => _vec = vec;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => Task.FromResult(_vec);
    }

    [Fact]
    public async Task EvaluateDetailedAsync_PopulatesScore_WithMinCosine_OnPass()
    {
        // identical vectors → cosine 1.0 → passes threshold, but score must still be surfaced
        var embeddings = new FixedEmbeddings(new[] { 1f, 0f });
        var guardrail = new GroundingGuardrail(embeddings);

        var json = """
        {"summary":{"text":"players score points","citations":[{"pdf_page":1,"quote":"score points","chunk_id":"11111111-1111-4111-8111-111111111111"}]}}
        """;
        using var doc = JsonDocument.Parse(json);
        var chunks = new[] { new MechanicSourceChunk(0, 1, Guid.Parse("11111111-1111-4111-8111-111111111111"), "score points") };
        var ctx = new MechanicGuardrailContext(MechanicSection.Summary, doc.RootElement, chunks, 10,
            new MechanicGuardrailOptions { MinClaimGroundingSimilarity = 0.5 });

        var result = await guardrail.EvaluateDetailedAsync(ctx, CancellationToken.None);

        result.Violations.Should().BeEmpty();
        result.Score.Should().NotBeNull();
        result.Score!.Value.Should().BeApproximately(1.0, 0.001);
    }
}
