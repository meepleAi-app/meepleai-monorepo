using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for recording game sessions with automatic stats updates.
/// Invalidates game detail cache after the commit, best effort: if the cache backend is down the session
/// stays recorded and the request succeeds, with the entry left to expire (#4720).
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

        // Invalidate cache. The session is already saved: if dropping the entry fails (Redis, the L2 of
        // HybridCache, is down — RemoveAsync throws then) the request must not turn into an error, because the
        // client retries it and the retry would record a SECOND session (#4720). The entry stays stale until it
        // expires. A cancelled request is not a backend failure and still propagates.
        var cacheKey = $"game-detail:{command.UserId}:{command.GameId}";
        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types: post-commit, best effort by design
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Game session {SessionId} saved, but dropping cache entry {CacheKey} failed; the entry stays until it expires",
                session.Id, cacheKey);
        }
#pragma warning restore CA1031

        _logger.LogInformation("Recorded session {SessionId} for game {GameId} by user {UserId}",
            session.Id, command.GameId, command.UserId);

        // Issue #3974: Publish cache invalidation event
        await _publisher.Publish(
            new UserGameSessionCompletedEvent(command.UserId, session.Id),
            cancellationToken).ConfigureAwait(false);

        return session.Id;
    }
}
