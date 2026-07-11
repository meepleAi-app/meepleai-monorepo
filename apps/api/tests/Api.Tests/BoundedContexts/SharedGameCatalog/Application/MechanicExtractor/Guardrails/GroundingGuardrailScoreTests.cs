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

    private sealed class KeyedEmbeddings : IEmbeddingService
    {
        private readonly IReadOnlyDictionary<string, float[]> _map;
        public KeyedEmbeddings(IReadOnlyDictionary<string, float[]> map) => _map = map;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) =>
            Task.FromResult(_map.TryGetValue(text, out var v) ? v : new float[] { 0f, 0f });
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

    [Fact]
    public async Task EvaluateDetailedAsync_PopulatesPerClaimScores_KeyedByObjectPath()
    {
        // #2811: two mechanics claims — one well-grounded (cosine 1.0), one poorly (cosine 0.0).
        // ClaimScores must carry EACH claim's own cosine keyed by its object path, so a well-grounded
        // sibling no longer inherits the section-wide min.
        var embeddings = new KeyedEmbeddings(new Dictionary<string, float[]>
        {
            ["well grounded claim"] = new[] { 1f, 0f },
            ["cited chunk A"] = new[] { 1f, 0f }, // cosine(well, A) = 1.0
            ["poorly grounded claim"] = new[] { 1f, 0f },
            ["cited chunk B"] = new[] { 0f, 1f }, // cosine(poor, B) = 0.0
        });
        var guardrail = new GroundingGuardrail(embeddings);

        var json = """
        {"mechanics":[
          {"description":"well grounded claim","citations":[{"pdf_page":1,"quote":"q","chunk_id":"11111111-1111-4111-8111-111111111111"}]},
          {"description":"poorly grounded claim","citations":[{"pdf_page":2,"quote":"q","chunk_id":"22222222-2222-4222-8222-222222222222"}]}
        ]}
        """;
        using var doc = JsonDocument.Parse(json);
        var chunks = new[]
        {
            new MechanicSourceChunk(0, 1, Guid.Parse("11111111-1111-4111-8111-111111111111"), "cited chunk A"),
            new MechanicSourceChunk(1, 2, Guid.Parse("22222222-2222-4222-8222-222222222222"), "cited chunk B"),
        };
        var ctx = new MechanicGuardrailContext(MechanicSection.Mechanics, doc.RootElement, chunks, 10,
            new MechanicGuardrailOptions { MinClaimGroundingSimilarity = 0.5 });

        var result = await guardrail.EvaluateDetailedAsync(ctx, CancellationToken.None);

        result.ClaimScores.Should().NotBeNull();
        result.ClaimScores!["$.mechanics[0]"].Should().BeApproximately(1.0, 0.001);
        result.ClaimScores!["$.mechanics[1]"].Should().BeApproximately(0.0, 0.001);
        result.Score!.Value.Should().BeApproximately(0.0, 0.001); // section min unchanged
    }
}
