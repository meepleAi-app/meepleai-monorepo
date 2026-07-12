using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardApplyFeedbackAggregatesTests
{
    private static MechanicCard NewCard() => MechanicCard.Reconstitute(
        id: Guid.NewGuid(), sharedGameId: Guid.NewGuid(), originAnalysisId: Guid.NewGuid(),
        origin: MechanicCardOrigin.AiReviewed, title: "Catan — Comprehension Card", content: "{}",
        version: 1, isSuppressed: false, suppressedReason: null, suppressedAt: null, suppressedBy: null,
        errorReportsCount: 0, feedbackScore: null,
        publishedAt: DateTime.UtcNow, publishedBy: Guid.NewGuid(),
        createdAt: DateTime.UtcNow, updatedAt: DateTime.UtcNow, xminVersion: 0);

    [Fact]
    public void ApplyFeedbackAggregates_SetsCountScoreAndUpdatedAt()
    {
        var card = NewCard();
        var now = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);

        card.ApplyFeedbackAggregates(errorReportsCount: 5, feedbackScore: 0.42m, utcNow: now);

        card.ErrorReportsCount.Should().Be(5);
        card.FeedbackScore.Should().Be(0.42m);
        card.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void ApplyFeedbackAggregates_AllowsNullScore_WhenNoFeedback()
    {
        var card = NewCard();
        card.ApplyFeedbackAggregates(0, null, DateTime.UtcNow);
        card.FeedbackScore.Should().BeNull();
        card.ErrorReportsCount.Should().Be(0);
    }

    [Fact]
    public void ApplyFeedbackAggregates_Throws_WhenCountNegative()
    {
        var card = NewCard();
        var act = () => card.ApplyFeedbackAggregates(-1, null, DateTime.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
