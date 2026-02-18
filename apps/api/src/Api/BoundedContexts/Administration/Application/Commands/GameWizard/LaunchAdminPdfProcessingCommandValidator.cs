using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Validator for LaunchAdminPdfProcessingCommand.
/// </summary>
internal sealed class LaunchAdminPdfProcessingCommandValidator : AbstractValidator<LaunchAdminPdfProcessingCommand>
{
    public LaunchAdminPdfProcessingCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty)
            .WithMessage("GameId is required");

        RuleFor(x => x.PdfDocumentId)
            .NotEqual(Guid.Empty)
            .WithMessage("PdfDocumentId is required");

        RuleFor(x => x.LaunchedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("LaunchedByUserId is required");
    }
}
