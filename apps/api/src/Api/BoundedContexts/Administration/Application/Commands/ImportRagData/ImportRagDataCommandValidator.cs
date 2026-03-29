using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

/// <summary>
/// Validates <see cref="ImportRagDataCommand"/>.
/// </summary>
internal sealed class ImportRagDataCommandValidator : AbstractValidator<ImportRagDataCommand>
{
    public ImportRagDataCommandValidator()
    {
        RuleFor(x => x.SnapshotPath)
            .NotEmpty()
            .WithMessage("SnapshotPath must not be empty");
    }
}
