using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for recording game sessions with automatic stats updates.
/// Invalidates game detail cache on success.
/// </summary>
internal class RecordGameSessionCommandHandler : ICommandHandler<RecordGameSessionCommand, Guid>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;
    private readonly IPublisher _publisher;
    private readonly ILogger<RecordGameSessionCommandHandler> _logger;

    public RecordGameSessionCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        HybridCache cache,
        IPublisher publisher,
        ILogger<RecordGameSessionCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(RecordGameSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Game {command.GameId} not found in your library");

        // Record session (domain handles stats update and event emission)
        var session = entry.RecordGameSession(
            playedAt: command.PlayedAt,
            durationMinutes: command.DurationMinutes,
            didWin: command.DidWin,
            players: command.Players,
            notes: command.Notes);

        // Persist changes
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        var cacheKey = $"game-detail:{command.UserId}:{command.GameId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Recorded session {SessionId} for game {GameId} by user {UserId}",
            session.Id, command.GameId, command.UserId);

        // Issue #3974: Publish cache invalidation event
        await _publisher.Publish(
            new UserGameSessionCompletedEvent(command.UserId, session.Id),
            cancellationToken).ConfigureAwait(false);

        return session.Id;
    }
}
