using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.Contact;

/// <summary>
/// Validator for SendContactMessageCommand.
/// Ensures all fields are present and within acceptable lengths.
/// </summary>
internal sealed class SendContactMessageCommandValidator : AbstractValidator<SendContactMessageCommand>
{
    public SendContactMessageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("A valid email address is required")
            .MaximumLength(255)
            .WithMessage("Email cannot exceed 255 characters");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .MaximumLength(200)
            .WithMessage("Subject cannot exceed 200 characters");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required")
            .MaximumLength(5000)
            .WithMessage("Message cannot exceed 5000 characters");
    }
}
