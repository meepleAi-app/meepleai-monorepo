using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to complete the currently in-progress game session within a game night.
/// </summary>
/// <param name="GameNightId">The night.</param>
/// <param name="WinnerId">The tracking participant who won, or null for no winner.</param>
/// <param name="UserId">The caller: the organizer.</param>
/// <param name="FinalRanks">
/// #4780: the ranks of the live game, by tracking participant id, when the game ends from the live. Null when the
/// organizer completes it from the night: the winner is 1 and the others follow in join order.
/// </param>
internal record CompleteGameNightSessionCommand(
    Guid GameNightId,
    Guid? WinnerId,
    Guid UserId,
    IReadOnlyDictionary<Guid, int>? FinalRanks = null
) : ICommand;

internal sealed class CompleteGameNightSessionCommandValidator : AbstractValidator<CompleteGameNightSessionCommand>
{
    public CompleteGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
