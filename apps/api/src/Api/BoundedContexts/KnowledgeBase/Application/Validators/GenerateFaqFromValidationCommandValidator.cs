using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for GenerateFaqFromValidationCommand.
/// </summary>
internal sealed class GenerateFaqFromValidationCommandValidator : AbstractValidator<GenerateFaqFromValidationCommand>
{
    public GenerateFaqFromValidationCommandValidator()
    {
        RuleFor(x => x.ValidationId)
            .NotEmpty()
            .WithMessage("ValidationId is required");
    }
}
