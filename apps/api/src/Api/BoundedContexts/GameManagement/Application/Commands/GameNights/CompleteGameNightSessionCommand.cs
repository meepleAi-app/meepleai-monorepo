using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to complete the currently in-progress game session within a game night.
/// </summary>
internal record CompleteGameNightSessionCommand(
    Guid GameNightId,
    Guid? WinnerId,
    Guid UserId
) : ICommand;

internal sealed class CompleteGameNightSessionCommandValidator : AbstractValidator<CompleteGameNightSessionCommand>
{
    public CompleteGameNightSessionCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
