// Aaron-validated — bootstrap quality verified manually post-Iter 1.B.
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class BootstrapGamebookGlossaryCommandHandler
    : IRequestHandler<BootstrapGamebookGlossaryCommand, IReadOnlyList<GamebookGlossaryEntryDto>>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookGlossaryRepository _glossary;
    private readonly ILlmService _llm;
    private readonly ILogger<BootstrapGamebookGlossaryCommandHandler> _logger;

    public BootstrapGamebookGlossaryCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookGlossaryRepository glossary,
        ILlmService llm,
        ILogger<BootstrapGamebookGlossaryCommandHandler> logger)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GamebookGlossaryEntryDto>> Handle(
        BootstrapGamebookGlossaryCommand cmd, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (campaign.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Caller is not the campaign owner");

        // Idempotent: if glossary already has entries, return existing without re-bootstrapping
        var existing = await _glossary.ListByCampaignAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
            return existing.Select(MapToDto).ToList().AsReadOnly();

        var gameId = campaign.GameId;
        var systemPrompt =
            "You are a board game localization expert. " +
            "Return JSON with this exact structure: {\"entries\": [{\"en\": \"...\", \"it\": \"...\"}]}. " +
            "Limit to 30 entries. Focus on proper nouns, character names, location names, and game-specific terms.";
        var userPrompt =
            $"Provide an EN→IT glossary for the tabletop game with id {gameId}. " +
            "If you don't know this game, return empty entries: {\"entries\": []}.";

        BootstrapResult? result = null;
        try
        {
            result = await _llm.GenerateJsonAsync<BootstrapResult>(
                systemPrompt, userPrompt, RequestSource.Manual, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gamebook.glossary.bootstrap failed for campaign {CampaignId} — returning empty", cmd.CampaignId);
            return Array.Empty<GamebookGlossaryEntryDto>();
        }

        if (result is null || result.Entries.Count == 0)
            return Array.Empty<GamebookGlossaryEntryDto>();

        var entries = result.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.En) && !string.IsNullOrWhiteSpace(e.It))
            .Take(30)
            .Select(e => GamebookGlossaryEntry.Create(
                cmd.CampaignId, e.En.Trim(), e.It.Trim(), GlossarySource.AutoBootstrap, cmd.CallerUserId))
            .ToList();

        if (entries.Count > 0)
        {
            await _glossary.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
            await _glossary.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return entries.Select(MapToDto).ToList().AsReadOnly();
    }

    private static GamebookGlossaryEntryDto MapToDto(GamebookGlossaryEntry e) => new(
        e.Id, e.TermEn, e.TermIt, e.Source.ToString(), e.UpdatedAt);

    private sealed record BootstrapResult(IReadOnlyList<BootstrapEntry> Entries);
    private sealed record BootstrapEntry(string En, string It);
}
