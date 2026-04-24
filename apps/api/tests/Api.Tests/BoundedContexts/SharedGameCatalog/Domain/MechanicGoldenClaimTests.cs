using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

public class MechanicGoldenClaimTests
{
    private readonly Mock<IEmbeddingService> _emb = new();
    private readonly Mock<IKeywordExtractor> _kw = new();

    public MechanicGoldenClaimTests()
    {
        _emb.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        _kw.Setup(k => k.Extract(It.IsAny<string>()))
            .Returns(new[] { "trade", "resource" });
    }

    private Task<MechanicGoldenClaim> BuildValidClaim(CancellationToken ct = default)
        => MechanicGoldenClaim.CreateAsync(
            sharedGameId: Guid.NewGuid(),
            section: MechanicSection.Mechanics,
            statement: "Players trade resources each round.",
            expectedPage: 5,
            sourceQuote: "On your turn you may trade any number of resources with other players.",
            curatorUserId: Guid.NewGuid(),
            embedding: _emb.Object,
            keywords: _kw.Object,
            ct: ct);

    [Fact]
    public async Task CreateAsync_sets_keywords_and_embedding()
    {
        var claim = await BuildValidClaim();

        claim.Keywords.Should().Contain("trade");
        claim.Embedding.Should().NotBeNull();
        claim.Embedding!.Length.Should().Be(768);
        _emb.Verify(e => e.EmbedAsync("Players trade resources each round.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_with_same_statement_does_not_reembed()
    {
        var claim = await BuildValidClaim();
        _emb.Invocations.Clear();

        await claim.UpdateAsync(
            statement: claim.Statement,
            expectedPage: 6,
            sourceQuote: "Updated quote that is valid and long enough.",
            embedding: _emb.Object,
            keywords: _kw.Object,
            ct: default);

        _emb.Verify(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_with_different_statement_reembeds()
    {
        var claim = await BuildValidClaim();
        _emb.Invocations.Clear();

        await claim.UpdateAsync(
            statement: "A completely different statement for the rule.",
            expectedPage: 5,
            sourceQuote: "On your turn you may trade any number of resources with other players.",
            embedding: _emb.Object,
            keywords: _kw.Object,
            ct: default);

        _emb.Verify(e => e.EmbedAsync("A completely different statement for the rule.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivate_twice_throws()
    {
        var claim = await BuildValidClaim();

        claim.Deactivate();
        claim.DeletedAt.Should().NotBeNull();

        var act = () => claim.Deactivate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", 5, "Valid source quote here.")]
    [InlineData("   ", 5, "Valid source quote here.")]
    public async Task CreateAsync_rejects_empty_statement(string statement, int page, string quote)
    {
        var act = () => MechanicGoldenClaim.CreateAsync(
            Guid.NewGuid(), MechanicSection.Mechanics, statement, page, quote,
            Guid.NewGuid(), _emb.Object, _kw.Object, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_statement_over_500_chars()
    {
        var longStatement = new string('x', 501);

        var act = () => MechanicGoldenClaim.CreateAsync(
            Guid.NewGuid(), MechanicSection.Mechanics, longStatement, 5, "Valid source quote here.",
            Guid.NewGuid(), _emb.Object, _kw.Object, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_page_less_than_1()
    {
        var act = () => MechanicGoldenClaim.CreateAsync(
            Guid.NewGuid(), MechanicSection.Mechanics, "Valid statement text.", 0, "Valid source quote here.",
            Guid.NewGuid(), _emb.Object, _kw.Object, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_source_quote_over_1000_chars()
    {
        var longQuote = new string('q', 1001);

        var act = () => MechanicGoldenClaim.CreateAsync(
            Guid.NewGuid(), MechanicSection.Mechanics, "Valid statement text.", 5, longQuote,
            Guid.NewGuid(), _emb.Object, _kw.Object, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
