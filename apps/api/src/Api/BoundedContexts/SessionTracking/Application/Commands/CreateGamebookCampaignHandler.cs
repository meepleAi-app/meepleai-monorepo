using System.Globalization;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateGamebookCampaignHandler : IRequestHandler<CreateGamebookCampaignCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _repo;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGamebookCampaignHandler(
        IGamebookCampaignSessionRepository repo,
        IMediator mediator,
        IUnitOfWork unitOfWork)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GamebookCampaignDto> Handle(CreateGamebookCampaignCommand cmd, CancellationToken cancellationToken)
    {
        // A0.2 (#1320): bare Guid cmd.GameId is still a SharedGame reference at the
        // wire level; wrap into GameRef.Shared until command DTO is updated upstream.
        var session = GamebookCampaignSession.Create(GameRef.Shared(cmd.GameId), cmd.OwnerUserId, cmd.Title);

        // #2917 (decision #2759 — Option A + no-live-mode): spin up a NON-LIVE Session to carry the
        // roster. The Session factory auto-seeds the owner participant; extra User-linked participants
        // + guest names come from the request. Campaign + Session commit in ONE explicit transaction —
        // the inner CreateSessionCommand's SaveChanges enlists in the ambient tx (shared scoped
        // DbContext), so a Session failure rolls the campaign INSERT back (no orphan).
        // SkipGameNightEnvelope=true keeps it night-less; SkipKbReadinessGate=true because gamebook
        // play does not depend on RAG KB readiness; live mode is NOT opened, so invariant #10
        // max-1-live stays scoped to GameNight play.
        //
        // The roster MUST carry an IsOwner participant with the caller's real display name:
        // CreateSessionCommandValidator requires one, and this mirrors the attach/start handlers
        // (resolve the caller via GetUserByIdQuery so the owner shows their name, not a literal).
        var owner = await _mediator.Send(new GetUserByIdQuery(cmd.OwnerUserId), cancellationToken).ConfigureAwait(false);
        var ownerName = owner is not null && !string.IsNullOrWhiteSpace(owner.DisplayName)
            ? owner.DisplayName
            : owner?.Email ?? "Owner";

        var participants = new List<ParticipantDto>
        {
            new() { Id = Guid.NewGuid(), UserId = cmd.OwnerUserId, DisplayName = ownerName, IsOwner = true, JoinOrder = 0 },
        };
        if (cmd.Participants is { Count: > 0 })
        {
            var joinOrder = 1;
            foreach (var p in cmd.Participants.Where(p => !p.IsOwner))
            {
                participants.Add(new ParticipantDto
                {
                    Id = Guid.NewGuid(),
                    UserId = p.UserId,
                    DisplayName = p.DisplayName,
                    IsOwner = false,
                    JoinOrder = joinOrder++,
                });
            }
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var commitStarted = false;
        try
        {
            await _repo.AddAsync(session, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _mediator.Send(new CreateSessionCommand(
                cmd.OwnerUserId,
                cmd.GameId,
                "GameSpecific",
                DateTime.UtcNow,
                Location: null,
                Participants: participants,
                GuestNames: cmd.GuestNames,
                GamebookCampaignId: session.Id,
                SkipGameNightEnvelope: true,
                SkipKbReadinessGate: true), cancellationToken).ConfigureAwait(false);

            commitStarted = true;
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Issue #1292 (AC-6.2): notify gamebook index cache so the new campaign appears within
        // 500ms on the next GET /api/v1/gamebooks. Published AFTER commit so cache-invalidation
        // subscribers never observe an uncommitted campaign.
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
            s.GameRef.Id,
            (int)s.GameRef.Kind,
            s.OwnerUserId,
            s.Title,
            currentParagraph,
            history,
            lastReadAt,
            s.CreatedAt,
            s.UpdatedAt,
            s.Outcome.HasValue ? (int)s.Outcome.Value : null,
            s.CompletedAt);
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
