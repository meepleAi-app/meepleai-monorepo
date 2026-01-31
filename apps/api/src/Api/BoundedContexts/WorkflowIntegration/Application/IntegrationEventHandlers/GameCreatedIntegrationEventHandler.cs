using Api.BoundedContexts.GameManagement.Application.IntegrationEvents;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.IntegrationEventHandlers;

/// <summary>
/// Handles GameCreatedIntegrationEvent from GameManagement context.
/// Triggers workflow setup for new games (e.g., n8n workflows for game notifications).
/// </summary>
internal sealed class GameCreatedIntegrationEventHandler : INotificationHandler<GameCreatedIntegrationEvent>
{
    private readonly ILogger<GameCreatedIntegrationEventHandler> _logger;

    public GameCreatedIntegrationEventHandler(ILogger<GameCreatedIntegrationEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task Handle(GameCreatedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogInformation(
            "Handling GameCreatedIntegrationEvent from {SourceContext}: GameId={GameId}, Name={GameName}",
            notification.SourceContext,
            notification.GameId,
            notification.GameName);

        // Future: Trigger n8n workflows for game setup
        // - Create game notification workflow
        // - Setup game-specific webhook subscriptions
        // - Initialize game monitoring

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
