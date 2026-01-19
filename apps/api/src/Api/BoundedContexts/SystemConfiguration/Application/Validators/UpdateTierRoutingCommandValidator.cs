using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdateTierRoutingCommand.
/// Issue #2596: LLM tier routing validation.
/// </summary>
internal sealed class UpdateTierRoutingCommandValidator : AbstractValidator<UpdateTierRoutingCommand>
{
    public UpdateTierRoutingCommandValidator()
    {
        RuleFor(x => x.Tier)
            .IsInEnum()
            .WithMessage("Invalid user tier");

        RuleFor(x => x.ProductionModelId)
            .NotEmpty()
            .WithMessage("Production model ID is required")
            .MaximumLength(200)
            .WithMessage("Production model ID must not exceed 200 characters");

        RuleFor(x => x.TestModelId)
            .NotEmpty()
            .WithMessage("Test model ID is required")
            .MaximumLength(200)
            .WithMessage("Test model ID must not exceed 200 characters");
    }
}
