using System.Globalization;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateGamebookProgressHandler : IRequestHandler<UpdateGamebookProgressCommand, GamebookCampaignDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly ISessionBookProgressRepository _progress;

    public UpdateGamebookProgressHandler(
        IGamebookCampaignSessionRepository campaigns,
        ISessionBookProgressRepository progress)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        ArgumentNullException.ThrowIfNull(progress);
        _campaigns = campaigns;
        _progress = progress;
    }

    public async Task<GamebookCampaignDto> Handle(UpdateGamebookProgressCommand cmd, CancellationToken cancellationToken)
    {
        var session = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (session.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Only owner can update progress");

        // SessionBookProgress.LastLocation semantic: paragraphs are stored as "§N".
        var location = string.Create(
            CultureInfo.InvariantCulture,
            $"§{cmd.CurrentParagraph}");

        var existing = await _progress
            .GetByCampaignAndBookAsync(cmd.CampaignId, cmd.GameBookId, cancellationToken)
            .ConfigureAwait(false);

        SessionBookProgress progressRow;
        if (existing is null)
        {
            progressRow = SessionBookProgress.Create(cmd.CampaignId, cmd.GameBookId, location);
            await _progress.AddAsync(progressRow, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.UpdateLocation(location);
            await _progress.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            progressRow = existing;
        }

        // Touch the parent session so UpdatedAt reflects user activity. The shared
        // DbContext is flushed via the campaign repo's SaveChangesAsync (which also
        // persists the staged SessionBookProgress changes — unit-of-work pattern).
        session.Touch(cmd.CallerUserId);
        await _campaigns.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreateGamebookCampaignHandler.MapToDto(session, progressRow);
    }
}
