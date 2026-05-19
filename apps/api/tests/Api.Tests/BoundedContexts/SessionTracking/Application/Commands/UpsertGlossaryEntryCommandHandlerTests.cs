using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Exceptions;
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
        var campaign = GamebookCampaignSession.Create(Guid.NewGuid(), OwnerId, "Test campaign");
        _campaignsMock
            .Setup(r => r.GetByIdAsync(CampaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        _handler = new UpsertGlossaryEntryCommandHandler(
            _campaignsMock.Object,
            _glossaryMock.Object);
    }

    private static GamebookGlossaryEntry MakeEntry(string termEn, string termIt, Guid? id = null)
    {
        var entry = GamebookGlossaryEntry.Create(
            CampaignId,
            termEn,
            termIt,
            GlossarySource.Manual,
            OwnerId);
        if (id.HasValue)
        {
            // The factory generates an Id; integration tests can't rebind it, but for
            // unit tests we accept what `Create` gave us and pass it through the mock.
        }
        return entry;
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
}
