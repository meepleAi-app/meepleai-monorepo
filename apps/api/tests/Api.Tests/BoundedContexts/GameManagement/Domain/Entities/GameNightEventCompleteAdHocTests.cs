using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="GameNightEvent.CompleteAdHoc"/> domain method
/// (Session Flow v2.1 — Plan 1bis T3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Feature", "SessionFlowV2.1")]
public class GameNightEventCompleteAdHocTests
{
    [Fact]
    public void CompleteAdHoc_WhenInProgress_TransitionsToCompleted()
    {
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", Guid.NewGuid());

        night.CompleteAdHoc();

        night.Status.Should().Be(GameNightStatus.Completed);
        night.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void CompleteAdHoc_WhenDraft_Throws()
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(),
            "Pianificata",
            DateTimeOffset.UtcNow.AddDays(1));

        var act = () => night.CompleteAdHoc();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public void CompleteAdHoc_WhenPublished_Throws()
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(),
            "Pianificata",
            DateTimeOffset.UtcNow.AddDays(1));
        night.Publish(new List<Guid> { Guid.NewGuid() });

        var act = () => night.CompleteAdHoc();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Published*");
    }

    [Fact]
    public void CompleteAdHoc_WhenCancelled_Throws()
    {
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", Guid.NewGuid());
        // Cancel requires non-completed; ad-hoc InProgress can be cancelled
        night.Cancel();

        var act = () => night.CompleteAdHoc();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cancelled*");
    }

    [Fact]
    public void CompleteAdHoc_WhenAlreadyCompleted_Throws()
    {
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", Guid.NewGuid());
        night.CompleteAdHoc();

        var act = () => night.CompleteAdHoc();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Completed*");
    }

    [Fact]
    public void CompleteAdHoc_Idempotent_SetsUpdatedAtToUtcNow()
    {
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", Guid.NewGuid());

        night.CompleteAdHoc();

        night.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(2));
    }
}
