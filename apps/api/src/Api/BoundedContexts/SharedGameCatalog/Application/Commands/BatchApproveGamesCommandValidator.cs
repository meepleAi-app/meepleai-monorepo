using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for BatchApproveGamesCommand.
/// </summary>
internal sealed class BatchApproveGamesCommandValidator : AbstractValidator<BatchApproveGamesCommand>
{
    public BatchApproveGamesCommandValidator()
    {
        RuleFor(x => x.GameIds)
            .NotNull()
            .WithMessage("GameIds is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("GameIds must contain at least one item");

        RuleForEach(x => x.GameIds)
            .NotEmpty()
            .WithMessage("GameId cannot be empty");

        RuleFor(x => x.ApprovedBy)
            .NotEmpty()
            .WithMessage("ApprovedBy is required");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note != null)
            .WithMessage("Note cannot exceed 500 characters");
    }
}
