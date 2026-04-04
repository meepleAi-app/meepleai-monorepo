using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

// ── Result / Request records ────────────────────────────────────────

public sealed record EnqueueResult(int Enqueued, int Skipped);

public sealed record EnqueueEnrichmentRequest(IReadOnlyList<Guid> SharedGameIds);

public sealed record MarkCompleteRequest(IReadOnlyList<Guid> SharedGameIds);

// ── Commands ────────────────────────────────────────────────────────

/// <summary>
/// Enqueue selected skeleton/failed games for BGG enrichment.
/// </summary>
public sealed record EnqueueEnrichmentCommand(
    IReadOnlyList<Guid> SharedGameIds, Guid UserId) : ICommand<EnqueueResult>;

/// <summary>
/// Enqueue ALL skeleton and failed games for BGG enrichment.
/// </summary>
public sealed record EnqueueAllSkeletonsCommand(Guid UserId) : ICommand<EnqueueResult>;

/// <summary>
/// Mark enriched games as complete (no PDF step needed).
/// </summary>
public sealed record MarkGamesCompleteCommand(
    IReadOnlyList<Guid> SharedGameIds) : ICommand<int>;

// ── Handlers ────────────────────────────────────────────────────────

/// <summary>
/// Handler for <see cref="EnqueueEnrichmentCommand"/>.
/// Loads games by IDs, filters to Skeleton/Failed, transitions to EnrichmentQueued,
/// and enqueues via <see cref="IBggImportQueueService"/>.
/// </summary>
internal sealed class EnqueueEnrichmentCommandHandler
    : ICommandHandler<EnqueueEnrichmentCommand, EnqueueResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IBggImportQueueService _queueService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnqueueEnrichmentCommandHandler> _logger;

    public EnqueueEnrichmentCommandHandler(
        ISharedGameRepository repository,
        IBggImportQueueService queueService,
        IUnitOfWork unitOfWork,
        ILogger<EnqueueEnrichmentCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnqueueResult> Handle(
        EnqueueEnrichmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var games = await _repository
            .GetByIdsAsync(command.SharedGameIds, cancellationToken)
            .ConfigureAwait(false);

        var eligible = games.Values
            .Where(g => g.GameDataStatus is GameDataStatus.Skeleton or GameDataStatus.Failed)
            .ToList();

        var skipped = command.SharedGameIds.Count - eligible.Count;

        if (eligible.Count == 0)
        {
            _logger.LogInformation("No eligible games to enqueue for enrichment (all {Count} skipped)", command.SharedGameIds.Count);
            return new EnqueueResult(0, skipped);
        }

        foreach (var game in eligible)
        {
            game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        }

        var items = eligible
            .Select(g => (g.Id, g.BggId, g.Title))
            .ToList();

        var (batchId, enqueued) = await _queueService
            .EnqueueEnrichmentBatchAsync(items, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued {Enqueued} games for enrichment (batch {BatchId}, skipped {Skipped})",
            enqueued, batchId, skipped);

        return new EnqueueResult(enqueued, skipped);
    }
}

/// <summary>
/// Handler for <see cref="EnqueueAllSkeletonsCommand"/>.
/// Queries all Skeleton and Failed games, transitions and enqueues them.
/// </summary>
internal sealed class EnqueueAllSkeletonsCommandHandler
    : ICommandHandler<EnqueueAllSkeletonsCommand, EnqueueResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IBggImportQueueService _queueService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnqueueAllSkeletonsCommandHandler> _logger;

    public EnqueueAllSkeletonsCommandHandler(
        ISharedGameRepository repository,
        IBggImportQueueService queueService,
        IUnitOfWork unitOfWork,
        ILogger<EnqueueAllSkeletonsCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnqueueResult> Handle(
        EnqueueAllSkeletonsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var skeletons = await _repository
            .GetByGameDataStatusAsync(GameDataStatus.Skeleton, cancellationToken)
            .ConfigureAwait(false);

        var failed = await _repository
            .GetByGameDataStatusAsync(GameDataStatus.Failed, cancellationToken)
            .ConfigureAwait(false);

        var eligible = skeletons.Concat(failed).ToList();

        if (eligible.Count == 0)
        {
            _logger.LogInformation("No skeleton or failed games to enqueue for enrichment");
            return new EnqueueResult(0, 0);
        }

        foreach (var game in eligible)
        {
            game.TransitionDataStatusTo(GameDataStatus.EnrichmentQueued);
        }

        var items = eligible
            .Select(g => (g.Id, g.BggId, g.Title))
            .ToList();

        var (batchId, enqueued) = await _queueService
            .EnqueueEnrichmentBatchAsync(items, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued all {Enqueued} skeleton/failed games for enrichment (batch {BatchId})",
            enqueued, batchId);

        return new EnqueueResult(enqueued, 0);
    }
}

/// <summary>
/// Handler for <see cref="MarkGamesCompleteCommand"/>.
/// Loads games by IDs, filters to Enriched status, and marks each complete.
/// </summary>
internal sealed class MarkGamesCompleteCommandHandler
    : ICommandHandler<MarkGamesCompleteCommand, int>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkGamesCompleteCommandHandler> _logger;

    public MarkGamesCompleteCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<MarkGamesCompleteCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(
        MarkGamesCompleteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var games = await _repository
            .GetByIdsAsync(command.SharedGameIds, cancellationToken)
            .ConfigureAwait(false);

        var enriched = games.Values
            .Where(g => g.GameDataStatus == GameDataStatus.Enriched)
            .ToList();

        foreach (var game in enriched)
        {
            game.MarkDataComplete();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Marked {Count} games as complete (skipped {Skipped})",
            enriched.Count, command.SharedGameIds.Count - enriched.Count);

        return enriched.Count;
    }
}
