using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for UploadPdfCommand.
/// Validates file, user, and optional priority parameter.
/// </summary>
internal sealed class UploadPdfCommandValidator : AbstractValidator<UploadPdfCommand>
{
    private static readonly string[] ValidPriorities = ["normal", "urgent"];

    public UploadPdfCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.UserId).NotEmpty();
        When(x => x.Priority != null, () =>
        {
            RuleFor(x => x.Priority)
                .Must(p => ValidPriorities.Contains(p!.ToLowerInvariant(), StringComparer.Ordinal))
                .WithMessage("Priority must be 'normal' or 'urgent'");
        });
    }
}
