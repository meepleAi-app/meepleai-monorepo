using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Translation;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for #1774 manual text translate flow. Mirrors TranslateGamebookSegmentQueryHandler
/// but accepts text directly (no photo, no OCR, no cache lookup).
///
/// DEC-BE-11 (#1774): NO TranslatedParagraph persistence (no segment FK target).
/// DEC-BE-12 (#1774): NO SessionBookProgress update (manual mode bypasses linear progress invariant).
/// DEC-BE-13 (#1774): Final chunk echoes user-provided SourceLang as DetectedSourceLang (audit);
/// LangDetectionConfidence is null (no detection happened).
/// </summary>
internal sealed class TranslateGamebookTextQueryHandler
    : IStreamingQueryHandler<TranslateGamebookTextQuery, TranslateChunk>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookGlossaryRepository _glossary;
    private readonly ILlmService _llm;
    private readonly ICampaignOwnershipGuard _ownershipGuard;
    private readonly ILogger<TranslateGamebookTextQueryHandler> _logger;

    public TranslateGamebookTextQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookGlossaryRepository glossary,
        ILlmService llm,
        ICampaignOwnershipGuard ownershipGuard,
        ILogger<TranslateGamebookTextQueryHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(ownershipGuard);
        ArgumentNullException.ThrowIfNull(logger);
        _campaigns = campaigns;
        _glossary = glossary;
        _llm = llm;
        _ownershipGuard = ownershipGuard;
        _logger = logger;
    }

    public async IAsyncEnumerable<TranslateChunk> Handle(
        TranslateGamebookTextQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "failure";
        double? streamingLatencySec = null;
        long? promptTokens = null;
        long? completionTokens = null;
        int totalApplicableTerms = 0;
        int matchedTerms = 0;

        try
        {
            await _ownershipGuard
                .AssertOwnedByAsync(query.CampaignId, query.CallerUserId, cancellationToken)
                .ConfigureAwait(false);

            _ = await _campaigns.GetByIdAsync(query.CampaignId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException($"Campaign {query.CampaignId} not found");

            var glossaryEntries = await _glossary
                .ListByCampaignAsync(query.CampaignId, cancellationToken)
                .ConfigureAwait(false);

            var glossaryTable = string.Join("\n", glossaryEntries.Select(g => $"- {g.TermEn} → {g.TermIt}"));

            var sourceLangCode = query.SourceLang.ToUpperInvariant();
            var sourceLangName = LanguageCodes.TryGetLanguageName(sourceLangCode) ?? "English";

            var systemPrompt =
                $"You are a translator from {sourceLangName} to Italian for a tabletop RPG storybook. " +
                "Preserve narrative tone, use formal pronouns (voi/lei) when addressing players. " +
                $"Apply this glossary EXACTLY ({sourceLangName} term → Italian term) without rephrasing the Italian:\n" +
                (glossaryTable.Length > 0 ? glossaryTable : "(no glossary entries yet)") + "\n" +
                "Output ONLY the Italian translation — no preamble, no notes.";

            var fullText = new StringBuilder();

            await foreach (var chunk in _llm.GenerateCompletionStreamAsync(
                systemPrompt, query.Text, RequestSource.Manual, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(chunk.Content))
                {
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
                        "gamebook.text.translate.cost campaign={CampaignId} tokens_in={In} tokens_out={Out}",
                        query.CampaignId, chunk.Usage.PromptTokens, chunk.Usage.CompletionTokens);
                }
            }

            var translatedIt = fullText.ToString().Trim();

            // Glossary consistency tracking (DEC-BE-9)
            foreach (var entry in glossaryEntries)
            {
                if (query.Text.Contains(entry.TermEn, StringComparison.OrdinalIgnoreCase))
                {
                    totalApplicableTerms++;
                    if (translatedIt.Contains(entry.TermIt, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTerms++;
                    }
                }
            }

            var appliedTerms = glossaryEntries
                .Where(g => query.Text.Contains(g.TermEn, StringComparison.OrdinalIgnoreCase)
                            && translatedIt.Contains(g.TermIt, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.TermEn)
                .ToList();

            // DEC-BE-11: NO TranslatedParagraph.Create / AddAsync / SaveChangesAsync
            // DEC-BE-12: NO SessionBookProgress update, NO campaign.Touch()

            // DEC-BE-13: final chunk echoes user-provided source lang, confidence null
            yield return new TranslateChunk(
                Delta: string.Empty,
                IsComplete: true,
                ParagraphId: null,
                AppliedTerms: appliedTerms,
                DetectedSourceLang: sourceLangCode,
                LangDetectionConfidence: null);

            status = "success";
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested
                && string.Equals(status, "failure", StringComparison.Ordinal))
            {
                status = "cancelled";
            }

            MeepleAiMetrics.RecordGamebookTranslationRequest(
                status: status,
                latencyFullSeconds: stopwatch.Elapsed.TotalSeconds,
                latencyStreamingSeconds: streamingLatencySec,
                promptTokens: promptTokens,
                completionTokens: completionTokens,
                costUsd: null,
                provider: "unknown",
                sourceMethod: "manual");

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
        return Convert.ToHexStringLower(bytes.AsSpan(0, 4));
    }
}
