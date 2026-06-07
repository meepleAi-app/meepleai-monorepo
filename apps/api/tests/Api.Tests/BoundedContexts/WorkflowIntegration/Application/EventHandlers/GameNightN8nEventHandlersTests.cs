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
        var evt = new GameNightPublishedEvent(
            gameNightEventId: Guid.NewGuid(),
            organizerId: Guid.NewGuid(),
            title: "Friday Catan",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(2),
            invitedUserIds: new List<Guid> { Guid.NewGuid() });

        var capturedPayload = await CapturePayloadAsync<GameNightPublishedEvent, GameNightPublishedN8nHandler>(
            _ => new GameNightPublishedN8nHandler(_clientMock.Object, NullLogger<GameNightPublishedN8nHandler>.Instance),
            evt,
            expectedPath: "game-night-published");

        // Assert — payload carries domainEventId equal to the originating IDomainEvent.EventId.
        // Presence alone is insufficient: a refactor that forwarded Guid.Empty (or a fresh Guid
        // unrelated to the event) would defeat n8n-side dedup. iso-3 contract requires identity.
        AssertDomainEventIdMatches(capturedPayload, evt.EventId);
    }

    [Fact]
    public async Task GameNightCancelledN8nHandler_ForwardsDomainEventIdInPayload()
    {
        var evt = new GameNightCancelledEvent(
            gameNightEventId: Guid.NewGuid(),
            organizerId: Guid.NewGuid(),
            title: "Friday Catan",
            invitedUserIds: new List<Guid> { Guid.NewGuid() });

        var capturedPayload = await CapturePayloadAsync<GameNightCancelledEvent, GameNightCancelledN8nHandler>(
            _ => new GameNightCancelledN8nHandler(_clientMock.Object, NullLogger<GameNightCancelledN8nHandler>.Instance),
            evt,
            expectedPath: "game-night-cancelled");

        AssertDomainEventIdMatches(capturedPayload, evt.EventId);
    }

    [Fact]
    public async Task GameNightRsvpN8nHandler_ForwardsDomainEventIdInPayload()
    {
        var evt = new GameNightRsvpReceivedEvent(
            gameNightEventId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            rsvpStatus: RsvpStatus.Accepted,
            organizerId: Guid.NewGuid());

        var capturedPayload = await CapturePayloadAsync<GameNightRsvpReceivedEvent, GameNightRsvpN8nHandler>(
            _ => new GameNightRsvpN8nHandler(_clientMock.Object, NullLogger<GameNightRsvpN8nHandler>.Instance),
            evt,
            expectedPath: "game-night-rsvp-changed");

        AssertDomainEventIdMatches(capturedPayload, evt.EventId);
    }

    private static void AssertDomainEventIdMatches(object payload, Guid expectedEventId)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        json.TryGetProperty("domainEventId", out var prop).Should().BeTrue(
            "iso-3 BE-side contract requires domainEventId on every domain-event-driven n8n call");
        prop.GetGuid().Should().Be(expectedEventId,
            "domainEventId MUST equal the originating IDomainEvent.EventId so n8n can dedup on it");
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
