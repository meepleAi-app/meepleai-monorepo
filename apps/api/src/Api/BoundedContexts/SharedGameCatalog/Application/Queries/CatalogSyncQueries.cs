using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

// ── DTOs ────────────────────────────────────────────────────────────

public sealed record CatalogSyncRunSummary(
    Guid Id,
    CatalogSyncProvider Provider,
    CatalogSyncStatus Status,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int ItemsAdded,
    int ItemsUpdated,
    int ItemsFailed,
    string? ErrorCode,
    string? ErrorDetail,
    Guid? TriggeredByUserId)
{
    public TimeSpan? Duration => StartedAt.HasValue && CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    public static CatalogSyncRunSummary FromDomain(CatalogSyncRun run) =>
        new(
            run.Id,
            run.Provider,
            run.Status,
            run.Title,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            run.ItemsAdded,
            run.ItemsUpdated,
            run.ItemsFailed,
            run.ErrorCode,
            run.ErrorDetail,
            run.TriggeredByUserId);
}

public sealed record CatalogSyncCumulative(int GamesTotal);

public sealed record CatalogSyncStatusResult(
    string Status, // "idle" | "running" | "never_run"
    CatalogSyncRunSummary? LastRun,
    CatalogSyncRunSummary? CurrentRun,
    CatalogSyncCumulative Cumulative,
    DateTimeOffset? NextScheduled);

public sealed record PagedCatalogSyncRunsResult(
    IReadOnlyList<CatalogSyncRunSummary> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record CatalogSyncRunLogsResult(
    Guid RunId,
    CatalogSyncStatus Status,
    string? ErrorCode,
    string? ErrorDetail,
    IReadOnlyList<string> Logs,
    bool LogsAvailable,
    string? LogsUnavailableReason);

// ── Queries ─────────────────────────────────────────────────────────

public sealed record GetCatalogSyncStatusQuery : IQuery<CatalogSyncStatusResult>;

public sealed record GetCatalogSyncRunsQuery(int Page, int PageSize)
    : IQuery<PagedCatalogSyncRunsResult>;

public sealed record GetCatalogSyncRunLogsQuery(Guid RunId, int Tail)
    : IQuery<CatalogSyncRunLogsResult?>;

// ── Validators ──────────────────────────────────────────────────────

public sealed class GetCatalogSyncRunsQueryValidator : AbstractValidator<GetCatalogSyncRunsQuery>
{
    public GetCatalogSyncRunsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetCatalogSyncRunLogsQueryValidator : AbstractValidator<GetCatalogSyncRunLogsQuery>
{
    public GetCatalogSyncRunLogsQueryValidator()
    {
        RuleFor(x => x.RunId).NotEmpty();
        RuleFor(x => x.Tail).InclusiveBetween(1, 10_000);
    }
}

// ── Handlers ────────────────────────────────────────────────────────

internal sealed class GetCatalogSyncStatusQueryHandler
    : IQueryHandler<GetCatalogSyncStatusQuery, CatalogSyncStatusResult>
{
    private readonly ICatalogSyncRunRepository _runRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetCatalogSyncStatusQueryHandler(
        ICatalogSyncRunRepository runRepository,
        ISharedGameRepository sharedGameRepository)
    {
        ArgumentNullException.ThrowIfNull(runRepository);
        ArgumentNullException.ThrowIfNull(sharedGameRepository);
        _runRepository = runRepository;
        _sharedGameRepository = sharedGameRepository;
    }

    public async Task<CatalogSyncStatusResult> Handle(
        GetCatalogSyncStatusQuery query, CancellationToken cancellationToken)
    {
        var current = await _runRepository
            .GetCurrentRunningAsync(cancellationToken)
            .ConfigureAwait(false);

        var last = await _runRepository
            .GetLatestCompletedAsync(cancellationToken)
            .ConfigureAwait(false);

        var gamesTotal = await _sharedGameRepository
            .CountAllAsync(cancellationToken)
            .ConfigureAwait(false);

        var status = current is not null
            ? "running"
            : (last is null ? "never_run" : "idle");

        return new CatalogSyncStatusResult(
            Status: status,
            LastRun: last is null ? null : CatalogSyncRunSummary.FromDomain(last),
            CurrentRun: current is null ? null : CatalogSyncRunSummary.FromDomain(current),
            Cumulative: new CatalogSyncCumulative(gamesTotal),
            NextScheduled: null); // Phase 5: populate from cron config
    }
}

internal sealed class GetCatalogSyncRunsQueryHandler
    : IQueryHandler<GetCatalogSyncRunsQuery, PagedCatalogSyncRunsResult>
{
    private readonly ICatalogSyncRunRepository _repository;

    public GetCatalogSyncRunsQueryHandler(ICatalogSyncRunRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<PagedCatalogSyncRunsResult> Handle(
        GetCatalogSyncRunsQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository
            .GetPagedAsync(query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var summaries = items.Select(CatalogSyncRunSummary.FromDomain).ToList();
        var hasMore = query.Page * query.PageSize < total;

        return new PagedCatalogSyncRunsResult(
            Items: summaries,
            Total: total,
            Page: query.Page,
            PageSize: query.PageSize,
            HasMore: hasMore);
    }
}

internal sealed class GetCatalogSyncRunLogsQueryHandler
    : IQueryHandler<GetCatalogSyncRunLogsQuery, CatalogSyncRunLogsResult?>
{
    private readonly ICatalogSyncRunRepository _repository;

    public GetCatalogSyncRunLogsQueryHandler(ICatalogSyncRunRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<CatalogSyncRunLogsResult?> Handle(
        GetCatalogSyncRunLogsQuery query, CancellationToken cancellationToken)
    {
        var run = await _repository
            .GetByIdAsync(query.RunId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        // For now logs are stored externally (file system path stored on the run aggregate).
        // Reading the actual log file is deferred to Phase 5 cron service implementation;
        // the API contract is in place and returns LogsAvailable=false when no path is set
        // or when the file cannot be read.
        var logs = new List<string>();
        var available = false;
        string? unavailableReason = null;

        if (string.IsNullOrEmpty(run.LogTailJsonPath))
        {
            unavailableReason = "No log path attached to this run.";
        }
        else if (!File.Exists(run.LogTailJsonPath))
        {
            unavailableReason = "Log file not available (path missing or retention expired).";
        }
        else
        {
            try
            {
                var allLines = await File
                    .ReadAllLinesAsync(run.LogTailJsonPath, cancellationToken)
                    .ConfigureAwait(false);

                logs = allLines.TakeLast(query.Tail).ToList();
                available = true;
            }
            catch (IOException ex)
            {
                unavailableReason = $"Log read error: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                unavailableReason = $"Log read error: {ex.Message}";
            }
        }

        return new CatalogSyncRunLogsResult(
            RunId: run.Id,
            Status: run.Status,
            ErrorCode: run.ErrorCode,
            ErrorDetail: run.ErrorDetail,
            Logs: logs,
            LogsAvailable: available,
            LogsUnavailableReason: unavailableReason);
    }
}
