using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

/// <summary>
/// Validator for BatchAddRagToSharedGameCommand.
/// Enforces list constraints before individual items are processed.
/// </summary>
internal sealed class BatchAddRagToSharedGameCommandValidator : AbstractValidator<BatchAddRagToSharedGameCommand>
{
    private const int MaxBatchSize = 20;

    public BatchAddRagToSharedGameCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items list is required")
            .NotEmpty().WithMessage("At least one item is required")
            .Must(items => items.Count <= MaxBatchSize)
            .WithMessage($"Batch size cannot exceed {MaxBatchSize} items");
    }
}
