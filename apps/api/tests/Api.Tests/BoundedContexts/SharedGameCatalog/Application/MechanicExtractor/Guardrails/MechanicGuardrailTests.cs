using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails;

public sealed class MechanicGuardrailOptionsTests
{
    [Fact]
    public void Defaults_MatchAdr051()
    {
        var o = new MechanicGuardrailOptions();
        o.MaxQuoteWords.Should().Be(25);
        o.MaxConsecutiveSourceWords.Should().Be(10);
        o.MinClaimGroundingSimilarity.Should().Be(0.65);
        o.MaxAnalysisCostUsd.Should().Be(2.00m);
        o.MaxRetriesPerSection.Should().Be(2);
        MechanicGuardrailOptions.SectionName.Should().Be("MechanicGuardrails");
    }
}

internal static class GuardrailTestContext
{
    public static MechanicGuardrailContext Ctx(
        string json,
        IReadOnlyList<MechanicSourceChunk>? chunks = null,
        int? pageCount = 50)
        => new(
            MechanicSection.Mechanics,
            JsonDocument.Parse(json).RootElement,
            chunks ?? Array.Empty<MechanicSourceChunk>(),
            pageCount,
            new MechanicGuardrailOptions());
}

public sealed class QuoteCapGuardrailTests
{
    // #2494 AC-1: T1 boundary/Unicode cases moved out of [InlineData] and into the
    // fixture corpus under tests/Api.Tests/Fixtures/MechanicValidator/T1/*.json so
    // future contributors can append cases without touching the test source.
    private static readonly string FixtureRoot = Path.Combine(
        Path.GetDirectoryName(typeof(QuoteCapGuardrailTests).Assembly.Location)!,
        "Fixtures",
        "MechanicValidator",
        "T1");

    public sealed record T1Fixture(string Name, string Description, string Quote, bool ExpectedPass);

    public static IEnumerable<object[]> T1Cases()
    {
        // Discover every *.json fixture at runtime — adding a new case is a single
        // file drop, no test code change required.
        foreach (var path in Directory.EnumerateFiles(FixtureRoot, "*.json").OrderBy(p => p))
        {
            var json = File.ReadAllText(path);
            var fixture = JsonSerializer.Deserialize<T1Fixture>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            yield return new object[] { fixture };
        }
    }

    [Theory]
    [MemberData(nameof(T1Cases))]
    public async Task QuoteCap_FixtureCorpus(T1Fixture fixture)
    {
        // Encode the fixture quote into the same {"citations":[{"quote": ...}]}
        // wrapper the guardrail expects. Use JsonSerializer to safely escape any
        // embedded Unicode separators (NBSP, thin space, em-dash) in the raw JSON.
        var encodedQuote = JsonSerializer.Serialize(fixture.Quote);
        var payload = "{\"citations\":[{\"quote\":" + encodedQuote + "}]}";

        var result = await new QuoteCapGuardrail()
            .EvaluateAsync(GuardrailTestContext.Ctx(payload), default);

        if (fixture.ExpectedPass)
        {
            result.Should().BeEmpty($"fixture {fixture.Name} expected to pass: {fixture.Description}");
        }
        else
        {
            result.Should().ContainSingle(
                $"fixture {fixture.Name} expected to fail: {fixture.Description}")
                .Which.Rule.Should().Be("T1_quote_cap");
        }
    }
}

public sealed class CitationPresenceGuardrailTests
{
    [Fact]
    public async Task ClaimWithoutCitations_Fails()
    {
        var json = "{\"claim\":\"x\"}";
        var r = await new CitationPresenceGuardrail().EvaluateAsync(GuardrailTestContext.Ctx(json), default);
        r.Should().ContainSingle().Which.Rule.Should().Be("T3_citation_required");
    }

    [Fact]
    public async Task ClaimWithCitations_Passes()
    {
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":1,\"quote\":\"q\"}]}";
        var r = await new CitationPresenceGuardrail().EvaluateAsync(GuardrailTestContext.Ctx(json), default);
        r.Should().BeEmpty();
    }
}

public sealed class PageSubstringGuardrailTests
{
    private static Task<IReadOnlyList<MechanicValidationViolation>> Eval(
        string json, IReadOnlyList<MechanicSourceChunk> chunks, int? pageCount)
        => new PageSubstringGuardrail().EvaluateAsync(GuardrailTestContext.Ctx(json, chunks, pageCount), default);

    [Fact]
    public async Task QuoteIsNormalizedSubstringOfChunk_Passes()
    {
        var chunks = new[] { new MechanicSourceChunk(0, 3, null, "Players take turns drawing cards from the deck.") };
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":3,\"quote\":\"drawing cards from the deck\"}]}";
        var r = await Eval(json, chunks, pageCount: 50);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task PageOutOfRange_Fails()
    {
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":51,\"quote\":\"anything\"}]}";
        var r = await Eval(json, Array.Empty<MechanicSourceChunk>(), pageCount: 50);
        r.Should().ContainSingle().Which.Rule.Should().Be("T4_page_out_of_range");
    }

    [Fact]
    public async Task QuoteNotSubstring_Fails()
    {
        var chunks = new[] { new MechanicSourceChunk(0, 3, null, "The board has nineteen spaces.") };
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":3,\"quote\":\"dragons breathe fire\"}]}";
        var r = await Eval(json, chunks, pageCount: 50);
        r.Should().ContainSingle().Which.Rule.Should().Be("T4_quote_not_substring");
    }

    [Fact]
    public async Task PageInRangeButNotIndexed_SkipsCheck()
    {
        // Pool has page metadata (page 3) but the citation cites page 5 (in range, not indexed).
        // Must skip — NOT widen to the whole document (would let a fabricated page citation pass).
        var chunks = new[] { new MechanicSourceChunk(0, 3, null, "the deck has cards") };
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":5,\"quote\":\"anything not present\"}]}";
        var r = await Eval(json, chunks, pageCount: 50);
        r.Should().BeEmpty();
    }
}

public sealed class RejectionSamplingGuardrailTests
{
    private static Task<IReadOnlyList<MechanicValidationViolation>> Eval(
        string json, IReadOnlyList<MechanicSourceChunk> chunks)
        => new RejectionSamplingGuardrail().EvaluateAsync(GuardrailTestContext.Ctx(json, chunks), default);

    [Fact]
    public async Task TenWordVerbatimFromClaim_Fails()
    {
        var src = "players take turns drawing cards from the top of the deck each round";
        var chunks = new[] { new MechanicSourceChunk(0, 1, null, src) };
        var json = "{\"claim\":\"players take turns drawing cards from the top of the deck\"}"; // 11 words verbatim
        var r = await Eval(json, chunks);
        r.Should().ContainSingle().Which.Rule.Should().Be("T2_long_verbatim");
    }

    [Fact]
    public async Task ShortOverlap_Passes()
    {
        var chunks = new[] { new MechanicSourceChunk(0, 1, null, "players take turns drawing cards from the top deck") };
        var json = "{\"claim\":\"players take turns drawing cards from the top\"}"; // 8 words < 10
        var r = await Eval(json, chunks);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task CitationQuote_IsExcluded()
    {
        var src = "players take turns drawing cards from the top of the deck each round";
        var chunks = new[] { new MechanicSourceChunk(0, 1, null, src) };
        var json = "{\"citations\":[{\"quote\":\"players take turns drawing cards from the top of the deck\"}]}";
        var r = await Eval(json, chunks);
        r.Should().BeEmpty();
    }
}

public sealed class GroundingGuardrailTests
{
    [Fact]
    public async Task LowCosine_Fails()
    {
        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.EmbedAsync("cards have suits", It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { 1f, 0f });
        embed.Setup(e => e.EmbedAsync(It.Is<string>(s => s != "cards have suits"), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { 0f, 1f }); // orthogonal → cosine 0 < 0.65
        var chunks = new[] { new MechanicSourceChunk(7, 12, null, "the board is hexagonal") };
        var json = "{\"claim\":\"cards have suits\",\"citations\":[{\"pdf_page\":12,\"quote\":\"q\"}]}";
        var r = await new GroundingGuardrail(embed.Object)
            .EvaluateAsync(GuardrailTestContext.Ctx(json, chunks), default);
        r.Should().ContainSingle().Which.Rule.Should().Be("T3_grounding");
    }

    [Fact]
    public async Task EmbeddingOutage_FailsClosed()
    {
        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("down"));
        var chunks = new[] { new MechanicSourceChunk(7, 12, null, "x") };
        var json = "{\"claim\":\"c\",\"citations\":[{\"pdf_page\":12,\"quote\":\"q\"}]}";
        var r = await new GroundingGuardrail(embed.Object)
            .EvaluateAsync(GuardrailTestContext.Ctx(json, chunks), default);
        r.Should().ContainSingle().Which.Rule.Should().Be("T3_grounding_unavailable");
    }

    [Fact]
    public async Task HighCosine_Passes()
    {
        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { 1f, 1f }); // identical → cosine 1.0
        var chunks = new[] { new MechanicSourceChunk(7, 12, null, "the board is hexagonal") };
        var json = "{\"claim\":\"cards have suits\",\"citations\":[{\"pdf_page\":12,\"quote\":\"q\"}]}";
        var r = await new GroundingGuardrail(embed.Object)
            .EvaluateAsync(GuardrailTestContext.Ctx(json, chunks), default);
        r.Should().BeEmpty();
    }
}

public sealed class MechanicOutputValidatorChainTests
{
    [Fact]
    public async Task FailFast_StopsAtFirstFailingFamily()
    {
        // T1 fails (26-word quote) AND T4 would fail (page 999) → only T1 reported (Order 10 < 20).
        var json = "{\"claim\":\"x\",\"citations\":[{\"pdf_page\":999,\"quote\":\"" +
                   string.Join(' ', Enumerable.Range(1, 26).Select(i => "w" + i)) + "\"}]}";
        var sut = new MechanicOutputValidator(
            new IMechanicGuardrail[] { new QuoteCapGuardrail(), new PageSubstringGuardrail() },
            NullLogger<MechanicOutputValidator>.Instance);
        var result = await sut.ValidateAsync(GuardrailTestContext.Ctx(json, pageCount: 50), default);
        result.IsValid.Should().BeFalse();
        result.Violations.Should().OnlyContain(v => v.Rule.StartsWith("T1"));
    }

    [Fact]
    public async Task AllPass_ReturnsValid()
    {
        var chunks = new[] { new MechanicSourceChunk(0, 3, null, "players draw a card") };
        var json = "{\"claim\":\"players draw\",\"citations\":[{\"pdf_page\":3,\"quote\":\"draw a card\"}]}";
        var sut = new MechanicOutputValidator(
            new IMechanicGuardrail[] { new QuoteCapGuardrail(), new CitationPresenceGuardrail(), new PageSubstringGuardrail() },
            NullLogger<MechanicOutputValidator>.Instance);
        var result = await sut.ValidateAsync(GuardrailTestContext.Ctx(json, chunks, pageCount: 50), default);
        result.IsValid.Should().BeTrue();
    }
}
