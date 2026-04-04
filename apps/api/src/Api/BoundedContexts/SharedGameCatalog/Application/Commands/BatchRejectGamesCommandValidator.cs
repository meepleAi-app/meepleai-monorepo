using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for BatchRejectGamesCommand.
/// </summary>
internal sealed class BatchRejectGamesCommandValidator : AbstractValidator<BatchRejectGamesCommand>
{
    public BatchRejectGamesCommandValidator()
    {
        RuleFor(x => x.GameIds)
            .NotNull()
            .WithMessage("GameIds is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("GameIds must contain at least one item");

        RuleForEach(x => x.GameIds)
            .NotEmpty()
            .WithMessage("GameId cannot be empty");

        RuleFor(x => x.RejectedBy)
            .NotEmpty()
            .WithMessage("RejectedBy is required");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required")
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters");
    }
}
