using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to start a game session within a published game night event.
/// Cross-BC: dispatches CreateSessionCommand to SessionTracking via MediatR.
/// If Participants is null or empty, the handler auto-seeds the organizer as sole owner.
/// </summary>
internal record StartGameNightSessionCommand(
    Guid GameNightId,
    Guid GameId,
    string GameTitle,
    Guid UserId,
    IReadOnlyList<ParticipantDto>? Participants = null,
    GameStateTier StateTier = GameStateTier.Minimal,
    // #4780 PR 4: the email invitations accepted without an account the organizer seats as guests. Only ids of this
    // night's accepted invitations without an account seat anyone; any other id seats nobody.
    IReadOnlyList<Guid>? GuestInvitationIds = null
) : ICommand<StartGameNightSessionResult>;

/// <summary>
/// The started sitting. #4752: <c>LiveSessionId</c> is the <c>LiveGameSession</c> created alongside
/// the tracking Session — where the game is played (<c>/sessions/{LiveSessionId}/live</c>); null only
/// for a result built without one.
/// </summary>
internal record StartGameNightSessionResult(
    Guid SessionId,
    Guid GameNightSessionId,
    string SessionCode,
    int PlayOrder,
    Guid? LiveSessionId = null);

internal sealed class StartGameNightSessionCommandValidator : AbstractValidator<StartGameNightSessionCommand>
{
    public StartGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.GameTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UserId).NotEmpty();
        // #4780 PR 4: a table has a seat per colour; more guests than that could never sit.
        RuleFor(x => x.GuestInvitationIds)
            .Must(ids => ids is null || ids.Count <= MaxGuests)
            .WithMessage($"At most {MaxGuests} guests can be seated.");
    }

    private static readonly int MaxGuests = Enum.GetValues<PlayerColor>().Length;
}
