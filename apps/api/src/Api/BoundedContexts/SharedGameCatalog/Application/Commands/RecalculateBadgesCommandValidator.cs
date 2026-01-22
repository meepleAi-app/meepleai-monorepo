using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for RecalculateBadgesCommand.
/// </summary>
public sealed class RecalculateBadgesCommandValidator : AbstractValidator<RecalculateBadgesCommand>
{
    public RecalculateBadgesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .When(x => x.UserId.HasValue)
            .WithMessage("User ID must not be empty if provided.");
    }
}
