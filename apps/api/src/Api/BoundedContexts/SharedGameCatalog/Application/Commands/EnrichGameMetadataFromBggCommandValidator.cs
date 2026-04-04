using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for EnrichGameMetadataFromBggCommand.
/// </summary>
internal sealed class EnrichGameMetadataFromBggCommandValidator : AbstractValidator<EnrichGameMetadataFromBggCommand>
{
    public EnrichGameMetadataFromBggCommandValidator()
    {
        RuleFor(x => x.ExtractedMetadata)
            .NotNull()
            .WithMessage("ExtractedMetadata is required");

        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BggId must be greater than 0");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
