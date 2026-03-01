using Api.BoundedContexts.GameManagement.Application.Commands.GameReviews;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameReviews;

/// <summary>
/// Validator for CreateGameReviewCommand.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class CreateGameReviewCommandValidator : AbstractValidator<CreateGameReviewCommand>
{
    public CreateGameReviewCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.AuthorName)
            .NotEmpty()
            .WithMessage("AuthorName is required")
            .MaximumLength(100)
            .WithMessage("AuthorName cannot exceed 100 characters");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 10)
            .WithMessage("Rating must be between 1 and 10");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required");
    }
}
