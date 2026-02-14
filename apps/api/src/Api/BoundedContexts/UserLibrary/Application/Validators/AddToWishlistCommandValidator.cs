using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for AddToWishlistCommand.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal sealed class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    private static readonly string[] ValidPriorities = ["HIGH", "MEDIUM", "LOW"];

    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .WithMessage("Priority is required")
            .Must(p => ValidPriorities.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Priority must be HIGH, MEDIUM, or LOW");

        RuleFor(x => x.TargetPrice)
            .GreaterThan(0)
            .WithMessage("TargetPrice must be greater than 0")
            .When(x => x.TargetPrice.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null);
    }
}
