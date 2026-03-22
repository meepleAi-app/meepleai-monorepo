using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for AutoDownloadPdfCommand.
/// </summary>
internal sealed class AutoDownloadPdfCommandValidator : AbstractValidator<AutoDownloadPdfCommand>
{
    public AutoDownloadPdfCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required");

        RuleFor(x => x.PdfUrl)
            .NotEmpty()
            .WithMessage("PdfUrl is required")
            .MaximumLength(2000)
            .WithMessage("PdfUrl cannot exceed 2000 characters")
            .Must(BeValidHttpsUrl)
            .WithMessage("PdfUrl must be a valid HTTPS URL");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty()
            .WithMessage("RequestedByUserId is required");
    }

    private static bool BeValidHttpsUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
