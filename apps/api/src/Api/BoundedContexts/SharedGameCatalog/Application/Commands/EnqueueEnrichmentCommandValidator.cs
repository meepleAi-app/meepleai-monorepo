using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for EnqueueEnrichmentCommand.
/// </summary>
internal sealed class EnqueueEnrichmentCommandValidator : AbstractValidator<EnqueueEnrichmentCommand>
{
    public EnqueueEnrichmentCommandValidator()
    {
        RuleFor(x => x.SharedGameIds)
            .NotNull()
            .WithMessage("SharedGameIds is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("SharedGameIds must contain at least one item");

        RuleForEach(x => x.SharedGameIds)
            .NotEmpty()
            .WithMessage("SharedGameId cannot be empty");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

/// <summary>
/// Validator for EnqueueAllSkeletonsCommand.
/// </summary>
internal sealed class EnqueueAllSkeletonsCommandValidator : AbstractValidator<EnqueueAllSkeletonsCommand>
{
    public EnqueueAllSkeletonsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

/// <summary>
/// Validator for MarkGamesCompleteCommand.
/// </summary>
internal sealed class MarkGamesCompleteCommandValidator : AbstractValidator<MarkGamesCompleteCommand>
{
    public MarkGamesCompleteCommandValidator()
    {
        RuleFor(x => x.SharedGameIds)
            .NotNull()
            .WithMessage("SharedGameIds is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("SharedGameIds must contain at least one item");

        RuleForEach(x => x.SharedGameIds)
            .NotEmpty()
            .WithMessage("SharedGameId cannot be empty");
    }
}
