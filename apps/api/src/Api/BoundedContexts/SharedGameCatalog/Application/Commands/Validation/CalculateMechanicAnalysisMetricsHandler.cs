using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="CalculateMechanicAnalysisMetricsCommand"/> (ADR-051 Sprint 1 / Task 23).
/// Projects the analysis claims to the matching engine's DTO (keywords + embedding + page),
/// invokes the engine against the golden-claim and BGG-tag corpus for the shared game, creates
/// the <see cref="MechanicAnalysisMetrics"/> snapshot, and syncs certification state back onto
/// the aggregate. When the metrics pass the configured thresholds the handler also raises
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Events.MechanicAnalysisCertifiedEvent"/>
/// for downstream subscribers (audit, notifications, dashboard refresh).
/// </summary>
/// <remarks>
/// <para>
/// The handler owns the <see cref="IUnitOfWork"/> boundary: the metrics insert, the aggregate
/// update, and the certification-event dispatch all share a single <c>SaveChangesAsync</c> call
/// so either everything commits or nothing does.
/// </para>
/// <para>
/// The analysis must be in <see cref="MechanicAnalysisStatus.Published"/>. The aggregate enum
/// does not expose a <c>Completed</c> literal — <c>Published</c> is the terminal post-approval
/// state. Any other status surfaces as a <see cref="ConflictException"/> (HTTP 409).
/// </para>
/// <para>
/// Claim pre-processing (keyword extraction + embedding) runs sequentially rather than via
/// <c>Task.WhenAll</c> because the embedding service is a remote call and keeping order
/// deterministic simplifies testing and debugging. Each claim must carry at least one citation
/// (ADR-051 T3 invariant); the handler defensively enforces this before dereferencing
/// <c>Citations[0].PdfPage</c>.
/// </para>
/// </remarks>
internal sealed class CalculateMechanicAnalysisMetricsHandler
    : ICommandHandler<CalculateMechanicAnalysisMetricsCommand, Guid>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IMechanicAnalysisMetricsRepository _metricsRepository;
    private readonly IMechanicGoldenClaimRepository _goldenRepository;
    private readonly IMechanicGoldenBggTagRepository _bggTagRepository;
    private readonly ICertificationThresholdsConfigRepository _thresholdsRepository;
    private readonly IMechanicMatchingEngine _matchingEngine;
    private readonly IKeywordExtractor _keywordExtractor;
    private readonly IEmbeddingService _embeddingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalculateMechanicAnalysisMetricsHandler> _logger;

    public CalculateMechanicAnalysisMetricsHandler(
        IMechanicAnalysisRepository analysisRepository,
        IMechanicAnalysisMetricsRepository metricsRepository,
        IMechanicGoldenClaimRepository goldenRepository,
        IMechanicGoldenBggTagRepository bggTagRepository,
        ICertificationThresholdsConfigRepository thresholdsRepository,
        IMechanicMatchingEngine matchingEngine,
        IKeywordExtractor keywordExtractor,
        IEmbeddingService embeddingService,
        IUnitOfWork unitOfWork,
        ILogger<CalculateMechanicAnalysisMetricsHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _goldenRepository = goldenRepository ?? throw new ArgumentNullException(nameof(goldenRepository));
        _bggTagRepository = bggTagRepository ?? throw new ArgumentNullException(nameof(bggTagRepository));
        _thresholdsRepository = thresholdsRepository ?? throw new ArgumentNullException(nameof(thresholdsRepository));
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _keywordExtractor = keywordExtractor ?? throw new ArgumentNullException(nameof(keywordExtractor));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        CalculateMechanicAnalysisMetricsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdWithClaimsAsync(request.MechanicAnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException("MechanicAnalysis", request.MechanicAnalysisId.ToString());
        }

        if (analysis.Status != MechanicAnalysisStatus.Published)
        {
            throw new ConflictException(
                $"MechanicAnalysis {analysis.Id} must be in Published status to calculate metrics " +
                $"(current: {analysis.Status}).");
        }

        var goldenClaims = await _goldenRepository
            .GetByGameAsync(analysis.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        var bggTags = await _bggTagRepository
            .GetByGameAsync(analysis.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        var thresholdsConfig = await _thresholdsRepository
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

        var versionHash = await _goldenRepository
            .GetVersionHashAsync(analysis.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (versionHash is null)
        {
            throw new ConflictException(
                $"No golden claims exist for SharedGame {analysis.SharedGameId}; cannot compute metrics.");
        }

        var analysisClaims = new List<AnalysisClaim>(analysis.Claims.Count);
        foreach (var claim in analysis.Claims)
        {
            if (claim.Citations.Count == 0)
            {
                // ADR-051 T3 invariant: every MechanicClaim must carry >=1 citation. This branch is
                // defensive — Reconstitute does not enforce it, so a malformed DB row would surface here.
                throw new InvalidOperationException(
                    $"Claim {claim.Id} has no citations; ADR-051 T3 invariant violation.");
            }

            var keywords = _keywordExtractor.Extract(claim.Text);
            var embedding = await _embeddingService
                .EmbedAsync(claim.Text, cancellationToken)
                .ConfigureAwait(false);

            analysisClaims.Add(new AnalysisClaim(
                Id: claim.Id,
                Keywords: keywords,
                Embedding: embedding,
                Page: claim.Citations[0].PdfPage));
        }

        // Sprint 1 MVP: analysis-side mechanic-tag extraction deferred to Sprint 2.
        var analysisTags = Array.Empty<AnalysisMechanicTag>();

        var matchingStart = Stopwatch.GetTimestamp();
        var matchResult = _matchingEngine.Match(
            analysisClaims,
            goldenClaims,
            bggTags,
            analysisTags,
            thresholdsConfig.Thresholds);
        MeepleAiMetrics.MatchingDuration.Record(
            Stopwatch.GetElapsedTime(matchingStart).TotalMilliseconds);

        var matchDetailsJson = JsonSerializer.Serialize(matchResult.Matches);

        var metrics = MechanicAnalysisMetrics.Create(
            analysisId: analysis.Id,
            sharedGameId: analysis.SharedGameId,
            coveragePct: matchResult.CoveragePct,
            pageAccuracyPct: matchResult.PageAccuracyPct,
            bggMatchPct: matchResult.BggMatchPct,
            thresholds: thresholdsConfig.Thresholds,
            goldenVersionHash: versionHash.Value,
            matchDetailsJson: matchDetailsJson);

        await _metricsRepository.AddAsync(metrics, cancellationToken).ConfigureAwait(false);

        analysis.ApplyMetricsResult(metrics);

        if (metrics.CertificationStatus == CertificationStatus.Certified)
        {
            // System sentinel: automatic certification has no human actor (WasOverride=false).
            // AddDomainEvent is protected on the aggregate, so we delegate through a dedicated
            // aggregate method that encodes the automatic-path invariants.
            analysis.RaiseAutomaticCertificationEvent(metrics.ComputedAt);
        }

        _analysisRepository.Update(analysis);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var statusTag = new TagList
        {
            { "certification_status", metrics.CertificationStatus.ToString().ToLowerInvariant() }
        };
        MeepleAiMetrics.MetricsComputed.Add(1, statusTag);
        MeepleAiMetrics.OverallScore.Record((double)metrics.OverallScore);

        if (metrics.CertificationStatus == CertificationStatus.Certified)
        {
            MeepleAiMetrics.CertificationsGranted.Add(1);
        }

        _logger.LogInformation(
            "Computed MechanicAnalysisMetrics {MetricsId} for MechanicAnalysis {AnalysisId} " +
            "(SharedGame {SharedGameId}): Coverage={CoveragePct}, PageAccuracy={PageAccuracyPct}, " +
            "BggMatch={BggMatchPct}, Overall={OverallScore}, Certification={CertificationStatus}",
            metrics.Id,
            analysis.Id,
            analysis.SharedGameId,
            metrics.CoveragePct,
            metrics.PageAccuracyPct,
            metrics.BggMatchPct,
            metrics.OverallScore,
            metrics.CertificationStatus);

        return metrics.Id;
    }
}
