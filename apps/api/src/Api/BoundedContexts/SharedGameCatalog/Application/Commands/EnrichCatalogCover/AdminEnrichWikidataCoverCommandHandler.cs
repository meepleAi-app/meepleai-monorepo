using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Handler for <see cref="AdminEnrichWikidataCoverCommand"/>. Thin wrapper over
/// <see cref="IWikidataCoverEnrichmentRunner"/>: runs the M8 pipeline + records
/// a <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt"/> row, then
/// flattens the result into a wire-friendly DTO for the admin endpoint.
/// Issue #1823 Wave 3 M12.
/// </summary>
internal sealed class AdminEnrichWikidataCoverCommandHandler
    : ICommandHandler<AdminEnrichWikidataCoverCommand, AdminEnrichWikidataCoverResult>
{
    private readonly IWikidataCoverEnrichmentRunner _runner;
    private readonly ILogger<AdminEnrichWikidataCoverCommandHandler> _logger;

    public AdminEnrichWikidataCoverCommandHandler(
        IWikidataCoverEnrichmentRunner runner,
        ILogger<AdminEnrichWikidataCoverCommandHandler> logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminEnrichWikidataCoverResult> Handle(
        AdminEnrichWikidataCoverCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AdminEnrichWikidataCover: triggered by user {UserId} for game {GameId} (forceRefresh={ForceRefresh})",
            request.TriggeredByUserId, request.GameId, request.ForceRefresh);

        var result = await _runner
            .EnrichAndRecordAsync(request.GameId, request.ForceRefresh, cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            EnrichCatalogCoverResult.Success success => AdminEnrichWikidataCoverResult.FromSuccess(success),
            EnrichCatalogCoverResult.Skipped skipped => AdminEnrichWikidataCoverResult.FromSkipped(skipped),
            EnrichCatalogCoverResult.Failed failed => AdminEnrichWikidataCoverResult.FromFailed(failed),
            _ => AdminEnrichWikidataCoverResult.FromFailed(
                new EnrichCatalogCoverResult.Failed("unexpected-result", result.GetType().Name)),
        };
    }
}
