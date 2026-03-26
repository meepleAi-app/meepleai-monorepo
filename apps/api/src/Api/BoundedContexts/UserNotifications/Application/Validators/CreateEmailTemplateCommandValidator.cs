using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class CreateEmailTemplateCommandValidator : AbstractValidator<CreateEmailTemplateCommand>
{
    public CreateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Locale)
            .NotEmpty()
            .WithMessage("Locale is required")
            .MaximumLength(10)
            .WithMessage("Locale must not exceed 10 characters");

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

        RuleFor(x => x.CreatedBy)
            .NotEmpty()
            .WithMessage("CreatedBy is required");
    }
}
