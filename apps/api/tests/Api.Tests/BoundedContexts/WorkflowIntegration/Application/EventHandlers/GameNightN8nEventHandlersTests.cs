using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;
using Api.BoundedContexts.WorkflowIntegration.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Issue #1942 / iso-3 — verifies that the 3 n8n handlers propagate the originating
/// IDomainEvent.EventId as a top-level <c>domainEventId</c> field on the n8n webhook
/// payload. This is the BE-side of the n8n idempotency contract documented in
/// <c>docs/for-developers/integrations/n8n-idempotency-contract.md</c>; n8n workflows
/// dedup on the field to avoid duplicate side-effects on re-dispatched events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "WorkflowIntegration")]
public sealed class GameNightN8nEventHandlersTests
{
    private readonly Mock<IN8nWebhookClient> _clientMock = new();

    [Fact]
    public async Task GameNightPublishedN8nHandler_ForwardsDomainEventIdInPayload()
    {
        // Arrange
        var capturedPayload = await CapturePayloadAsync<GameNightPublishedEvent, GameNightPublishedN8nHandler>(
            evt => new GameNightPublishedN8nHandler(_clientMock.Object, NullLogger<GameNightPublishedN8nHandler>.Instance),
            new GameNightPublishedEvent(
                gameNightEventId: Guid.NewGuid(),
                organizerId: Guid.NewGuid(),
                title: "Friday Catan",
                scheduledAt: DateTimeOffset.UtcNow.AddDays(2),
                invitedUserIds: new List<Guid> { Guid.NewGuid() }),
            expectedPath: "game-night-published");

        // Assert — payload carries domainEventId equal to the event's EventId.
        var json = JsonSerializer.SerializeToElement(capturedPayload);
        json.TryGetProperty("domainEventId", out var domainEventIdProp).Should().BeTrue(
            "iso-3 BE-side contract requires domainEventId on every domain-event-driven n8n call");
    }

    [Fact]
    public async Task GameNightCancelledN8nHandler_ForwardsDomainEventIdInPayload()
    {
        var capturedPayload = await CapturePayloadAsync<GameNightCancelledEvent, GameNightCancelledN8nHandler>(
            evt => new GameNightCancelledN8nHandler(_clientMock.Object, NullLogger<GameNightCancelledN8nHandler>.Instance),
            new GameNightCancelledEvent(
                gameNightEventId: Guid.NewGuid(),
                organizerId: Guid.NewGuid(),
                title: "Friday Catan",
                invitedUserIds: new List<Guid> { Guid.NewGuid() }),
            expectedPath: "game-night-cancelled");

        var json = JsonSerializer.SerializeToElement(capturedPayload);
        json.TryGetProperty("domainEventId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GameNightRsvpN8nHandler_ForwardsDomainEventIdInPayload()
    {
        var capturedPayload = await CapturePayloadAsync<GameNightRsvpReceivedEvent, GameNightRsvpN8nHandler>(
            evt => new GameNightRsvpN8nHandler(_clientMock.Object, NullLogger<GameNightRsvpN8nHandler>.Instance),
            new GameNightRsvpReceivedEvent(
                gameNightEventId: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                rsvpStatus: RsvpStatus.Accepted,
                organizerId: Guid.NewGuid()),
            expectedPath: "game-night-rsvp-changed");

        var json = JsonSerializer.SerializeToElement(capturedPayload);
        json.TryGetProperty("domainEventId", out _).Should().BeTrue();
    }

    private async Task<object> CapturePayloadAsync<TEvent, THandler>(
        Func<TEvent, THandler> handlerFactory,
        TEvent notification,
        string expectedPath)
        where THandler : MediatR.INotificationHandler<TEvent>
        where TEvent : MediatR.INotification
    {
        object? captured = null;
        _clientMock
            .Setup(c => c.TriggerWorkflowAsync(expectedPath, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((_, payload, _) => captured = payload)
            .Returns(Task.CompletedTask);

        var handler = handlerFactory(notification);
        await handler.Handle(notification, CancellationToken.None);

        captured.Should().NotBeNull($"the handler must call TriggerWorkflowAsync(\"{expectedPath}\", …)");
        return captured!;
    }
}
