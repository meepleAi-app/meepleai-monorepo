using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

/// <summary>
/// #4401: the PDF import wizard creates the game as a skeleton and fills it with the metadata the
/// admin reviewed. The generic transitions keep a skeleton away from Complete
/// (<c>SkeletonGame_CannotTransitionDirectlyToComplete</c>); this is the one named way through.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4401")]
public sealed class SharedGameCompleteFromReviewedImportTests
{
    private static readonly Guid AdminUserId = Guid.NewGuid();

    [Fact]
    public void AFilledSkeleton_BecomesComplete()
    {
        var game = FilledSkeleton();

        game.CompleteFromReviewedImport(AdminUserId);

        game.GameDataStatus.Should().Be(GameDataStatus.Complete);
        game.ModifiedBy.Should().Be(AdminUserId);
    }

    [Fact]
    public void ASkeletonStillHoldingItsZeroDefaults_IsNotComplete()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System);

        var act = () => game.CompleteFromReviewedImport(AdminUserId);

        act.Should().Throw<ArgumentException>();
        game.GameDataStatus.Should().Be(GameDataStatus.Skeleton);
    }

    [Fact]
    public void AGameThatIsNotASkeleton_IsRefused()
    {
        var game = FilledSkeleton();
        game.CompleteFromReviewedImport(AdminUserId);

        var act = () => game.CompleteFromReviewedImport(AdminUserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Complete*");
    }

    [Fact]
    public void WithoutAnAuthor_IsRefused()
    {
        var game = FilledSkeleton();

        var act = () => game.CompleteFromReviewedImport(Guid.Empty);

        act.Should().Throw<ArgumentException>();
        game.GameDataStatus.Should().Be(GameDataStatus.Skeleton);
    }

    private static SharedGame FilledSkeleton()
    {
        var game = SharedGame.CreateSkeleton("Catan", AdminUserId, TimeProvider.System);
        game.UpdateInfo(
            "Catan", 1995, "Trade and build on the island of Catan.",
            3, 4, 90, 10, null, null, string.Empty, string.Empty, null, AdminUserId);
        return game;
    }
}
