using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Validator for <see cref="ReverseStorageMigrationCommand"/>.
/// </summary>
internal sealed class ReverseStorageMigrationCommandValidator : AbstractValidator<ReverseStorageMigrationCommand>
{
    public ReverseStorageMigrationCommandValidator()
    {
        RuleFor(c => c.MigrationId)
            .NotEqual(Guid.Empty)
            .WithMessage("MigrationId must be a non-empty GUID.");
    }
}
