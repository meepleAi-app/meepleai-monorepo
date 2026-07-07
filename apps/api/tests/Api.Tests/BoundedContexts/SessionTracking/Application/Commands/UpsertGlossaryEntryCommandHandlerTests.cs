using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Tests for <see cref="UpsertGlossaryEntryCommandHandler"/> covering the
/// cross-entry <c>termIt</c> collision detection introduced by issue #1312.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "1312")]
public sealed class UpsertGlossaryEntryCommandHandlerTests
{
    private readonly Mock<IGamebookCampaignSessionRepository> _campaignsMock = new();
    private readonly Mock<IGamebookGlossaryRepository> _glossaryMock = new();
    private readonly UpsertGlossaryEntryCommandHandler _handler;

    private static readonly Guid CampaignId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EntryId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public UpsertGlossaryEntryCommandHandlerTests()
    {
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), OwnerId, "Test campaign");
        _campaignsMock
            .Setup(r => r.GetByIdAsync(CampaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        _handler = new UpsertGlossaryEntryCommandHandler(
            _campaignsMock.Object,
            _glossaryMock.Object);
    }

    private static GamebookGlossaryEntry MakeEntry(string termEn, string termIt)
    {
        // The factory generates an Id; tests that need a specific Id read it back
        // via `entry.Id` after construction (see AC-2 below for the self-update flow).
        return GamebookGlossaryEntry.Create(
            CampaignId,
            termEn,
            termIt,
            GlossarySource.Manual,
            OwnerId);
    }

    // -------------------------------------------------------------------------
    // AC-1 — 409 when another entry already uses the target termIt
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_AnotherEntryHasSameTermIt_ThrowsGlossaryTermCollisionException()
    {
        // Arrange — the entry being edited.
        var editing = MakeEntry("Voidstone", "Pietra del Vuoto");
        _glossaryMock
            .Setup(r => r.GetByIdAsync(EntryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editing);

        // Another entry on the same campaign already uses "Pietra del Caos".
        var conflicting = MakeEntry("Chaosstone", "Pietra del Caos");
        _glossaryMock
            .Setup(r => r.GetByTermItAsync(CampaignId, "Pietra del Caos", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conflicting);

        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, EntryId, "Voidstone", "Pietra del Caos", OwnerId);

        // Act
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<GlossaryTermCollisionException>();
        ex.Which.CollidingEntryId.Should().Be(conflicting.Id);
        ex.Which.CollidingTermEn.Should().Be("Chaosstone");
    }

    // -------------------------------------------------------------------------
    // AC-2 — 200 when the SAME entry updates its own termIt (no false positive)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_SameEntryUpdatesItsOwnTermIt_DoesNotThrow_AndSucceeds()
    {
        // Arrange — the entry being edited; the only match on the new termIt
        // is the entry ITSELF (same Id). This must NOT trigger collision.
        var editing = MakeEntry("Voidstone", "Pietra del Vuoto");
        _glossaryMock
            .Setup(r => r.GetByIdAsync(editing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editing);

        _glossaryMock
            .Setup(r => r.GetByTermItAsync(CampaignId, "Pietra del Vuoto rev", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);

        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, editing.Id, "Voidstone", "Pietra del Vuoto rev", OwnerId);

        // Act
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        // Assert — succeeded, no collision exception, returned DTO reflects update.
        dto.TermEn.Should().Be("Voidstone");
        dto.TermIt.Should().Be("Pietra del Vuoto rev");
    }

    // -------------------------------------------------------------------------
    // AC-1 edge — case-insensitive trimmed match
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_CollisionIsCaseInsensitiveAndTrimmed()
    {
        // Arrange — colliding entry has "  pietra DEL caos  " (whitespace + casing).
        var editing = MakeEntry("Voidstone", "Pietra del Vuoto");
        _glossaryMock
            .Setup(r => r.GetByIdAsync(EntryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editing);

        var conflicting = MakeEntry("Chaosstone", "  pietra DEL caos  ");
        _glossaryMock
            .Setup(r => r.GetByTermItAsync(
                CampaignId,
                It.Is<string>(s => s.Equals("Pietra del Caos", StringComparison.OrdinalIgnoreCase) ||
                                   s.Trim().Equals("pietra del caos", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conflicting);

        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, EntryId, "Voidstone", "Pietra del Caos", OwnerId);

        // Act
        var act = () => _handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<GlossaryTermCollisionException>();
    }

    // -------------------------------------------------------------------------
    // #2638 / SI-7 — multi-context upsert
    // -------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "2638")]
    public async Task Handle_WithContexts_ReplacesAndReturnsThemInDto()
    {
        // Arrange — existing entry seeded with a single (legacy) context.
        var legacyBook = Guid.NewGuid();
        var editing = GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto", GlossarySource.Manual, OwnerId,
            firstSeenBookId: legacyBook);
        _glossaryMock
            .Setup(r => r.GetByIdAsync(editing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editing);
        _glossaryMock
            .Setup(r => r.GetByTermItAsync(CampaignId, "Pietra del Vuoto", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);

        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, editing.Id, "Voidstone", "Pietra del Vuoto", OwnerId,
            Contexts: new List<GlossaryContextDto>
            {
                new(bookA, "§147", null),
                new(bookB, "§63", "definizione contestuale"),
            });

        // Act
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        // Assert — full-set replace; legacy context gone, both new ones present in the DTO.
        dto.Contexts.Should().HaveCount(2);
        dto.Contexts.Should().NotContain(c => c.BookId == legacyBook);
        dto.Contexts.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§147" && c.Definition == null);
        dto.Contexts.Should().ContainSingle(c =>
            c.BookId == bookB && c.ParagraphRef == "§63" && c.Definition == "definizione contestuale");
        _glossaryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Issue", "2638")]
    public async Task Handle_WithoutContexts_LeavesExistingUnchanged()
    {
        // Arrange — existing entry with a single seeded context; command carries no contexts.
        var legacyBook = Guid.NewGuid();
        var editing = GamebookGlossaryEntry.Create(
            CampaignId, "Voidstone", "Pietra del Vuoto", GlossarySource.Manual, OwnerId,
            firstSeenBookId: legacyBook);
        _glossaryMock
            .Setup(r => r.GetByIdAsync(editing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editing);
        _glossaryMock
            .Setup(r => r.GetByTermItAsync(CampaignId, "Pietra del Vuoto rev", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);

        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, editing.Id, "Voidstone", "Pietra del Vuoto rev", OwnerId,
            Contexts: null);

        // Act
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        // Assert — termIt updated, but the pre-existing context is untouched.
        dto.TermIt.Should().Be("Pietra del Vuoto rev");
        dto.Contexts.Should().ContainSingle(c => c.BookId == legacyBook);
    }

    [Fact]
    [Trait("Issue", "2638")]
    public async Task Handle_CreateWithContexts_PersistsThemOnNewEntry()
    {
        // Arrange — no existing entry (create branch).
        _glossaryMock
            .Setup(r => r.GetByIdAsync(EntryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);
        _glossaryMock
            .Setup(r => r.GetByTermItAsync(CampaignId, "Spada", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookGlossaryEntry?)null);

        var bookA = Guid.NewGuid();
        var cmd = new UpsertGlossaryEntryCommand(
            CampaignId, EntryId, "Sword", "Spada", OwnerId,
            Contexts: new List<GlossaryContextDto> { new(bookA, "§12", null) });

        // Act
        var dto = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.TermEn.Should().Be("Sword");
        dto.Contexts.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§12");
        _glossaryMock.Verify(r => r.AddAsync(It.IsAny<GamebookGlossaryEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
