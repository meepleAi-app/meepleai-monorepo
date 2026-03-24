using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class ToggleAiModelActiveCommandValidator : AbstractValidator<ToggleAiModelActiveCommand>
{
    public ToggleAiModelActiveCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
