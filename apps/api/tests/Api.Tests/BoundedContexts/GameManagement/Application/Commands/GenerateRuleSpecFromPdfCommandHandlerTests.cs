using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Unit tests for GenerateRuleSpecFromPdfCommandHandler.
/// Issue #3096: an orphaned PDF (not linked to any game) must surface a 409 instead of
/// fabricating a RuleSpec with GameId = Guid.Empty.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GenerateRuleSpecFromPdfCommandHandlerTests
{
    private readonly MeepleAiDbContext _db;
    private readonly GenerateRuleSpecFromPdfCommandHandler _handler;

    public GenerateRuleSpecFromPdfCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GenerateRuleSpecFromPdfCommandHandler(
            _db, new RuleAtomParsingDomainService(), TimeProvider.System);
    }

    private const string TwoAtomicRules = "[\"Rule one\", \"Rule two\"]";

    private PdfDocumentEntity SeedPdf(
        Guid? privateGameId,
        Guid? sharedGameId,
        string? atomicRules = TwoAtomicRules,
        string? extractedText = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = privateGameId,
            SharedGameId = sharedGameId,
            FileName = "Rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1000,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            AtomicRules = atomicRules,
            ExtractedText = extractedText
        };
        _db.PdfDocuments.Add(pdf);
        return pdf;
    }

    [Fact]
    public async Task Handle_WhenPdfLinkedToPrivateGame_ReturnsRuleSpecWithThatGameId()
    {
        var gameId = Guid.NewGuid();
        var pdf = SeedPdf(privateGameId: gameId, sharedGameId: null);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GenerateRuleSpecFromPdfCommand(pdf.Id), CancellationToken.None);

        result.GameId.Should().Be(gameId);
        result.Atoms.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    public async Task Handle_WhenAtomicRulesAbsentOrEmpty_BuildsOneAtomPerNonBlankExtractedTextLine(string? atomicRules)
    {
        // #4440: la pipeline non scrive piu' AtomicRules (le produceva l'estrattore iText, tolto).
        // Per i PDF nuovi il ripiego su ExtractedText e' l'unico percorso vivo di
        // POST /ingest/pdf/{id}/rulespec: le righe vuote non diventano atomi.
        var pdf = SeedPdf(
            privateGameId: Guid.NewGuid(),
            sharedGameId: null,
            atomicRules: atomicRules,
            extractedText: "Prima regola\r\nSeconda regola\n\nTerza regola");
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GenerateRuleSpecFromPdfCommand(pdf.Id), CancellationToken.None);

        result.Atoms.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WhenPdfLinkedOnlyToSharedGame_FallsBackToSharedGameId()
    {
        var sharedGameId = Guid.NewGuid();
        var pdf = SeedPdf(privateGameId: null, sharedGameId: sharedGameId);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GenerateRuleSpecFromPdfCommand(pdf.Id), CancellationToken.None);

        result.GameId.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task Handle_WhenPdfNotLinkedToAnyGame_ThrowsConflictInsteadOfFabricatingEmptyGuid()
    {
        // Regression for #3096: previously this fell back to `?? Guid.Empty` and returned a
        // RuleSpec claiming to belong to the empty game.
        var pdf = SeedPdf(privateGameId: null, sharedGameId: null);
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(new GenerateRuleSpecFromPdfCommand(pdf.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenPdfDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GenerateRuleSpecFromPdfCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
