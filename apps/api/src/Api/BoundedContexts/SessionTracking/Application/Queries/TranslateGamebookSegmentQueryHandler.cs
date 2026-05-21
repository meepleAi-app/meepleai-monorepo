using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

internal sealed class TranslateGamebookSegmentQueryHandler
    : IStreamingQueryHandler<TranslateGamebookSegmentQuery, TranslateChunk>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly ITranslatedParagraphRepository _paragraphs;
    private readonly IGamebookGlossaryRepository _glossary;
    private readonly ISessionBookProgressRepository _progress;
    private readonly ILlmService _llm;
    private readonly ILogger<TranslateGamebookSegmentQueryHandler> _logger;

    public TranslateGamebookSegmentQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        ITranslatedParagraphRepository paragraphs,
        IGamebookGlossaryRepository glossary,
        ISessionBookProgressRepository progress,
        ILlmService llm,
        ILogger<TranslateGamebookSegmentQueryHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(logger);
        _campaigns = campaigns;
        _photos = photos;
        _paragraphs = paragraphs;
        _glossary = glossary;
        _progress = progress;
        _llm = llm;
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
        int totalApplicableTerms = 0;
        int matchedTerms = 0;

        try
        {
            var campaign = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

            if (campaign.OwnerUserId != query.CallerUserId)
                throw new ConflictException("Forbidden");

            var artifact = await _photos.GetByIdAsync(query.PhotoId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException($"Photo {query.PhotoId} not found");

            if (artifact.CampaignId != query.CampaignId)
                throw new ConflictException("Photo does not belong to this campaign");

            var segment = artifact.Segments.FirstOrDefault(s => s.ParagraphNumber == query.ParagraphNumber)
                ?? throw new NotFoundException($"Segment §{query.ParagraphNumber} not found in photo");

            var glossaryEntries = await _glossary.ListByCampaignAsync(query.CampaignId, cancellationToken).ConfigureAwait(false);
            var glossaryTable = string.Join("\n", glossaryEntries.Select(g => $"- {g.TermEn} → {g.TermIt}"));

            var systemPrompt =
                "You are a translator from English to Italian for a tabletop RPG storybook. " +
                "Preserve narrative tone, use formal pronouns (voi/lei) when addressing players. " +
                "Apply this glossary EXACTLY (English term → Italian term) without rephrasing the Italian:\n" +
                (glossaryTable.Length > 0 ? glossaryTable : "(no glossary entries yet)") + "\n" +
                "Output ONLY the Italian translation — no preamble, no notes.";

            var fullText = new StringBuilder();
            await foreach (var chunk in _llm.GenerateCompletionStreamAsync(
                systemPrompt, segment.SourceText, RequestSource.Manual, cancellationToken).ConfigureAwait(false))
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
                    yield return new TranslateChunk(chunk.Content, IsComplete: false);
                }
                if (chunk.IsFinal && chunk.Usage is not null)
                {
                    promptTokens = chunk.Usage.PromptTokens;
                    completionTokens = chunk.Usage.CompletionTokens;
                    _logger.LogInformation(
                        "gamebook.translate.cost campaign={CampaignId} paragraph={Paragraph} tokens_in={In} tokens_out={Out}",
                        query.CampaignId, query.ParagraphNumber, chunk.Usage.PromptTokens, chunk.Usage.CompletionTokens);
                }
            }

            var translatedIt = fullText.ToString().Trim();

            // Glossary consistency: ratio of glossary terms present in source AND correctly mapped to translation.
            // Issue #833 metric: meepleai.gamebook.glossary_consistency_rate
            foreach (var entry in glossaryEntries)
            {
                if (segment.SourceText.Contains(entry.TermEn, StringComparison.OrdinalIgnoreCase))
                {
                    totalApplicableTerms++;
                    if (translatedIt.Contains(entry.TermIt, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTerms++;
                    }
                }
            }
            var appliedTerms = glossaryEntries
                .Where(g => segment.SourceText.Contains(g.TermEn, StringComparison.OrdinalIgnoreCase)
                            && translatedIt.Contains(g.TermIt, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.TermEn)
                .ToList();

            var paragraph = TranslatedParagraph.Create(
                query.CampaignId, query.GameBookId, query.PhotoId, query.ParagraphNumber,
                segment.SourceText, translatedIt, appliedTerms, query.CallerUserId);

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

            yield return new TranslateChunk(string.Empty, IsComplete: true, paragraph.Id, appliedTerms);
            status = "success";
        }
        finally
        {
            // Detect cancellation that happened on the cooperative path
            if (cancellationToken.IsCancellationRequested && string.Equals(status, "failure", StringComparison.Ordinal))
            {
                status = "cancelled";
            }

            // Cost tracking via existing LlmCostUsdTotal (MeepleAiMetrics.LlmOperational, Issue #5480)
            // is emitted by HybridLlmService per-request; gamebook-specific cost_eur deferred to a
            // follow-up that derives cost from token counts + provider pricing (LlmUsage record does
            // not currently carry CostUsd / Provider fields).
            MeepleAiMetrics.RecordGamebookTranslationRequest(
                status: status,
                latencyFullSeconds: stopwatch.Elapsed.TotalSeconds,
                latencyStreamingSeconds: streamingLatencySec,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                costUsd: null,
                provider: "unknown");

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
