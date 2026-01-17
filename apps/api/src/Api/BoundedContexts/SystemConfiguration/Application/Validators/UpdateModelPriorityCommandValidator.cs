using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdateModelPriorityCommand
/// </summary>
/// <remarks>
/// Issue #2567: Validation rules for AI model priority updates
/// </remarks>
internal sealed class UpdateModelPriorityCommandValidator : AbstractValidator<UpdateModelPriorityCommand>
{
    public UpdateModelPriorityCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.NewPriority)
            .GreaterThan(0)
            .WithMessage("Priority must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Priority must not exceed 100");
    }
}
