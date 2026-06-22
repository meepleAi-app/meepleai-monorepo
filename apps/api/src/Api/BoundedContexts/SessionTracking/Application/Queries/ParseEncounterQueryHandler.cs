using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1520 — handler for <see cref="ParseEncounterQuery"/>.
///
/// <para>Mirror of <see cref="TranslateGamebookSegmentQueryHandler"/> but:</para>
/// <list type="bullet">
///   <item><description>Synchronous (no SSE streaming) — the FE renders a card all at once.</description></item>
///   <item><description>Ephemeral — no persistence: the result lives in the user's session only.</description></item>
///   <item><description>Structured JSON output via <see cref="ILlmService.GenerateJsonAsync{T}"/>
///   (which transparently appends JSON schema instructions and handles deserialization).</description></item>
/// </list>
/// </summary>
internal sealed class ParseEncounterQueryHandler
    : IQueryHandler<ParseEncounterQuery, EncounterCheatsheetDto>
{
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly ILlmService _llm;
    private readonly ICampaignOwnershipGuard _ownershipGuard;
    private readonly ILogger<ParseEncounterQueryHandler> _logger;

    private const string SystemPrompt =
        "You analyse a single paragraph of an Encounter Book for a tabletop gamebook " +
        "(librogame) and extract a structured cheatsheet. Output strictly the JSON " +
        "schema you have been instructed to follow. Conventions:\n" +
        "- enemies[].hp/atk/def/mov are STRING tokens preserving the raw value " +
        "(e.g. '8', '+2', '—'); use '—' when a stat is absent.\n" +
        "- options[].diceRoll is OPTIONAL; omit (null) for narrative-only choices.\n" +
        "- options[].outcome MAY contain the paragraph marker (e.g. '→ §219').\n" +
        "- conditions.win and conditions.loss are nullable.\n" +
        "- confidence.enemies/options/conditions are doubles in [0,1] reflecting " +
        "how confidently you extracted that cluster from the source text.\n" +
        "Return ONLY the JSON object — no preamble, no notes, no markdown fences.";

    public ParseEncounterQueryHandler(
        IGamebookPhotoArtifactRepository photos,
        ILlmService llm,
        ICampaignOwnershipGuard ownershipGuard,
        ILogger<ParseEncounterQueryHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(ownershipGuard);
        ArgumentNullException.ThrowIfNull(logger);

        _photos = photos;
        _llm = llm;
        _ownershipGuard = ownershipGuard;
        _logger = logger;
    }

    public async Task<EncounterCheatsheetDto> Handle(
        ParseEncounterQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Ownership — must run before any repository lookup so unauthorized
        //    callers never observe whether the photo even exists.
        await _ownershipGuard
            .AssertOwnedByAsync(query.CampaignId, query.CallerUserId, cancellationToken)
            .ConfigureAwait(false);

        // 2. Photo artifact lookup + campaign tenancy check.
        var artifact = await _photos.GetByIdAsync(query.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Photo {query.PhotoId} not found");

        if (artifact.CampaignId != query.CampaignId)
        {
            throw new ConflictException("Photo does not belong to this campaign");
        }

        // 3. Segment lookup — the photo's OCR must contain the requested paragraph.
        var segment = artifact.Segments.FirstOrDefault(s => s.ParagraphNumber == query.ParagraphNumber)
            ?? throw new NotFoundException($"Segment §{query.ParagraphNumber} not found in photo");

        // 4. LLM call. GenerateJsonAsync<T> appends the JSON schema instructions
        //    derived from the target type and deserializes the response; null
        //    means the model returned a payload we could not parse.
        var cheatsheet = await _llm
            .GenerateJsonAsync<EncounterCheatsheetDto>(
                SystemPrompt,
                segment.SourceText,
                RequestSource.Manual,
                cancellationToken)
            .ConfigureAwait(false);

        if (cheatsheet is null)
        {
            _logger.LogWarning(
                "encounter.parse.failed campaign={CampaignId} photo={PhotoId} paragraph={Paragraph} reason=llm_returned_null",
                query.CampaignId, query.PhotoId, query.ParagraphNumber);
            throw new ConflictException(
                $"Encounter cheatsheet could not be extracted from §{query.ParagraphNumber}. " +
                "Retry with a clearer photo or use manual input.");
        }

        _logger.LogInformation(
            "encounter.parse.success campaign={CampaignId} photo={PhotoId} paragraph={Paragraph} " +
            "enemies={EnemiesCount} options={OptionsCount} conf_enemies={ConfEnemies:F2}",
            query.CampaignId, query.PhotoId, query.ParagraphNumber,
            cheatsheet.Enemies.Count, cheatsheet.Options.Count, cheatsheet.Confidence.Enemies);

        return cheatsheet;
    }
}
