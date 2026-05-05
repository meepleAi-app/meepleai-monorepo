using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Handler for <see cref="DeactivateMechanicGoldenClaimCommand"/> (ADR-051 Sprint 1 / Task 21).
/// Loads a <see cref="MechanicGoldenClaim"/> by id, invokes the aggregate's synchronous
/// <see cref="MechanicGoldenClaim.Deactivate"/> method, persists the soft-delete through the
/// repository / unit-of-work, and invalidates the per-game golden-set cache key so downstream
/// scoring readers stop observing the deactivated claim immediately.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="UpdateMechanicGoldenClaimHandler"/> this handler does not need embedding or
/// keyword services — <see cref="MechanicGoldenClaim.Deactivate"/> only mutates
/// <see cref="MechanicGoldenClaim.DeletedAt"/>.
/// </para>
/// <para>
/// Throws <see cref="NotFoundException"/> (HTTP 404) when no claim exists for the given id.
/// </para>
/// <para>
/// When the target claim has already been deactivated the domain aggregate raises
/// <see cref="InvalidOperationException"/>; this handler translates that into a
/// <see cref="ConflictException"/> (HTTP 409) to avoid leaking a raw exception as 500 — per
/// project pitfall #2568, <c>InvalidOperationException</c> must never escape a handler.
/// </para>
/// </remarks>
internal sealed class DeactivateMechanicGoldenClaimHandler : ICommandHandler<DeactivateMechanicGoldenClaimCommand, Unit>
{
    private readonly IMechanicGoldenClaimRepository _repository;
    private readonly IHybridCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateMechanicGoldenClaimHandler> _logger;

    public DeactivateMechanicGoldenClaimHandler(
        IMechanicGoldenClaimRepository repository,
        IHybridCacheService cache,
        IUnitOfWork unitOfWork,
        ILogger<DeactivateMechanicGoldenClaimHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeactivateMechanicGoldenClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Deactivating mechanic golden claim {ClaimId}",
            command.ClaimId);

        var claim = await _repository.GetByIdAsync(command.ClaimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            throw new NotFoundException("MechanicGoldenClaim", command.ClaimId.ToString());
        }

        try
        {
            claim.Deactivate();
        }
        catch (InvalidOperationException ex)
        {
            // Aggregate raises InvalidOperationException when the claim is already deactivated.
            // Translate to ConflictException so the API surface returns 409 (per pitfall #2568).
            throw new ConflictException("Already deactivated.", ex);
        }

        await _repository.UpdateAsync(claim, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = $"golden:{claim.SharedGameId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deactivated mechanic golden claim {ClaimId} for SharedGameId={SharedGameId}",
            claim.Id, claim.SharedGameId);

        return Unit.Value;
    }
}
