using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;

/// <summary>
/// Handler for RecordGameEventCommand. Persists game analytics events.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed class RecordGameEventCommandHandler : IRequestHandler<RecordGameEventCommand>
{
    private readonly IGameAnalyticsEventRepository _repository;
    private readonly ILogger<RecordGameEventCommandHandler> _logger;

    public RecordGameEventCommandHandler(
        IGameAnalyticsEventRepository repository,
        ILogger<RecordGameEventCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RecordGameEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var analyticsEvent = GameAnalyticsEvent.Record(
            command.GameId,
            command.EventType,
            command.UserId);

        await _repository.AddAsync(analyticsEvent, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Recorded game analytics event: GameId={GameId}, Type={EventType}, UserId={UserId}",
            command.GameId,
            command.EventType,
            command.UserId);
    }
}
