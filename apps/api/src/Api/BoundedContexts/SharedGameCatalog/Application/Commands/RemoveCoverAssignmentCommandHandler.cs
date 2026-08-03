using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 — removes a context's cover assignment (resetting it to the resolver's
/// implicit precedence) through the aggregate + reconcile path. Removing an absent
/// assignment is an idempotent no-op; only a missing game is a 404.
///
/// Slice 2 (AC-6): evicts the read-model caches (list + detail) across replicas after
/// the change so the reverted cover renders immediately instead of waiting for the TTL.
/// </summary>
internal sealed class RemoveCoverAssignmentCommandHandler : ICommandHandler<RemoveCoverAssignmentCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<RemoveCoverAssignmentCommandHandler> _logger;

    public RemoveCoverAssignmentCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ILogger<RemoveCoverAssignmentCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(RemoveCoverAssignmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        game.RemoveCoverAssignment(command.Context);

        await _repository.ReconcileCoverAssignmentsAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await CoverCacheInvalidation
            .EvictReadModelAsync(_cache, command.GameId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Removed cover assignment for {Context} on game {GameId} by {AdminId}",
            command.Context, command.GameId, command.AdminId);

        return Unit.Value;
    }
}
