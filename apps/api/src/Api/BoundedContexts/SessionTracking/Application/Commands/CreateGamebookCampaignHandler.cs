using System.Globalization;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateGamebookCampaignHandler : IRequestHandler<CreateGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;
    private readonly IMediator _mediator;

    public CreateGamebookCampaignHandler(IGamebookCampaignSessionRepository repo, IMediator mediator)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<GamebookCampaignDto> Handle(CreateGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        // A0.2 (#1320): bare Guid cmd.GameId is still a SharedGame reference at the
        // wire level; wrap into GameRef.Shared until command DTO is updated upstream.
        var session = GamebookCampaignSession.Create(GameRef.Shared(cmd.GameId), cmd.OwnerUserId, cmd.Title);
        await _repo.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _repo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #1292 (AC-6.2): notify gamebook index cache so the new campaign
        // appears within 500ms on the next GET /api/v1/gamebooks for the owner.
        await _mediator.Publish(
            new GamebookCampaignCreatedDomainEvent(session.Id, session.GameRef.Id, session.OwnerUserId),
            cancellationToken).ConfigureAwait(false);

        return MapToDto(session, progress: null);
    }

    /// <summary>
    /// Maps a campaign session and an optional per-book progress row to the legacy DTO shape.
    /// C2 (2026-05-19): the Progress VO was removed from <see cref="GamebookCampaignSession"/>.
    /// Per-book progress now lives in <see cref="SessionBookProgress"/>. Callers that
    /// know the relevant book id can pass the corresponding <paramref name="progress"/>
    /// to populate <see cref="GamebookCampaignDto.CurrentParagraph"/>/<see cref="GamebookCampaignDto.History"/>;
    /// when <c>null</c>, the DTO emits default zero/empty values. The DTO shape itself
    /// is preserved for backward compatibility with the FE until Phase E.
    /// </summary>
    internal static GamebookCampaignDto MapToDto(GamebookCampaignSession s, SessionBookProgress? progress)
    {
        var currentParagraph = 0;
        IReadOnlyList<int> history = Array.Empty<int>();
        var lastReadAt = s.UpdatedAt;

        if (progress is not null)
        {
            currentParagraph = ParseParagraph(progress.LastLocation);
            history = ParseHistory(progress.HistoryJson);
            lastReadAt = progress.LastVisitedAt;
        }

        return new GamebookCampaignDto(
            s.Id,
            // Issue #1392: legacy GameId alias kept for backward compat; always
            // equal to GameRefId until the FE migrates off it.
            s.GameRef.Id,
            s.GameRef.Id,
            (int)s.GameRef.Kind,
            s.OwnerUserId,
            s.Title,
            currentParagraph,
            history,
            lastReadAt,
            s.CreatedAt,
            s.UpdatedAt);
    }

    private static int ParseParagraph(string location)
    {
        // SessionBookProgress.LastLocation format: "§N" (paragraph marker). Accept
        // either with or without the § prefix to stay tolerant of future formats.
        var trimmed = location.AsSpan().TrimStart('§').TrimStart();
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var paragraph)
            ? paragraph
            : 0;
    }

    private static IReadOnlyList<int> ParseHistory(string historyJson)
    {
        try
        {
            var raw = System.Text.Json.JsonSerializer.Deserialize<List<string>>(historyJson)
                      ?? new List<string>();
            return raw.Select(ParseParagraph).Where(p => p > 0).ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<int>();
        }
    }
}
