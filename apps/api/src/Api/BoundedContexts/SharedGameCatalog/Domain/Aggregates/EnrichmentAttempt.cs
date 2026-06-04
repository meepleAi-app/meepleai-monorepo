using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Aggregate root for a single BGG enrichment attempt outcome (#1874).
/// One row per attempt (success or failure). Multiple attempts for the same
/// <see cref="SharedGameId"/> over time are expected; the <see cref="RetryCount"/>
/// disambiguates the chain.
/// </summary>
/// <remarks>
/// Used by:
/// <list type="bullet">
///   <item>Admin Failed Items panel (aggregates last attempt per shared game).</item>
///   <item>BggImportQueueBackgroundService (records each iteration).</item>
/// </list>
/// </remarks>
public sealed class EnrichmentAttempt : AggregateRoot<Guid>
{
    public Guid SharedGameId { get; private set; }

    /// <summary>Reference to the <see cref="CatalogSyncRun"/> when the attempt is part of cron / triggered run; <c>null</c> for manual one-offs.</summary>
    public Guid? CatalogSyncRunId { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public bool Success { get; private set; }

    /// <summary>Machine-readable error code (e.g. "BGG_API_RATE_LIMIT_429"). Null on success.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Human-readable error detail. Null on success.</summary>
    public string? ErrorDetail { get; private set; }

    /// <summary>0 = first try, N = N-th retry.</summary>
    public int RetryCount { get; private set; }

    // ===================================================
    // Constructors
    // ===================================================

    private EnrichmentAttempt() : base() { }

    private EnrichmentAttempt(
        Guid id,
        Guid sharedGameId,
        Guid? catalogSyncRunId,
        DateTimeOffset attemptedAt,
        bool success,
        string? errorCode,
        string? errorDetail,
        int retryCount)
        : base(id)
    {
        SharedGameId = sharedGameId;
        CatalogSyncRunId = catalogSyncRunId;
        AttemptedAt = attemptedAt;
        Success = success;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
        RetryCount = retryCount;
    }

    // ===================================================
    // Factories
    // ===================================================

    /// <summary>Records a successful enrichment outcome.</summary>
    public static EnrichmentAttempt RecordSuccess(
        Guid sharedGameId,
        Guid? catalogSyncRunId,
        int retryCount)
    {
        ValidateBasics(sharedGameId, catalogSyncRunId, retryCount);

        return new EnrichmentAttempt(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            catalogSyncRunId: catalogSyncRunId,
            attemptedAt: DateTimeOffset.UtcNow,
            success: true,
            errorCode: null,
            errorDetail: null,
            retryCount: retryCount);
    }

    /// <summary>Records a failed enrichment outcome with structured error context.</summary>
    public static EnrichmentAttempt RecordFailure(
        Guid sharedGameId,
        Guid? catalogSyncRunId,
        string errorCode,
        string errorDetail,
        int retryCount)
    {
        ValidateBasics(sharedGameId, catalogSyncRunId, retryCount);

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code is required for failure attempts.", nameof(errorCode));
        }

        if (errorCode.Length > 100)
        {
            throw new ArgumentException("Error code must be 100 characters or fewer.", nameof(errorCode));
        }

        if (string.IsNullOrWhiteSpace(errorDetail))
        {
            throw new ArgumentException("Error detail is required for failure attempts.", nameof(errorDetail));
        }

        return new EnrichmentAttempt(
            id: Guid.NewGuid(),
            sharedGameId: sharedGameId,
            catalogSyncRunId: catalogSyncRunId,
            attemptedAt: DateTimeOffset.UtcNow,
            success: false,
            errorCode: errorCode,
            errorDetail: errorDetail,
            retryCount: retryCount);
    }

    /// <summary>Repository hydration — bypasses invariants.</summary>
    public static EnrichmentAttempt Reconstitute(
        Guid id,
        Guid sharedGameId,
        Guid? catalogSyncRunId,
        DateTimeOffset attemptedAt,
        bool success,
        string? errorCode,
        string? errorDetail,
        int retryCount)
    {
        return new EnrichmentAttempt
        {
            Id = id,
            SharedGameId = sharedGameId,
            CatalogSyncRunId = catalogSyncRunId,
            AttemptedAt = attemptedAt,
            Success = success,
            ErrorCode = errorCode,
            ErrorDetail = errorDetail,
            RetryCount = retryCount,
        };
    }

    // ===================================================
    // Helpers
    // ===================================================

    private static void ValidateBasics(Guid sharedGameId, Guid? catalogSyncRunId, int retryCount)
    {
        if (sharedGameId == Guid.Empty)
        {
            throw new ArgumentException("SharedGameId cannot be Guid.Empty.", nameof(sharedGameId));
        }

        if (catalogSyncRunId.HasValue && catalogSyncRunId.Value == Guid.Empty)
        {
            throw new ArgumentException("CatalogSyncRunId cannot be Guid.Empty (use null when manual).", nameof(catalogSyncRunId));
        }

        if (retryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "RetryCount must be non-negative.");
        }
    }
}
