using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="CreateGroupMemoryCommand"/> inputs.
/// </summary>
internal sealed class CreateGroupMemoryCommandValidator : AbstractValidator<CreateGroupMemoryCommand>
{
    public CreateGroupMemoryCommandValidator()
    {
        RuleFor(x => x.CreatorId)
            .NotEmpty()
            .WithMessage("Creator ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Group name is required")
            .MaximumLength(200)
            .WithMessage("Group name cannot exceed 200 characters");
    }
}
