using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to finalize a game night, transitioning it to Completed status.
/// All sessions must be finished before finalization.
/// </summary>
internal record FinalizeGameNightCommand(
    Guid GameNightId,
    Guid UserId
) : ICommand;

internal sealed class FinalizeGameNightCommandValidator : AbstractValidator<FinalizeGameNightCommand>
{
    public FinalizeGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
