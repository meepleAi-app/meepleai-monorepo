using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ImportGameFromBggCommand.
/// Ensures BggId is a valid positive integer.
/// </summary>
public class ImportGameFromBggCommandValidator : AbstractValidator<ImportGameFromBggCommand>
{
    public ImportGameFromBggCommandValidator()
    {
        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BGG ID must be a positive integer");
    }
}
