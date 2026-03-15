using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class SlackMessageBuilderFactoryTests
{
    private readonly IConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public SlackMessageBuilderFactoryTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();

        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void GetBuilder_ShareRequestType_ReturnsShareRequestBuilder()
    {
        // Arrange
        var shareBuilder = new ShareRequestSlackBuilder(_config, _timeProvider);
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { shareBuilder, new BadgeSlackBuilder() },
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.ShareRequestCreated);

        // Assert
        builder.Should().BeSameAs(shareBuilder);
    }

    [Fact]
    public void GetBuilder_GameNightType_ReturnsGameNightBuilder()
    {
        // Arrange
        var gnBuilder = new GameNightSlackBuilder(_timeProvider);
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { gnBuilder },
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.GameNightInvitation);

        // Assert
        builder.Should().BeSameAs(gnBuilder);
    }

    [Fact]
    public void GetBuilder_BadgeType_ReturnsBadgeBuilder()
    {
        // Arrange
        var badgeBuilder = new BadgeSlackBuilder();
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { badgeBuilder },
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.BadgeEarned);

        // Assert
        builder.Should().BeSameAs(badgeBuilder);
    }

    [Fact]
    public void GetBuilder_UnknownType_FallsBackToGenericBuilder()
    {
        // Arrange
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { new BadgeSlackBuilder() },
            fallback);

        // Act — NewComment has no specific builder
        var builder = factory.GetBuilder(NotificationType.NewComment);

        // Assert
        builder.Should().BeSameAs(fallback);
    }

    [Fact]
    public void GetBuilder_AdminType_ReturnsAdminAlertBuilder()
    {
        // Arrange
        var adminBuilder = new AdminAlertSlackBuilder();
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { adminBuilder },
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.AdminCircuitBreakerStateChanged);

        // Assert
        builder.Should().BeSameAs(adminBuilder);
    }

    [Fact]
    public void GetBuilder_PdfType_ReturnsPdfProcessingBuilder()
    {
        // Arrange
        var pdfBuilder = new PdfProcessingSlackBuilder(_config);
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            new ISlackMessageBuilder[] { pdfBuilder },
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.PdfUploadCompleted);

        // Assert
        builder.Should().BeSameAs(pdfBuilder);
    }

    [Fact]
    public void GetBuilder_WithEmptyBuilderList_AlwaysFallsBack()
    {
        // Arrange
        var fallback = new GenericSlackBuilder(_config);
        var factory = new SlackMessageBuilderFactory(
            Array.Empty<ISlackMessageBuilder>(),
            fallback);

        // Act
        var builder = factory.GetBuilder(NotificationType.ShareRequestCreated);

        // Assert
        builder.Should().BeSameAs(fallback);
    }
}
