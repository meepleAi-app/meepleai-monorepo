using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Domain unit tests for <see cref="GameCoverAssignment"/> (epic #3470, Slice 1a).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GameCoverAssignmentTests
{
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Admin2Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public void Create_WithDefaults_CentersFocalAndLeavesCropUnrendered()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Hero, CoverAssignmentSource.Wikidata, AdminId);

        a.SharedGameId.Should().Be(GameId);
        a.Context.Should().Be(CoverContext.Hero);
        a.Source.Should().Be(CoverAssignmentSource.Wikidata);
        a.FocalX.Should().Be(0.5);
        a.FocalY.Should().Be(0.5);
        a.GeneratedR2Key.Should().BeNull();
        a.CreatedBy.Should().Be(AdminId);
        a.UpdatedAt.Should().BeNull();
        a.UpdatedBy.Should().BeNull();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "dddddddd-dddd-dddd-dddd-dddddddddddd")]
    [InlineData("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "00000000-0000-0000-0000-000000000000")]
    public void Create_RejectsEmptyIds(string gameId, string adminId)
    {
        var act = () => GameCoverAssignment.Create(
            Guid.Parse(gameId), CoverContext.Card, CoverAssignmentSource.Pdf, Guid.Parse(adminId));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-0.01, 0.5)]
    [InlineData(0.5, 1.01)]
    public void Create_RejectsOutOfRangeFocalPoint(double x, double y)
    {
        var act = () => GameCoverAssignment.Create(
            GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId, x, y);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_RejectsNonFiniteFocalPoint()
    {
        var actNan = () => GameCoverAssignment.Create(
            GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId, double.NaN, 0.5);
        var actInf = () => GameCoverAssignment.Create(
            GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId, 0.5, double.PositiveInfinity);

        actNan.Should().Throw<ArgumentOutOfRangeException>();
        actInf.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_RejectsUndefinedEnumValues()
    {
        var actContext = () => GameCoverAssignment.Create(
            GameId, (CoverContext)99, CoverAssignmentSource.Pdf, AdminId);
        var actSource = () => GameCoverAssignment.Create(
            GameId, CoverContext.Card, (CoverAssignmentSource)99, AdminId);

        actContext.Should().Throw<ArgumentException>();
        actSource.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeSource_ClearsGeneratedKeyAndStampsUpdater()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);
        a.SetGeneratedKey("covers/manual/x/cover.webp");

        a.ChangeSource(CoverAssignmentSource.Manual, Admin2Id);

        a.Source.Should().Be(CoverAssignmentSource.Manual);
        a.GeneratedR2Key.Should().BeNull("changing source invalidates the stale rendered crop");
        a.UpdatedAt.Should().NotBeNull();
        a.UpdatedBy.Should().Be(Admin2Id);
    }

    [Fact]
    public void ChangeSource_ToSameSource_IsNoOp()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);
        a.SetGeneratedKey("k.webp");

        a.ChangeSource(CoverAssignmentSource.Pdf, Admin2Id);

        a.GeneratedR2Key.Should().Be("k.webp", "a no-op source change must not clear the crop");
        a.UpdatedBy.Should().BeNull("a no-op change must not stamp an updater");
    }

    [Fact]
    public void ChangeSource_RejectsUndefinedSourceAndEmptyUpdater()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);

        var actEnum = () => a.ChangeSource((CoverAssignmentSource)99, Admin2Id);
        var actUpdater = () => a.ChangeSource(CoverAssignmentSource.Wikidata, Guid.Empty);

        actEnum.Should().Throw<ArgumentException>();
        actUpdater.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetFocalPoint_UpdatesClearsGeneratedKeyAndStampsUpdater()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Hero, CoverAssignmentSource.Bgg, AdminId);
        a.SetGeneratedKey("k.webp");

        a.SetFocalPoint(0.25, 0.75, Admin2Id);

        a.FocalX.Should().Be(0.25);
        a.FocalY.Should().Be(0.75);
        a.GeneratedR2Key.Should().BeNull();
        a.UpdatedBy.Should().Be(Admin2Id);
    }

    [Fact]
    public void SetGeneratedKey_RejectsBlank()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);

        var act = () => a.SetGeneratedKey("  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetGeneratedKey_IsSystemOp_DoesNotStampUpdater()
    {
        var a = GameCoverAssignment.Create(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId);

        a.SetGeneratedKey("covers/manual/x/cover.webp");

        a.UpdatedAt.Should().NotBeNull("the render pipeline still stamps the timestamp");
        a.UpdatedBy.Should().BeNull("a system render is not attributed to an admin");
    }
}
