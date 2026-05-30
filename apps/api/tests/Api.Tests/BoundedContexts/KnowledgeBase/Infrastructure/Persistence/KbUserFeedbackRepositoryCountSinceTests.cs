using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1655")]
public sealed class KbUserFeedbackRepositoryCountSinceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static KbUserFeedback MakeFeedback(DateTime createdAt)
    {
        var fb = KbUserFeedback.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            chatSessionId: Guid.NewGuid(),
            messageId: Guid.NewGuid(),
            outcome: "helpful",
            comment: null);
        var prop = typeof(KbUserFeedback).GetProperty(nameof(KbUserFeedback.CreatedAt))!;
        prop.SetValue(fb, createdAt);
        return fb;
    }

    [Fact]
    public async Task CountSinceAsync_NoFeedback_ReturnsZero()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repo = new KbUserFeedbackRepository(db);

        var count = await repo.CountSinceAsync(DateTime.UtcNow.AddDays(-7), Ct);

        count.Should().Be(0);
    }

    [Fact]
    public async Task CountSinceAsync_IncludesFeedbackOnAndAfterSince()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-7);

        db.KbUserFeedbacks.AddRange(
            MakeFeedback(now),                          // within window
            MakeFeedback(now.AddDays(-3)),              // within window
            MakeFeedback(since),                        // exactly at boundary -> included
            MakeFeedback(now.AddDays(-10)),             // before window
            MakeFeedback(now.AddDays(-30)));            // before window
        await db.SaveChangesAsync(Ct);

        var repo = new KbUserFeedbackRepository(db);
        var count = await repo.CountSinceAsync(since, Ct);

        count.Should().Be(3);
    }

    [Fact]
    public async Task CountSinceAsync_AllOutcomesCount()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var now = DateTime.UtcNow;

        var helpful = MakeFeedback(now.AddDays(-1));
        var notHelpful = KbUserFeedback.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "not_helpful", null);
        typeof(KbUserFeedback).GetProperty(nameof(KbUserFeedback.CreatedAt))!
            .SetValue(notHelpful, now.AddDays(-2));

        db.KbUserFeedbacks.AddRange(helpful, notHelpful);
        await db.SaveChangesAsync(Ct);

        var repo = new KbUserFeedbackRepository(db);
        var count = await repo.CountSinceAsync(now.AddDays(-7), Ct);

        count.Should().Be(2);
    }
}
