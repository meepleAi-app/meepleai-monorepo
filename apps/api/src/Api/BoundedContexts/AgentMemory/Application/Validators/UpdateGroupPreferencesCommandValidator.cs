using Api.BoundedContexts.AgentMemory.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.AgentMemory.Application.Validators;

/// <summary>
/// Validates <see cref="UpdateGroupPreferencesCommand"/> inputs.
/// </summary>
internal sealed class UpdateGroupPreferencesCommandValidator : AbstractValidator<UpdateGroupPreferencesCommand>
{
    private static readonly string[] ValidComplexities = ["Light", "Medium", "Heavy"];

    public UpdateGroupPreferencesCommandValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty()
            .WithMessage("Group ID is required");

        RuleFor(x => x.PreferredComplexity)
            .Must(c => c == null || ValidComplexities.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Preferred complexity must be Light, Medium, or Heavy");

        RuleFor(x => x.CustomNotes)
            .MaximumLength(2000)
            .When(x => x.CustomNotes != null)
            .WithMessage("Custom notes cannot exceed 2000 characters");
    }
}
