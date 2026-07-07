using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Caching;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Translation;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

internal sealed class TranslateGamebookSegmentQueryHandler
    : IStreamingQueryHandler<TranslateGamebookSegmentQuery, TranslateChunk>
{
    // DEC-14 (#1559): Language code → name mapping moved to Api.SharedKernel.Translation.LanguageCodes
    // per DEC-BE-10 (#1774). Extend LanguageCodes.LangNameByCode to add new source languages.

    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly ITranslatedParagraphRepository _paragraphs;
    private readonly IGamebookGlossaryRepository _glossary;
    private readonly ISessionBookProgressRepository _progress;
    private readonly ILlmService _llm;
    private readonly ICampaignOwnershipGuard _ownershipGuard;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<TranslateGamebookSegmentQueryHandler> _logger;

    public TranslateGamebookSegmentQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        ITranslatedParagraphRepository paragraphs,
        IGamebookGlossaryRepository glossary,
        ISessionBookProgressRepository progress,
        ILlmService llm,
        ICampaignOwnershipGuard ownershipGuard,
        IHybridCacheService cache,
        ILogger<TranslateGamebookSegmentQueryHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(ownershipGuard);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        _campaigns = campaigns;
        _photos = photos;
        _paragraphs = paragraphs;
        _glossary = glossary;
        _progress = progress;
        _llm = llm;
        _ownershipGuard = ownershipGuard;
        _cache = cache;
        _logger = logger;
    }

    public async IAsyncEnumerable<TranslateChunk> Handle(
        TranslateGamebookSegmentQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Issue #833: gamebook translation metrics. try/finally is the only viable
        // wrapper around `yield return` in IAsyncEnumerable (CS1626 forbids catch).
        var stopwatch = Stopwatch.StartNew();
        var status = "failure";          // pessimistic default; set to "success" on completion
        double? streamingLatencySec = null;
        long? promptTokens = null;
        long? completionTokens = null;
        double? costUsd = null;          // #2752: captured from StreamChunk.Cost on the final chunk
        string provider = "unknown";     // #2752: LLM provider that served the request
        int totalApplicableTerms = 0;
        int matchedTerms = 0;

        try
        {
            // Issue #1415: ownership/existence verified via shared guard (single source of
            // truth across SSE pre-flight + handler). Guard caches positive outcomes in
            // HttpContext.Items so the subsequent _campaigns.GetByIdAsync below does not
            // cause a double DB roundtrip on the happy path within the same request.
            await _ownershipGuard
                .AssertOwnedByAsync(query.CampaignId, query.CallerUserId, cancellationToken)
                .ConfigureAwait(false);

            var campaign = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

            // #1559 DEC-13: cache-first lookup with repo fallback (graceful degrade).
            // Cache is populated by SegmentGamebookPhotoCommandHandler immediately after OCR; on
            // a cold start (cache evicted or legacy photo) we reconstruct from the repo artifact.
            var cacheValue = await _cache.GetOrCreateAsync<PhotoOcrCacheValue>(
                GamebookCacheKeys.PhotoOcr(query.PhotoId),
                factory: async ct2 =>
                {
                    var artifact = await _photos.GetByIdAsync(query.PhotoId, ct2).ConfigureAwait(false)
                        ?? throw new NotFoundException($"Photo {query.PhotoId} not found");

                    if (artifact.CampaignId != query.CampaignId)
                        throw new ConflictException("Photo does not belong to this campaign");

                    return new PhotoOcrCacheValue(
                        FullText: artifact.OcrFullText ?? string.Empty,
                        Paragraphs: artifact.Segments
                            .Select(s => new OcrParagraphCacheEntry(s.ParagraphNumber, s.SourceText))
                            .ToList(),
                        AverageConfidence: 0.0, // not persisted on artifact directly
                        DetectedSourceLang: artifact.DetectedSourceLang,
                        LangDetectionConfidence: artifact.LangDetectionConfidence);
                },
                tags: null,
                expiration: TimeSpan.FromHours(24),
                ct: cancellationToken).ConfigureAwait(false);

            // DEC-13: cacheValue is guaranteed non-null here (factory throws NotFoundException on
            // missing artifact; GetOrCreateAsync propagates factory exceptions to the caller).
            var detectedSourceLang = cacheValue.DetectedSourceLang;
            var langDetectionConfidence = cacheValue.LangDetectionConfidence;

            var segmentSourceText = cacheValue.Paragraphs
                .FirstOrDefault(p => p.Number == query.ParagraphNumber)?.Text
                ?? throw new NotFoundException($"Segment §{query.ParagraphNumber} not found in photo");

            var glossaryEntries = await _glossary.ListByCampaignAsync(query.CampaignId, cancellationToken).ConfigureAwait(false);
            var glossaryTable = string.Join("\n", glossaryEntries.Select(g => $"- {g.TermEn} → {g.TermIt}"));

            // DEC-14 (#1559): dynamic prompt — effective lang is: override > detected > "EN" default.
            var effectiveSourceLangCode =
                (query.SourceLangOverride ?? detectedSourceLang ?? "EN").ToUpperInvariant();
            var sourceLangName = LanguageCodes.TryGetLanguageName(effectiveSourceLangCode) ?? "English";

            var systemPrompt =
                $"You are a translator from {sourceLangName} to Italian for a tabletop RPG storybook. " +
                "Preserve narrative tone, use formal pronouns (voi/lei) when addressing players. " +
                $"Apply this glossary EXACTLY ({sourceLangName} term → Italian term) without rephrasing the Italian:\n" +
                (glossaryTable.Length > 0 ? glossaryTable : "(no glossary entries yet)") + "\n" +
                "Output ONLY the Italian translation — no preamble, no notes.";

            var fullText = new StringBuilder();
            await foreach (var chunk in _llm.GenerateCompletionStreamAsync(
                systemPrompt, segmentSourceText, RequestSource.Manual, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    // Time-to-first-chunk only recorded once per request
                    if (streamingLatencySec is null)
                    {
                        streamingLatencySec = stopwatch.Elapsed.TotalSeconds;
                    }
                    fullText.Append(chunk.Content);
                    yield return new TranslateChunk(Delta: chunk.Content, IsComplete: false);
                }
                if (chunk.IsFinal && chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                    _logger.LogInformation(
                        "gamebook.translate.cost campaign={CampaignId} paragraph={Paragraph} tokens_in={In} tokens_out={Out}",
                        query.CampaignId, query.ParagraphNumber, chunk.Usage.PromptTokens, chunk.Usage.CompletionTokens);
                }
                // #2752: cost + provider ride on StreamChunk.Cost (sibling of Usage), populated
                // per-provider by OpenRouterLlmClient / DeepSeekLlmClient on the final chunk.
                if (chunk.Cost is not null)
                {
                    costUsd = (double)chunk.Cost.TotalCost;
                    provider = chunk.Cost.Provider;
                }
            }

            var translatedIt = fullText.ToString().Trim();

            // Glossary consistency: ratio of glossary terms present in source AND correctly mapped to translation.
            // Issue #833 metric: meepleai.gamebook.glossary_consistency_rate
            foreach (var entry in glossaryEntries)
            {
                if (segmentSourceText.Contains(entry.TermEn, StringComparison.OrdinalIgnoreCase))
                {
                    totalApplicableTerms++;
                    if (translatedIt.Contains(entry.TermIt, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTerms++;
                    }
                }
            }
            var appliedTerms = glossaryEntries
                .Where(g => segmentSourceText.Contains(g.TermEn, StringComparison.OrdinalIgnoreCase)
                            && translatedIt.Contains(g.TermIt, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.TermEn)
                .ToList();

            var paragraph = TranslatedParagraph.Create(
                query.CampaignId, query.GameBookId, query.PhotoId, query.ParagraphNumber,
                segmentSourceText, translatedIt, appliedTerms, query.CallerUserId);

            await _paragraphs.AddAsync(paragraph, cancellationToken).ConfigureAwait(false);
            await _paragraphs.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // C2 (2026-05-19): advance per-book progress via SessionBookProgress.
            // LastLocation format follows the "§N" paragraph-marker convention.
            var location = string.Create(
                CultureInfo.InvariantCulture,
                $"§{query.ParagraphNumber}");
            var existingProgress = await _progress
                .GetByCampaignAndBookAsync(query.CampaignId, query.GameBookId, cancellationToken)
                .ConfigureAwait(false);
            if (existingProgress is null)
            {
                var fresh = SessionBookProgress.Create(query.CampaignId, query.GameBookId, location);
                await _progress.AddAsync(fresh, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                existingProgress.UpdateLocation(location);
                await _progress.UpdateAsync(existingProgress, cancellationToken).ConfigureAwait(false);
            }
            campaign.Touch(query.CallerUserId);
            await _campaigns.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // DEC-10 (#1559): DetectedSourceLang / LangDetectionConfidence in final chunk only.
            yield return new TranslateChunk(
                Delta: string.Empty,
                IsComplete: true,
                ParagraphId: paragraph.Id,
                AppliedTerms: appliedTerms,
                DetectedSourceLang: detectedSourceLang,
                LangDetectionConfidence: langDetectionConfidence);
            status = "success";
        }
        finally
        {
            // Detect cancellation that happened on the cooperative path
            if (cancellationToken.IsCancellationRequested && string.Equals(status, "failure", StringComparison.Ordinal))
            {
                status = "cancelled";
            }

            // #2752: gamebook-specific cost_eur is derived (USD x UsdToEurRate) inside the helper
            // from StreamChunk.Cost, captured above. costUsd stays null when the provider does not
            // report cost (or on a failed/cancelled stream) — the helper skips the EUR record then.
            MeepleAiMetrics.RecordGamebookTranslationRequest(
                status: status,
                latencyFullSeconds: stopwatch.Elapsed.TotalSeconds,
                latencyStreamingSeconds: streamingLatencySec,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                costUsd: costUsd,
                provider: provider);

            if (totalApplicableTerms > 0)
            {
                var rate = (double)matchedTerms / totalApplicableTerms;
                MeepleAiMetrics.RecordGamebookGlossaryConsistency(rate, HashCampaignId(query.CampaignId));
            }
        }
    }

    private static string HashCampaignId(Guid campaignId)
    {
        var bytes = SHA256.HashData(campaignId.ToByteArray());
        return Convert.ToHexStringLower(bytes.AsSpan(0, 4)); // 8 hex chars
    }
}
