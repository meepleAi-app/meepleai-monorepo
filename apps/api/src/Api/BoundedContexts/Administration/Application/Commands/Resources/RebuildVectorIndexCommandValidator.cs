using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Validator for RebuildVectorIndexCommand.
/// Ensures collection name and confirmation are provided.
/// Issue #3695: Resources Monitoring - Rebuild vector index validation
/// </summary>
internal class RebuildVectorIndexCommandValidator : AbstractValidator<RebuildVectorIndexCommand>
{
    public RebuildVectorIndexCommandValidator()
    {
        RuleFor(x => x.CollectionName)
            .NotEmpty()
            .WithMessage("Collection name is required.");

        RuleFor(x => x.Confirmed)
            .Equal(true)
            .WithMessage("Confirmation is required to rebuild the vector index. This operation may take significant time.");
    }
}
