using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateSharedGameFromBggCommand.
/// </summary>
internal sealed class UpdateSharedGameFromBggCommandValidator : AbstractValidator<UpdateSharedGameFromBggCommand>
{
    public UpdateSharedGameFromBggCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BGG ID must be a positive integer");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleForEach(x => x.FieldsToUpdate)
            .Must(field => BggUpdatableFields.IsValid(field))
            .WithMessage("Invalid field name: {PropertyValue}. Valid fields are: " +
                string.Join(", ", BggUpdatableFields.All))
            .When(x => x.FieldsToUpdate is not null && x.FieldsToUpdate.Count > 0);
    }
}
