using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class InvalidateCacheCommandValidator : AbstractValidator<InvalidateCacheCommand>
{
    public InvalidateCacheCommandValidator()
    {
        RuleFor(x => x.Key)
            .MaximumLength(500)
            .WithMessage("Key must not exceed 500 characters")
            .When(x => x.Key is not null);
    }
}
