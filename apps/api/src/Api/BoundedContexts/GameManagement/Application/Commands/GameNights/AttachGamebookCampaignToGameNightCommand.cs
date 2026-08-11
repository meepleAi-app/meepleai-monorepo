using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #2632 (SI-1b): attaches a libro-game campaign to a <c>Published</c> GameNight as a new
/// live sitting. Creates a SessionTracking <c>Session</c> linked to the campaign
/// (<c>GamebookCampaignId</c>), adds a <c>GameNightSession</c>, and starts it — so the #15
/// promotion + #10 max-1-live guard fire through the existing GameNight machinery
/// (the guard lives in <c>StartCurrentSession()</c>, hence the <c>AddSession → StartCurrentSession</c>
/// sequence must both run). Limited to shared-game campaigns (D-SHARED): a private-game
/// campaign cannot become a Session because <c>Session.GameId</c> FK-restricts to shared_games.
/// </summary>
internal record AttachGamebookCampaignToGameNightCommand(
    Guid GameNightId,
    Guid CampaignId,
    Guid CallerUserId
) : ICommand<AttachGamebookCampaignToGameNightResult>;

/// <summary>Result of <see cref="AttachGamebookCampaignToGameNightCommand"/>.</summary>
internal record AttachGamebookCampaignToGameNightResult(
    Guid SessionId,
    Guid GameNightSessionId,
    string SessionCode,
    int PlayOrder);

internal sealed class AttachGamebookCampaignToGameNightCommandValidator
    : AbstractValidator<AttachGamebookCampaignToGameNightCommand>
{
    public AttachGamebookCampaignToGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}
