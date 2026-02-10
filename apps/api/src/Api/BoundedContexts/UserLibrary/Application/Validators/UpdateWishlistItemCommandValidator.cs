using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UpdateWishlistItemCommand.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal sealed class UpdateWishlistItemCommandValidator : AbstractValidator<UpdateWishlistItemCommand>
{
    private static readonly string[] ValidPriorities = ["HIGH", "MEDIUM", "LOW"];

    public UpdateWishlistItemCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.WishlistItemId)
            .NotEmpty()
            .WithMessage("WishlistItemId is required");

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p!, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Priority must be HIGH, MEDIUM, or LOW")
            .When(x => x.Priority != null);

        RuleFor(x => x.TargetPrice)
            .GreaterThan(0)
            .WithMessage("TargetPrice must be greater than 0")
            .When(x => x.TargetPrice.HasValue && !x.ClearTargetPrice);

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters")
            .When(x => x.Notes != null && !x.ClearNotes);
    }
}
