using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Epic #3470 Slice 1c-2 — aggregate-level management of per-context cover
/// assignments (<see cref="GameCoverAssignment"/>) on the <see cref="SharedGame"/>
/// root. At most one assignment per context (mirrors the DB unique constraint);
/// mutation goes through the aggregate.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCoverAssignmentTests
{
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Admin2Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static SharedGame NewGame() => SharedGame.Create(
        title: "Catan",
        yearPublished: 1995,
        description: "Trade, build, settle",
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        minAge: 10,
        complexityRating: 2.5m,
        averageRating: 7.8m,
        imageUrl: "https://example.com/c.jpg",
        thumbnailUrl: "https://example.com/c-thumb.jpg",
        rules: null,
        createdBy: AdminId);

    [Fact]
    public void CoverAssignments_StartsEmpty()
    {
        NewGame().CoverAssignments.Should().BeEmpty();
    }

    [Fact]
    public void AssignCover_NewContext_AddsAssignmentBoundToGame()
    {
        var game = NewGame();

        var a = game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.25, 0.75);

        game.CoverAssignments.Should().ContainSingle();
        a.SharedGameId.Should().Be(game.Id);
        a.Context.Should().Be(CoverContext.Card);
        a.Source.Should().Be(CoverAssignmentSource.Wikidata);
        a.FocalX.Should().Be(0.25);
        a.FocalY.Should().Be(0.75);
        a.CreatedBy.Should().Be(AdminId);
    }

    [Fact]
    public void AssignCover_DefaultFocal_CentersTheCrop()
    {
        var game = NewGame();

        var a = game.AssignCover(CoverContext.Hero, CoverAssignmentSource.Pdf, AdminId);

        a.FocalX.Should().Be(0.5);
        a.FocalY.Should().Be(0.5);
    }

    [Fact]
    public void AssignCover_ExistingContext_UpdatesInPlace_NoDuplicate()
    {
        var game = NewGame();
        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);

        var updated = game.AssignCover(CoverContext.Card, CoverAssignmentSource.Pdf, Admin2Id, 0.1, 0.2);

        game.CoverAssignments.Should().ContainSingle("one assignment per context (unique constraint)");
        updated.Source.Should().Be(CoverAssignmentSource.Pdf);
        updated.FocalX.Should().Be(0.1);
        updated.FocalY.Should().Be(0.2);
        updated.UpdatedBy.Should().Be(Admin2Id);
    }

    [Fact]
    public void AssignCover_DistinctContexts_CoexistIndependently()
    {
        var game = NewGame();

        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        game.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);
        game.AssignCover(CoverContext.Social, CoverAssignmentSource.Bgg, AdminId);

        game.CoverAssignments.Should().HaveCount(3);
        game.CoverAssignments.Select(a => a.Context)
            .Should().BeEquivalentTo(new[] { CoverContext.Card, CoverContext.Hero, CoverContext.Social });
    }

    [Fact]
    public void RemoveCoverAssignment_Existing_RemovesOnlyThatContext()
    {
        var game = NewGame();
        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        game.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);

        var removed = game.RemoveCoverAssignment(CoverContext.Card);

        removed.Should().BeTrue();
        game.CoverAssignments.Should().ContainSingle();
        game.CoverAssignments.Single().Context.Should().Be(CoverContext.Hero);
    }

    [Fact]
    public void RemoveCoverAssignment_Absent_IsNoOpReturningFalse()
    {
        var game = NewGame();
        game.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);

        var removed = game.RemoveCoverAssignment(CoverContext.Card);

        removed.Should().BeFalse();
        game.CoverAssignments.Should().ContainSingle();
    }

    [Fact]
    public void AssignCover_RejectsEmptyAdminId()
    {
        var game = NewGame();

        var act = () => game.AssignCover(CoverContext.Card, CoverAssignmentSource.Pdf, Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignCover_NoOpResave_KeepsRenderedCropAndDoesNotRestamp()
    {
        // A duplicate resave with identical source + focal must be idempotent: it
        // must NOT wipe an already-rendered crop nor bump the audit stamp (which
        // would force a needless re-render and imply a change that never happened).
        var game = NewGame();
        var a = game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.3, 0.7);
        a.SetGeneratedKey("covers/card/crop.webp");

        var again = game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, Admin2Id, 0.3, 0.7);

        again.Should().BeSameAs(a);
        again.GeneratedR2Key.Should().Be("covers/card/crop.webp", "a no-op resave must not wipe the rendered crop");
        again.UpdatedBy.Should().BeNull("a no-op resave must not stamp an updater");
    }

    [Fact]
    public void AssignCover_ExistingContext_FocalChangeStillInvalidatesCrop()
    {
        // Guard the guard: a real change (focal moved) MUST still clear the crop.
        var game = NewGame();
        var a = game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.3, 0.7);
        a.SetGeneratedKey("covers/card/crop.webp");

        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, Admin2Id, 0.9, 0.1);

        a.GeneratedR2Key.Should().BeNull("moving the focal point invalidates the stale crop");
        a.UpdatedBy.Should().Be(Admin2Id);
    }
}
