using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to start a game session within a published game night event.
/// Cross-BC: dispatches CreateSessionCommand to SessionTracking via MediatR.
/// </summary>
internal record StartGameNightSessionCommand(
    Guid GameNightId,
    Guid GameId,
    string GameTitle,
    Guid UserId
) : ICommand<StartGameNightSessionResult>;

internal record StartGameNightSessionResult(
    Guid SessionId,
    Guid GameNightSessionId,
    string SessionCode,
    int PlayOrder);

internal sealed class StartGameNightSessionCommandValidator : AbstractValidator<StartGameNightSessionCommand>
{
    public StartGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.GameTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UserId).NotEmpty();
    }
}
