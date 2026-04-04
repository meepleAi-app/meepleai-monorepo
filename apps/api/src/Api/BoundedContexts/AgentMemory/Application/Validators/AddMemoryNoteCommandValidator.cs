using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="AddMemoryNoteCommand"/> inputs.
/// </summary>
internal sealed class AddMemoryNoteCommandValidator : AbstractValidator<AddMemoryNoteCommand>
{
    public AddMemoryNoteCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.OwnerId)
            .NotEmpty()
            .WithMessage("Owner ID is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(5000)
            .WithMessage("Content cannot exceed 5000 characters");
    }
}
