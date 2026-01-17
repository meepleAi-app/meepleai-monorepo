using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

public sealed class UpdateAiModelPriorityCommandValidator : AbstractValidator<UpdateAiModelPriorityCommand>
{
    public UpdateAiModelPriorityCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.NewPriority)
            .GreaterThanOrEqualTo(1);
    }
}
