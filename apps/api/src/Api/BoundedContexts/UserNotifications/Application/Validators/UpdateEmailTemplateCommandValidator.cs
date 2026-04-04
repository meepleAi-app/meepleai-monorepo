using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .MaximumLength(500)
            .WithMessage("Subject must not exceed 500 characters");

        RuleFor(x => x.HtmlBody)
            .NotEmpty()
            .WithMessage("HtmlBody is required")
            .MaximumLength(100000)
            .WithMessage("HtmlBody must not exceed 100000 characters");

        RuleFor(x => x.ModifiedBy)
            .NotEmpty()
            .WithMessage("ModifiedBy is required");
    }
}
