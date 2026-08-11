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

    // ── #3615: the base cover changing underneath a rendered crop ────────────────────────
    //
    // ChangeSource/SetFocalPoint cover «the admin changed their mind». The opposite direction —
    // nobody touches the assignment, but the pipeline regenerates the base image — left the crop
    // pointing at the old picture indefinitely, because nothing regenerates it. Clearing the key
    // makes the resolver fall back to the NEW base cover instead of serving a stale crop.

    [Fact]
    public void SetPdfCoverR2Key_NewKey_InvalidatesCropsPinnedToPdf()
    {
        var game = NewGame();
        game.SetPdfCoverR2Key("covers/g/cover-preview");
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Pdf, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetPdfCoverR2Key("covers/g/cover-preview-v2");

        a.GeneratedR2Key.Should().BeNull("the image the crop was rendered from no longer exists");
    }

    [Fact]
    public void SetBggCover_NewKey_InvalidatesCropsPinnedToBgg()
    {
        var game = NewGame();
        game.SetBggCover("covers/g/bgg");
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Bgg, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetBggCover("covers/g/bgg-v2");

        a.GeneratedR2Key.Should().BeNull();
    }

    [Fact]
    public void SetPdfCoverR2Key_SameKey_KeepsTheCrop()
    {
        // Re-writing the identical key is a no-op upstream (the event handler is idempotent):
        // discarding a valid crop there would degrade the Social rendering for nothing.
        var game = NewGame();
        game.SetPdfCoverR2Key("covers/g/cover-preview");
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Pdf, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetPdfCoverR2Key("covers/g/cover-preview");

        a.GeneratedR2Key.Should().Be("covers/crops/g/social.webp");
    }

    [Fact]
    public void SetPdfCoverR2Key_DoesNotTouchCropsPinnedToAnotherSource()
    {
        // The PDF pipeline regenerating its own image says nothing about a crop rendered from the
        // Wikidata cover: invalidating it would throw away a perfectly current file.
        var game = NewGame();
        game.SetWikidataCover("covers/g/cover", "CC BY-SA 4.0", null, "https://www.wikidata.org/entity/Q1", DateTime.UtcNow);
        var wikidataCrop = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Wikidata, AdminId);
        wikidataCrop.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetPdfCoverR2Key("covers/g/cover-preview");

        wikidataCrop.GeneratedR2Key.Should().Be("covers/crops/g/social.webp");
    }

    [Fact]
    public void SetWikidataCover_SameImageReVerified_KeepsTheCrop()
    {
        // SetWikidataCover has no idempotency guard: the quarterly re-verification calls it with
        // the same key just to refresh verifiedAt. That must not cost the crop.
        var game = NewGame();
        game.SetWikidataCover("covers/g/cover", "CC BY-SA 4.0", null, "https://www.wikidata.org/entity/Q1", DateTime.UtcNow);
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Wikidata, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetWikidataCover("covers/g/cover", "CC BY-SA 4.0", null, "https://www.wikidata.org/entity/Q1", DateTime.UtcNow.AddDays(90));

        a.GeneratedR2Key.Should().Be("covers/crops/g/social.webp", "only a NEW image invalidates the crop");
    }

    [Fact]
    public void SetWikidataCover_NewImage_InvalidatesTheCrop()
    {
        var game = NewGame();
        game.SetWikidataCover("covers/g/cover", "CC BY-SA 4.0", null, "https://www.wikidata.org/entity/Q1", DateTime.UtcNow);
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Wikidata, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetWikidataCover("covers/g/cover-v2", "CC BY-SA 4.0", null, "https://www.wikidata.org/entity/Q1", DateTime.UtcNow);

        a.GeneratedR2Key.Should().BeNull();
    }

    [Fact]
    public void SetManualCover_NewImage_InvalidatesTheCrop()
    {
        var game = NewGame();
        game.SetManualCover("covers/g/manual", "CC0", null, "https://example.com/a", AdminId, DateTime.UtcNow);
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Manual, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetManualCover("covers/g/manual-v2", "CC0", null, "https://example.com/b", AdminId, DateTime.UtcNow);

        a.GeneratedR2Key.Should().BeNull();
    }

    [Fact]
    public void SetManualCover_SameImageReAttested_KeepsTheCrop()
    {
        var game = NewGame();
        game.SetManualCover("covers/g/manual", "CC0", null, "https://example.com/a", AdminId, DateTime.UtcNow);
        var a = game.AssignCover(CoverContext.Social, CoverAssignmentSource.Manual, AdminId);
        a.SetGeneratedKey("covers/crops/g/social.webp");

        game.SetManualCover("covers/g/manual", "CC BY 4.0", "Artist", "https://example.com/a", Admin2Id, DateTime.UtcNow);

        a.GeneratedR2Key.Should().Be("covers/crops/g/social.webp");
    }
}
