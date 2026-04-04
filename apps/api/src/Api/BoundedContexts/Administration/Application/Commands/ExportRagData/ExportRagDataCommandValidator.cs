using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

/// <summary>
/// Validates <see cref="ExportRagDataCommand"/>.
/// </summary>
internal sealed class ExportRagDataCommandValidator : AbstractValidator<ExportRagDataCommand>
{
    public ExportRagDataCommandValidator()
    {
        RuleFor(x => x.GameIdFilter)
            .Must(v => v == null || Guid.TryParse(v, out _))
            .WithMessage("GameIdFilter must be a valid GUID when provided");
    }
}
