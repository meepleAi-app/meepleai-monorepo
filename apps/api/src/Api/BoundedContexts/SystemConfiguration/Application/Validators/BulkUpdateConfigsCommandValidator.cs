using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class BulkUpdateConfigsCommandValidator : AbstractValidator<BulkUpdateConfigsCommand>
{
    public BulkUpdateConfigsCommandValidator()
    {
        RuleFor(x => x.Updates)
            .NotEmpty()
            .WithMessage("Updates is required");

        RuleForEach(x => x.Updates).ChildRules(update =>
        {
            update.RuleFor(u => u.Id)
                .NotEmpty()
                .WithMessage("Update Id is required");

            update.RuleFor(u => u.Value)
                .NotEmpty()
                .WithMessage("Update Value is required")
                .MaximumLength(10000)
                .WithMessage("Update Value must not exceed 10000 characters");
        });

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
