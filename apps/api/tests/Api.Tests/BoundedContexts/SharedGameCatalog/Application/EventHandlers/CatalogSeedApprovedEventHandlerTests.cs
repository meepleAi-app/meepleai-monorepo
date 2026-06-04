using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedApprovedEventHandlerTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _drafts = new();
    private readonly Mock<ISharedGameRepository> _games = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly TimeProvider _time = TimeProvider.System;

    private CatalogSeedApprovedEventHandler Handler() =>
        new(_drafts.Object, _games.Object, _uow.Object, _time,
            NullLogger<CatalogSeedApprovedEventHandler>.Instance);

    private static string ProvenanceWithTitle(string title)
    {
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "labels.en",
                FetchedAt: DateTime.UtcNow,
                Value: title),
            ["yearPublished"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "P577",
                FetchedAt: DateTime.UtcNow,
                Value: 1995),
        };
        return new CatalogSeedProvenance(fields).ToJson();
    }

    private static CatalogSeedDraftEntity SeedFetchedDraft(
        int? bggId = 12345,
        string? provenanceJson = null,
        string title = "Catan")
    {
        return new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = bggId,
            Status = "Approved", // M4.4 already set this before publishing the event
            ProvenanceJson = provenanceJson ?? ProvenanceWithTitle(title),
            ResultingSharedGameId = Guid.NewGuid(), // M4.4 placeholder we will overwrite
            ApprovedAt = DateTime.UtcNow,
            ApprovedByUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesSkeletonAndOverwritesResultingId()
    {
        var draft = SeedFetchedDraft();
        var placeholderId = draft.ResultingSharedGameId!.Value;
        var approvedByUserId = Guid.NewGuid();

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _games.Setup(r => r.GetByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((SharedGame?)null);

        SharedGame? added = null;
        _games.Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
              .Callback<SharedGame, CancellationToken>((g, _) => added = g)
              .Returns(Task.CompletedTask);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, placeholderId, approvedByUserId),
            default);

        added.Should().NotBeNull();
        added!.Title.Should().Be("Catan");
        added.BggId.Should().Be(12345);
        added.CreatedBy.Should().Be(approvedByUserId);

        draft.ResultingSharedGameId.Should().Be(added.Id, "the placeholder must be overwritten with the real aggregate Id");
        draft.ResultingSharedGameId.Should().NotBe(placeholderId);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DraftNotFound_LogsAndReturns()
    {
        var draftId = Guid.NewGuid();
        _drafts.Setup(r => r.GetByIdAsync(draftId, It.IsAny<CancellationToken>()))
               .ReturnsAsync((CatalogSeedDraftEntity?)null);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draftId, Guid.NewGuid(), Guid.NewGuid()),
            default);

        _games.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyProvenance_LogsAndReturns()
    {
        var draft = SeedFetchedDraft();
        draft.ProvenanceJson = null;

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, Guid.NewGuid()),
            default);

        _games.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MalformedProvenanceJson_LogsAndReturns()
    {
        var draft = SeedFetchedDraft();
        draft.ProvenanceJson = "{ not valid json";

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, Guid.NewGuid()),
            default);

        _games.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingTitleInProvenance_LogsAndReturns()
    {
        var draft = SeedFetchedDraft();
        // Build a provenance with no "title" key
        var fieldsNoTitle = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["yearPublished"] = new FieldProvenance(
                Provider: "wikidata",
                SourceUrl: "https://www.wikidata.org/wiki/Q123",
                SourceField: "P577",
                FetchedAt: DateTime.UtcNow,
                Value: 1995),
        };
        draft.ProvenanceJson = new CatalogSeedProvenance(fieldsNoTitle).ToJson();

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, Guid.NewGuid()),
            default);

        _games.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SharedGameAlreadyExistsForBggId_ReusesExistingAndOverwritesPlaceholder()
    {
        var draft = SeedFetchedDraft(bggId: 12345);
        var placeholderId = draft.ResultingSharedGameId!.Value;
        var approvedByUserId = Guid.NewGuid();

        // Existing aggregate (reuse path — idempotency for double-approval / collision with prior import)
        var existing = SharedGame.CreateSkeleton("Catan", Guid.NewGuid(), TimeProvider.System, bggId: 12345);

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _games.Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, placeholderId, approvedByUserId),
            default);

        _games.Verify(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()), Times.Never);
        draft.ResultingSharedGameId.Should().Be(existing.Id, "should reuse existing aggregate Id, overwriting placeholder");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoBggId_StillCreatesSkeletonByTitleOnly()
    {
        // Catalog seed pure-Wikidata path (no BggId)
        var draft = SeedFetchedDraft(bggId: null, title: "Pandemic");

        _drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        SharedGame? added = null;
        _games.Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
              .Callback<SharedGame, CancellationToken>((g, _) => added = g)
              .Returns(Task.CompletedTask);

        await Handler().Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, Guid.NewGuid()),
            default);

        added.Should().NotBeNull();
        added!.Title.Should().Be("Pandemic");
        added.BggId.Should().BeNull();
        // GetByBggIdAsync should not have been called when BggId is null
        _games.Verify(r => r.GetByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
