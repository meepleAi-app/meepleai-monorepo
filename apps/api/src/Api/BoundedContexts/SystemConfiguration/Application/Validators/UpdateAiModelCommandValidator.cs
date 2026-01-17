using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdateAiModelCommand
/// </summary>
/// <remarks>
/// Issue #2567: Validation rules for AI model updates
/// Note: ModelId and DisplayName are immutable and not part of update command
/// </remarks>
internal sealed class UpdateAiModelCommandValidator : AbstractValidator<UpdateAiModelCommand>
{
    public UpdateAiModelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.Priority)
            .GreaterThan(0)
            .WithMessage("Priority must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Priority must not exceed 100");

        RuleFor(x => x.Settings)
            .NotNull()
            .WithMessage("Settings are required");

        RuleFor(x => x.Settings.MaxTokens)
            .GreaterThan(0)
            .WithMessage("MaxTokens must be greater than 0")
            .LessThanOrEqualTo(32000)
            .WithMessage("MaxTokens must not exceed 32000")
            .When(x => x.Settings != null);

        RuleFor(x => x.Settings.Temperature)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Temperature must be >= 0")
            .LessThanOrEqualTo(2)
            .WithMessage("Temperature must be <= 2")
            .When(x => x.Settings != null);
    }
}
