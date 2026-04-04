using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RequestAccessCommandValidator : AbstractValidator<RequestAccessCommand>
{
    public RequestAccessCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");
    }
}
