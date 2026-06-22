using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validates <see cref="RestorePlayRecordVersionCommand"/> (issue #2437-3).
/// </summary>
internal sealed class RestorePlayRecordVersionCommandValidator
    : AbstractValidator<RestorePlayRecordVersionCommand>
{
    public RestorePlayRecordVersionCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.VersionNumber)
            .GreaterThan(0)
            .WithMessage("Version number must be greater than 0.");
    }
}
