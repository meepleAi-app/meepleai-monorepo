using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4819: which cited documents cannot vouch for the page of their passages. The rule itself is
/// <see cref="GetDocumentsWithUnreliablePageNumbersQuery"/> (#4782); what is pinned here is what the answers that cite
/// a page can rely on: the right documents come back, the question is asked once, and when it cannot be asked no page
/// is believed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4819")]
public sealed class CitedPageTrustTests
{
    private static readonly Guid Flat = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Measured = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IMediator> _mediator = new();

    private CitedPageTrust Sut() => new(_mediator.Object, NullLogger<CitedPageTrust>.Instance);

    private void Unreliable(params Guid[] documents) => _mediator
        .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((IReadOnlyList<Guid>)documents);

    [Fact]
    public async Task OnlyTheDocumentsTheQuerySaysAreUnreliable_ComeBack()
    {
        Unreliable(Flat);

        var result = await Sut().WithoutAPageAsync(
            [Flat.ToString(), Measured.ToString()], CancellationToken.None);

        result.Should().BeEquivalentTo([Flat.ToString()]);
    }

    [Fact]
    public async Task TheIdComesBackAsTheCallerWroteIt_WhateverItsCase()
    {
        Unreliable(Flat);
        var upper = Flat.ToString().ToUpperInvariant();

        var result = await Sut().WithoutAPageAsync([upper], CancellationToken.None);

        result.Should().Contain(upper);
        result.Should().Contain(Flat.ToString(), "the comparison is case-insensitive");
    }

    [Fact]
    public async Task ADocumentCitedTwice_IsAskedAboutOnce()
    {
        GetDocumentsWithUnreliablePageNumbersQuery? asked = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<IReadOnlyList<Guid>>, CancellationToken>(
                (q, _) => asked = (GetDocumentsWithUnreliablePageNumbersQuery)q)
            .ReturnsAsync((IReadOnlyList<Guid>)[]);

        // The same document, written three ways: upper case, and without the hyphens. They are three strings and one GUID.
        await Sut().WithoutAPageAsync(
            [Flat.ToString(), Flat.ToString().ToUpperInvariant(), Flat.ToString("N"), Measured.ToString()],
            CancellationToken.None);

        asked.Should().NotBeNull();
        asked!.DocumentIds.Should().BeEquivalentTo([Flat, Measured]);
        _mediator.Verify(
            m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("doc-1")]
    public async Task WhatIsNotADocumentKnowledgeBaseWrote_IsNotAskedAboutAndNotReported(string? id)
    {
        var result = await Sut().WithoutAPageAsync([id], CancellationToken.None);

        result.Should().BeEmpty();
        _mediator.Verify(
            m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenTheQueryFails_NoPageIsBelieved()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var result = await Sut().WithoutAPageAsync(
            [Flat.ToString(), Measured.ToString()], CancellationToken.None);

        result.Should().BeEquivalentTo([Flat.ToString(), Measured.ToString()]);
    }

    [Fact]
    public async Task WhenTheQueryFails_EveryIdNamedIsReported_AlsoThoseThatAreNotDocuments()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var result = await Sut().WithoutAPageAsync([Flat.ToString(), "doc-1"], CancellationToken.None);

        // The same choice as the rule dispute: when nothing can be asked, nothing is believed.
        result.Should().BeEquivalentTo([Flat.ToString(), "doc-1"]);
    }

    [Fact]
    public async Task ACancellationOfTheCaller_PropagatesInsteadOfBecomingAFailure()
    {
        using var cts = new CancellationTokenSource();
        _mediator
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<IReadOnlyList<Guid>>, CancellationToken>((_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.FromResult((IReadOnlyList<Guid>)[]);
            });

        var act = () => Sut().WithoutAPageAsync([Flat.ToString()], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ACancellationThatIsNotTheCallers_IsAFailureLikeAnyOther()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetDocumentsWithUnreliablePageNumbersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("a timeout deeper down"));

        var result = await Sut().WithoutAPageAsync([Flat.ToString()], CancellationToken.None);

        result.Should().BeEquivalentTo([Flat.ToString()]);
    }
}
