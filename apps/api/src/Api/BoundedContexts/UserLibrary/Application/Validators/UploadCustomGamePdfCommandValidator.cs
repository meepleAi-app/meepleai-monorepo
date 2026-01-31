using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UploadCustomGamePdfCommand.
/// </summary>
internal sealed class UploadCustomGamePdfCommandValidator : AbstractValidator<UploadCustomGamePdfCommand>
{
    private const long MaxFileSizeBytes = 100_000_000; // 100 MB

    public UploadCustomGamePdfCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.PdfUrl)
            .NotEmpty()
            .WithMessage("PDF URL is required")
            .Must(BeValidHttpsUrl)
            .WithMessage("PDF URL must be a valid HTTPS URL (not HTTP, file://, or internal addresses)");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File size must be greater than 0")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size cannot exceed {MaxFileSizeBytes / 1_000_000} MB");

        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .WithMessage("Original file name is required")
            .MaximumLength(255)
            .WithMessage("File name cannot exceed 255 characters")
            .Must(BeValidFileName)
            .WithMessage("Invalid file name format");
    }

    private static bool BeValidHttpsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // SECURITY: Only HTTPS allowed (no HTTP, file://, javascript:, data:)
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            return false;

        // SSRF Protection: Block loopback and private IPs
        if (uri.IsLoopback)
            return false;

        if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
        {
            var host = uri.Host;
            if (host.StartsWith("127.", StringComparison.Ordinal) ||
                host.StartsWith("10.", StringComparison.Ordinal) ||
                host.StartsWith("172.16.", StringComparison.Ordinal) ||
                host.StartsWith("192.168.", StringComparison.Ordinal) ||
                string.Equals(host, "::1", StringComparison.Ordinal) ||
                host.StartsWith("fe80:", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool BeValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var invalidChars = Path.GetInvalidFileNameChars();
        return !fileName.Any(c => invalidChars.Contains(c));
    }
}
