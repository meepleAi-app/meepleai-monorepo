using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;

/// <summary>
/// Handler for <see cref="ImportBggTagsCommand"/> (ADR-051 Sprint 1 / Task 22). Projects the
/// incoming <see cref="BggTagDto"/> list to the repository's tuple contract, invokes the batch
/// upsert, commits the unit of work, and invalidates the per-game golden-set cache key so
/// downstream scoring readers observe the new tags immediately.
/// </summary>
/// <remarks>
/// <para>
/// The repository's <c>UpsertBatchAsync</c> stages inserts via EF change-tracking but does not
/// self-commit — this handler owns the <see cref="IUnitOfWork"/> boundary.
/// </para>
/// <para>
/// An empty <see cref="ImportBggTagsCommand.Tags"/> list is a valid no-op: the handler
/// short-circuits, returning <c>0</c> without touching the repository, unit of work, or cache.
/// </para>
/// <para>
/// The handler does not pre-check <see cref="ImportBggTagsCommand.SharedGameId"/> existence.
/// FK integrity against <c>shared_games</c> is enforced at the database level — surfacing a
/// synthetic "not found" here would race against concurrent archive/publish operations on the
/// shared game, so we rely on the DB to reject orphaned inserts.
/// </para>
/// <para>
/// Returns the count of tags submitted by the caller (i.e. <c>Tags.Count</c>). The underlying
/// repository does not distinguish between newly inserted rows and duplicates skipped — this
/// is documented on <see cref="ImportBggTagsCommand"/>.
/// </para>
/// </remarks>
internal sealed class ImportBggTagsHandler : ICommandHandler<ImportBggTagsCommand, int>
{
    private readonly IMechanicGoldenBggTagRepository _repository;
    private readonly IHybridCacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportBggTagsHandler> _logger;

    public ImportBggTagsHandler(
        IMechanicGoldenBggTagRepository repository,
        IHybridCacheService cache,
        IUnitOfWork unitOfWork,
        ILogger<ImportBggTagsHandler> logger)
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

    public async Task<int> Handle(ImportBggTagsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var requestedCount = command.Tags.Count;

        if (requestedCount == 0)
        {
            _logger.LogInformation(
                "ImportBggTags no-op: empty tag list for SharedGameId={SharedGameId}",
                command.SharedGameId);
            return 0;
        }

        _logger.LogInformation(
            "Importing {TagCount} BGG tag(s) for SharedGameId={SharedGameId}",
            requestedCount, command.SharedGameId);

        var tuples = command.Tags
            .Select(t => (t.Name, t.Category))
            .ToList();

        await _repository.UpsertBatchAsync(command.SharedGameId, tuples, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = $"golden:{command.SharedGameId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Imported {TagCount} BGG tag(s) for SharedGameId={SharedGameId}; invalidated cache key {CacheKey}",
            requestedCount, command.SharedGameId, cacheKey);

        return requestedCount;
    }
}
