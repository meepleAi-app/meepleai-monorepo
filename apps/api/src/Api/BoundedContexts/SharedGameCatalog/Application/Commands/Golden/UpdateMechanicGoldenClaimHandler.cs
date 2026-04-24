using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using IEmbeddingService = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IEmbeddingService;
using IKeywordExtractor = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IKeywordExtractor;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Handler for <see cref="UpdateMechanicGoldenClaimCommand"/> (ADR-051 Sprint 1 / Task 20).
/// Loads a <see cref="MechanicGoldenClaim"/> by id, applies the curator's edits via the aggregate's
/// <see cref="MechanicGoldenClaim.UpdateAsync"/> method (which internally recomputes keywords and
/// the statement embedding only when the statement text actually changed), persists through the
/// repository / unit-of-work, and invalidates the per-game golden-set cache key so downstream
/// scoring readers observe the change immediately.
/// </summary>
/// <remarks>
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
internal sealed class UpdateMechanicGoldenClaimHandler : ICommandHandler<UpdateMechanicGoldenClaimCommand, Unit>
{
    private readonly IMechanicGoldenClaimRepository _repository;
    private readonly IEmbeddingService _embedding;
    private readonly IKeywordExtractor _keywords;
    private readonly IHybridCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateMechanicGoldenClaimHandler> _logger;

    public UpdateMechanicGoldenClaimHandler(
        IMechanicGoldenClaimRepository repository,
        IEmbeddingService embedding,
        IKeywordExtractor keywords,
        IHybridCacheService cache,
        IUnitOfWork unitOfWork,
        ILogger<UpdateMechanicGoldenClaimHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentNullException.ThrowIfNull(keywords);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _embedding = embedding;
        _keywords = keywords;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateMechanicGoldenClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating mechanic golden claim {ClaimId} ExpectedPage={ExpectedPage}",
            command.ClaimId, command.ExpectedPage);

        var claim = await _repository.GetByIdAsync(command.ClaimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            throw new NotFoundException("MechanicGoldenClaim", command.ClaimId.ToString());
        }

        try
        {
            await claim.UpdateAsync(
                statement: command.Statement,
                expectedPage: command.ExpectedPage,
                sourceQuote: command.SourceQuote,
                embedding: _embedding,
                keywords: _keywords,
                ct: cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // Aggregate raises InvalidOperationException when the claim is already deactivated.
            // Translate to ConflictException so the API surface returns 409 (per pitfall #2568).
            throw new ConflictException("Cannot update a deactivated claim.", ex);
        }

        await _repository.UpdateAsync(claim, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = $"golden:{claim.SharedGameId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated mechanic golden claim {ClaimId} for SharedGameId={SharedGameId}",
            claim.Id, claim.SharedGameId);

        return Unit.Value;
    }
}
