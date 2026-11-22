using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.UserNotifications.Application.EventHandlers;

/// <summary>
/// #5112: the dispatcher keeps one notification per (recipient, SourceEventId). This handler sent the monthly and the
/// pending warning for the same event, to the same user, with the same SourceEventId: when a request crossed 80% of
/// both limits, the second warning was dropped as a duplicate. One warning per event, naming every limit it nears.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
[Trait("Issue", "5112")]
public sealed class RateLimitApproachingHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IRateLimitEvaluator> _evaluator = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly List<NotificationMessage> _sent = [];

    public RateLimitApproachingHandlerTests()
    {
        _dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((message, _) => _sent.Add(message))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task NearingBothLimits_IsOneWarning_ThatNamesBoth()
    {
        var created = await HandleAsync(monthly: 8, pending: 4);

        var warning = _sent.Should().ContainSingle().Subject;
        warning.SourceEventId.Should().Be(created.EventId);
        var payload = warning.Payload.Should().BeOfType<GenericPayload>().Subject;
        payload.Title.Should().Be("Approaching Your Limits");
        payload.Body.Should().Contain("8 of your 10 monthly share requests").And.Contain("4 pending requests");
        warning.DeepLinkPath.Should().Be(NotificationRoutes.ContributionsPending);
    }

    [Fact]
    public async Task NearingTheMonthlyLimit_IsTheMonthlyWarning()
    {
        await HandleAsync(monthly: 8, pending: 0);

        var warning = _sent.Should().ContainSingle().Subject;
        warning.Payload.Should().BeOfType<GenericPayload>().Which.Title.Should().Be("Approaching Monthly Limit");
        warning.DeepLinkPath.Should().Be(NotificationRoutes.Contributions);
    }

    [Fact]
    public async Task NearingThePendingLimit_IsThePendingWarning()
    {
        await HandleAsync(monthly: 0, pending: 4);

        var warning = _sent.Should().ContainSingle().Subject;
        warning.Payload.Should().BeOfType<GenericPayload>().Which.Title.Should().Be("Approaching Pending Limit");
        warning.DeepLinkPath.Should().Be(NotificationRoutes.ContributionsPending);
    }

    [Theory]
    [InlineData(7, 3)] // below 80% of both
    [InlineData(10, 5)] // at the limit: the request was refused, not warned about
    public async Task AwayFromEitherThreshold_SendsNothing(int monthly, int pending)
    {
        await HandleAsync(monthly, pending);

        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AUserWithUnlimitedAccess_IsNeverWarned()
    {
        await HandleAsync(monthly: 8, pending: 4, tier: UserTier.Admin);

        _sent.Should().BeEmpty();
    }

    private async Task<ShareRequestCreatedEvent> HandleAsync(int monthly, int pending, UserTier tier = UserTier.Free)
    {
        _evaluator
            .Setup(e => e.GetUserStatusAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitStatus
            {
                UserId = UserId,
                Tier = tier,
                CurrentMonthlyCount = monthly,
                EffectiveMaxPerMonth = 10,
                CurrentPendingCount = pending,
                EffectiveMaxPending = 5,
                MonthResetAt = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        var created = new ShareRequestCreatedEvent(Guid.NewGuid(), UserId, Guid.NewGuid(), ContributionType.NewGame);

        var handler = new RateLimitApproachingHandler(
            _evaluator.Object, _dispatcher.Object, NullLogger<RateLimitApproachingHandler>.Instance);
        await handler.Handle(created, TestContext.Current.CancellationToken);

        return created;
    }
}
