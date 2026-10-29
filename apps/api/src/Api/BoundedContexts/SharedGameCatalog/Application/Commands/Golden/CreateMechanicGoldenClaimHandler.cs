using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using IEmbeddingService = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IEmbeddingService;
using IKeywordExtractor = Api.BoundedContexts.SharedGameCatalog.Domain.Services.IKeywordExtractor;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Handler for <see cref="CreateMechanicGoldenClaimCommand"/> (ADR-051 Sprint 1 / Task 19).
/// Creates a new <see cref="MechanicGoldenClaim"/> aggregate via the async factory (which computes
/// the embedding and extracts keywords from the statement), persists it through the repository /
/// unit-of-work, and invalidates the per-game golden-set cache key so downstream scoring readers
/// observe the change immediately.
/// </summary>
internal sealed class CreateMechanicGoldenClaimHandler : ICommandHandler<CreateMechanicGoldenClaimCommand, Guid>
{
    private readonly IMechanicGoldenClaimRepository _repository;
    private readonly IEmbeddingService _embedding;
    private readonly IKeywordExtractor _keywords;
    private readonly IHybridCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateMechanicGoldenClaimHandler> _logger;

    public CreateMechanicGoldenClaimHandler(
        IMechanicGoldenClaimRepository repository,
        IEmbeddingService embedding,
        IKeywordExtractor keywords,
        IHybridCacheService cache,
        IUnitOfWork unitOfWork,
        ILogger<CreateMechanicGoldenClaimHandler> logger)
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

    public async Task<Guid> Handle(CreateMechanicGoldenClaimCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Creating mechanic golden claim for SharedGameId={SharedGameId} Section={Section} ExpectedPage={ExpectedPage}",
            command.SharedGameId, command.Section, command.ExpectedPage);

        var claim = await MechanicGoldenClaim.CreateAsync(
            sharedGameId: command.SharedGameId,
            section: command.Section,
            statement: command.Statement,
            expectedPage: command.ExpectedPage,
            sourceQuote: command.SourceQuote,
            curatorUserId: command.CuratorUserId,
            embedding: _embedding,
            keywords: _keywords,
            ct: cancellationToken).ConfigureAwait(false);

        await _repository.AddAsync(claim, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The claim is already saved: if dropping the key fails (Redis is down — the wrapper rethrows on
        // purpose, other callers need to see it) the request must not turn into an error, because the client
        // retries it and the retry would embed again and insert a SECOND claim (#4720). The key stays stale
        // until it expires. A cancelled request is not a backend failure and still propagates.
        var cacheKey = $"golden:{command.SharedGameId}";
        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types: post-commit, best effort by design
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Golden claim {ClaimId} saved, but dropping cache key {CacheKey} failed; the key stays until it expires",
                claim.Id, cacheKey);
        }
#pragma warning restore CA1031

        _logger.LogInformation(
            "Created mechanic golden claim {ClaimId} for SharedGameId={SharedGameId}",
            claim.Id, command.SharedGameId);

        return claim.Id;
    }
}
