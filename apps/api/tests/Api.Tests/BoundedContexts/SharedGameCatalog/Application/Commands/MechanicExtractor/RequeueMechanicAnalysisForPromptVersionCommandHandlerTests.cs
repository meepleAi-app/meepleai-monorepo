using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class RequeueMechanicAnalysisForPromptVersionCommandHandlerTests
{
    private const string CurrentPrompt = "v1.2.0";

    private readonly Mock<IMechanicCardRepository> _cards = new();
    private readonly Mock<IMechanicAnalysisRepository> _analyses = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMechanicPromptProvider> _prompt = new();
    private readonly RequeueMechanicAnalysisForPromptVersionCommandHandler _handler;

    public RequeueMechanicAnalysisForPromptVersionCommandHandlerTests()
    {
        _prompt.SetupGet(p => p.PromptVersion).Returns(CurrentPrompt);
        _handler = new RequeueMechanicAnalysisForPromptVersionCommandHandler(
            _cards.Object,
            _analyses.Object,
            _mediator.Object,
            _prompt.Object,
            new Mock<ILogger<RequeueMechanicAnalysisForPromptVersionCommandHandler>>().Object);
    }

    private static MechanicCard BuildCard(Guid gameId, string content) =>
        MechanicCard.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: gameId,
            originAnalysisId: Guid.NewGuid(),
            origin: MechanicCardOrigin.AiReviewed,
            title: "Card",
            content: content,
            version: 1,
            isSuppressed: false,
            suppressedReason: null,
            suppressedAt: null,
            suppressedBy: null,
            errorReportsCount: 0,
            feedbackScore: null,
            publishedAt: DateTime.UtcNow,
            publishedBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow);

    private static string ContentWithPdf(Guid gameId, Guid pdfId) =>
        new MechanicCardContent
        {
            SnapshotAt = DateTime.UtcNow,
            SourceAnalysisId = Guid.NewGuid(),
            SourcePromptVersion = "v1.1.0",
            Metadata = new MechanicCardMetadata
            {
                SharedGameId = gameId,
                SharedGameName = "Catan",
                Publisher = "Kosmos",
                Language = "en"
            },
            Claims = new[]
            {
                new MechanicCardClaimSnapshot
                {
                    Id = Guid.NewGuid(), Section = "Summary", Ordinal = 0, Claim = "c",
                    Citations = new[] { new MechanicCardCitationSnapshot { PdfId = pdfId, PdfPage = 1, Quote = "q" } }
                }
            }
        }.ToJson();

    private static MechanicAnalysisGenerationResponseDto Response(Guid id) =>
        new(id, MechanicAnalysisStatus.Draft, CurrentPrompt, 2.00m, 0.5m, 1000, false, $"/status/{id}", false);

    [Fact]
    public async Task Handle_WithActiveCard_SendsGenerateForSamePdf()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var expected = Response(Guid.NewGuid());
        _cards.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCard(gameId, ContentWithPdf(gameId, pdfId)));
        _analyses.Setup(r => r.FindByPromptVersionAsync(gameId, pdfId, CurrentPrompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicAnalysis?)null);
        _mediator.Setup(m => m.Send(
                It.Is<GenerateMechanicAnalysisCommand>(c =>
                    c.SharedGameId == gameId && c.PdfDocumentId == pdfId &&
                    c.RequestedBy == actor && c.CostCapUsd == 3.5m && !c.ForceRegenerate),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(
            new RequeueMechanicAnalysisForPromptVersionCommand(gameId, actor, 3.5m), CancellationToken.None);

        result.Should().Be(expected);
        _mediator.Verify(m => m.Send(It.IsAny<GenerateMechanicAnalysisCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutCard_Throws404()
    {
        var gameId = Guid.NewGuid();
        _cards.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicCard?)null);

        var act = () => _handler.Handle(
            new RequeueMechanicAnalysisForPromptVersionCommand(gameId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mediator.Verify(m => m.Send(It.IsAny<GenerateMechanicAnalysisCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnalysisInReview_Throws409AndDoesNotSendGenerate()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var inReview = MechanicAnalysis.Create(
            gameId, pdfId, CurrentPrompt, Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1.0m);
        var citation = MechanicCitation.Create(pdfId, 1, "q", null, 0);
        inReview.AddClaim(MechanicClaim.Create(inReview.Id, MechanicSection.Mechanics, "c", 0, new[] { citation }));
        inReview.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        _cards.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCard(gameId, ContentWithPdf(gameId, pdfId)));
        _analyses.Setup(r => r.FindByPromptVersionAsync(gameId, pdfId, CurrentPrompt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inReview);

        var act = () => _handler.Handle(
            new RequeueMechanicAnalysisForPromptVersionCommand(gameId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _mediator.Verify(m => m.Send(It.IsAny<GenerateMechanicAnalysisCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMalformedCardContent_Throws409()
    {
        var gameId = Guid.NewGuid();
        _cards.Setup(r => r.GetActiveByGameAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildCard(gameId, "{not json"));

        var act = () => _handler.Handle(
            new RequeueMechanicAnalysisForPromptVersionCommand(gameId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*unreadable*");
    }
}
