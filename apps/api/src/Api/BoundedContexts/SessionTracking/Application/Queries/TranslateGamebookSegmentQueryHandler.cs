using System.Runtime.CompilerServices;
using System.Text;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
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
    private readonly ILlmService _llm;
    private readonly ILogger<TranslateGamebookSegmentQueryHandler> _logger;

    public TranslateGamebookSegmentQueryHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        ITranslatedParagraphRepository paragraphs,
        IGamebookGlossaryRepository glossary,
        ILlmService llm,
        ILogger<TranslateGamebookSegmentQueryHandler> logger)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _photos = photos ?? throw new ArgumentNullException(nameof(photos));
        _paragraphs = paragraphs ?? throw new ArgumentNullException(nameof(paragraphs));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<TranslateChunk> Handle(
        TranslateGamebookSegmentQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
                fullText.Append(chunk.Content);
                yield return new TranslateChunk(chunk.Content, IsComplete: false);
            }
            if (chunk.IsFinal && chunk.Usage is not null)
            {
                _logger.LogInformation(
                    "gamebook.translate.cost campaign={CampaignId} paragraph={Paragraph} tokens_in={In} tokens_out={Out}",
                    query.CampaignId, query.ParagraphNumber, chunk.Usage.PromptTokens, chunk.Usage.CompletionTokens);
            }
        }

        var translatedIt = fullText.ToString().Trim();
        var appliedTerms = glossaryEntries
            .Where(g => segment.SourceText.Contains(g.TermEn, StringComparison.OrdinalIgnoreCase)
                        && translatedIt.Contains(g.TermIt, StringComparison.OrdinalIgnoreCase))
            .Select(g => g.TermEn)
            .ToList();

        var paragraph = TranslatedParagraph.Create(
            query.CampaignId, query.PhotoId, query.ParagraphNumber, artifact.PageType,
            segment.SourceText, translatedIt, appliedTerms, query.CallerUserId);

        await _paragraphs.AddAsync(paragraph, cancellationToken).ConfigureAwait(false);
        await _paragraphs.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Advance current paragraph in campaign
        campaign.UpdateProgress(query.ParagraphNumber);
        await _campaigns.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        yield return new TranslateChunk(string.Empty, IsComplete: true, paragraph.Id, appliedTerms);
    }
}
