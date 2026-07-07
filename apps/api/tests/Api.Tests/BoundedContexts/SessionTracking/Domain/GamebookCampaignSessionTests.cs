using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookCampaignSessionTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static GameRef SharedRef() => GameRef.Shared(GameId);

    [Fact]
    public void Create_WithValidInputs_SetsCoreFields()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "Campagna Nanolith #1");

        session.GameRef.Id.Should().Be(GameId);
        session.GameRef.Kind.Should().Be(GameRefKind.Shared);
        session.OwnerUserId.Should().Be(OwnerId);
        session.Title.Should().Be("Campagna Nanolith #1");
        session.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyTitle_Throws()
    {
        Action act = () => GamebookCampaignSession.Create(SharedRef(), OwnerId, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Touch_StampsUpdatedAtAndUpdatedBy()
    {
        // C2 (2026-05-19): per-paragraph progress moved to SessionBookProgress.
        // The aggregate exposes only Touch() to mark user activity on the parent row.
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        var before = session.UpdatedAt;
        Thread.Sleep(50);

        session.Touch(updatedBy: OwnerId);

        session.UpdatedAt.Should().BeAfter(before);
        session.UpdatedBy.Should().Be(OwnerId);
    }

    [Fact]
    public void SoftDelete_FlagsAsDeletedWithTimestamp()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        session.SoftDelete(deletedBy: OwnerId);

        session.IsDeleted.Should().BeTrue();
        session.DeletedAt.Should().NotBeNull();
    }

    // ── A0.2 (#1320): GameRef discriminator tests ─────────────────────────────

    [Fact]
    public void Create_WithGameRef_StoresAsDiscriminator()
    {
        var sharedId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(
            gameRef: GameRef.Shared(sharedId),
            ownerUserId: Guid.NewGuid(),
            title: "Campagna 1");

        Assert.Equal(GameRefKind.Shared, session.GameRef.Kind);
        Assert.Equal(sharedId, session.GameRef.Id);
    }

    [Fact]
    public void Create_WithPrivateGameRef_StoresAsDiscriminator()
    {
        var privateId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(
            gameRef: GameRef.Private(privateId),
            ownerUserId: Guid.NewGuid(),
            title: "Campagna privata");

        Assert.Equal(GameRefKind.Private, session.GameRef.Kind);
        Assert.Equal(privateId, session.GameRef.Id);
    }

    // ── SI-8 (#2639): manual campaign close (Completa / Abbandona terminal) ────

    [Fact]
    public void Create_LeavesCampaignOpen_NoOutcome()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");

        session.Outcome.Should().BeNull();
        session.CompletedAt.Should().BeNull();
        session.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Close_WithCompleted_SetsOutcomeCompletedAtAndAudit()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        var before = session.UpdatedAt;
        Thread.Sleep(20);

        session.Close(GamebookCampaignOutcome.Completed, closedBy: OwnerId);

        session.Outcome.Should().Be(GamebookCampaignOutcome.Completed);
        session.CompletedAt.Should().NotBeNull();
        session.IsClosed.Should().BeTrue();
        session.UpdatedAt.Should().BeAfter(before);
        session.UpdatedBy.Should().Be(OwnerId);
    }

    [Fact]
    public void Close_WithAbandoned_SetsOutcome()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");

        session.Close(GamebookCampaignOutcome.Abandoned, closedBy: OwnerId);

        session.Outcome.Should().Be(GamebookCampaignOutcome.Abandoned);
        session.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsConflict()
    {
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        session.Close(GamebookCampaignOutcome.Completed, closedBy: OwnerId);

        Action act = () => session.Close(GamebookCampaignOutcome.Abandoned, closedBy: OwnerId);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Close_DoesNotSoftDelete()
    {
        // Archivia is the resumable default (no Close call); Completa/Abbandona are
        // terminal but the campaign row is NOT soft-deleted — it stays queryable so
        // the resume picker can list it under a "completed" section.
        var session = GamebookCampaignSession.Create(SharedRef(), OwnerId, "C1");
        session.Close(GamebookCampaignOutcome.Completed, closedBy: OwnerId);

        session.IsDeleted.Should().BeFalse();
    }
}
